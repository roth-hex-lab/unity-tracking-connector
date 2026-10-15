using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace HEXLab.Hextrackingconnector
{
    [SkeletonPipelineNode("SkeletonPoseRecorder")]
    public class SkeletonPoseRecorder : MonoBehaviour
    {
        [SerializeField, SkeletonProvider(allowSelf: false)] private MonoBehaviour source;
        [SerializeField] private SkeletonRecorderSourceMode sourceMode = SkeletonRecorderSourceMode.ProviderOutput;
        [SerializeField] private SkeletonRecordingFormat recordingFormat = SkeletonRecordingFormat.JsonLines;
        [SerializeField, FormerlySerializedAs("outputPath")] private string recordingFolder;
        [SerializeField] private bool recordOnStart;
        [SerializeField, Min(0f)] private float maxDurationSeconds;
        [SerializeField, Min(0)] private int maxFrameCount;
        [SerializeField, Min(1)] private int maxQueuedFrames = 2048;
        [SerializeField] private SkeletonRecordingOverflowMode overflowMode = SkeletonRecordingOverflowMode.StopRecordingAndWarn;
        [SerializeField] private bool stopOnDefinitionChange = true;

        private ISkeletonProvider activeProvider;
        private ISkeletonCaptureSource activeCaptureSource;
        private Action<SkeletonFrame> frameObserver;
        private RecordingSession recordingSession;
        private bool resultReported;
        private string activeOutputPath;

        internal Func<SkeletonRecordingFormat, string, SkeletonDefinition, ISkeletonRecordingWriter> WriterFactory =
            SkeletonRecordingWriterFactory.Create;
        internal RecordingSession Session => recordingSession;

        public bool IsRecording => recordingSession != null && recordingSession.IsRecording;
        public bool IsStopping => recordingSession != null && !recordingSession.IsRecording && !recordingSession.Completion.IsCompleted;
        public string RecordingFolder => ResolveRecordingFolder();
        public string OutputPath => RecordingFolder;
        public string ActiveOutputPath => activeOutputPath ?? string.Empty;
        public SkeletonRecordingFormat RecordingFormat => recordingFormat;
        public int QueuedFrameCount => recordingSession?.QueuedFrameCount ?? 0;
        public int WrittenFrameCount => recordingSession?.WrittenFrameCount ?? 0;
        public int DroppedFrameCount => recordingSession?.DroppedFrameCount ?? 0;
        public double RecordingDuration => recordingSession?.Duration ?? 0.0;

        private void Start()
        {
            if (recordOnStart) StartRecording();
        }

        private void Update()
        {
            var session = recordingSession;
            if (session == null) return;
            if (!session.IsRecording)
            {
                Unsubscribe();
                ReportResult();
                return;
            }
            if ((maxDurationSeconds > 0f && RecordingDuration >= maxDurationSeconds) ||
                (maxFrameCount > 0 && WrittenFrameCount >= maxFrameCount))
                StopRecording();
        }

        private void OnDisable() { StopRecording(); }
        private void OnDestroy()
        {
            if (IsRecording) StopRecording();
            else Unsubscribe();
        }

        private void OnValidate()
        {
            maxDurationSeconds = Mathf.Max(0f, maxDurationSeconds);
            maxFrameCount = Mathf.Max(0, maxFrameCount);
            maxQueuedFrames = Mathf.Max(1, maxQueuedFrames);
        }

        public bool StartRecording()
        {
            if (recordingSession != null && !recordingSession.Completion.IsCompleted) return false;
            ReportResult();
            if (!ResolveSource())
            {
                Debug.LogWarning("SkeletonPoseRecorder has no usable source to record.", this);
                return false;
            }

            activeOutputPath = ResolveOutputPath();
            var path = activeOutputPath;
            var format = recordingFormat;
            var factory = WriterFactory;
            var session = new RecordingSession(definition => factory(format, path, definition),
                maxQueuedFrames, overflowMode, stopOnDefinitionChange, exception => Debug.LogException(exception));
            recordingSession = session;
            resultReported = false;
            // Capture the session, so a previously dispatched callback cannot feed a later recording.
            frameObserver = frame => session.TryEnqueue(frame);
            session.Start();
            if (activeCaptureSource != null) activeCaptureSource.FrameCaptured += frameObserver;
            else activeProvider.PoseReceived += frameObserver;
            return true;
        }

        public void StopRecording()
        {
            var session = recordingSession;
            session?.RequestStop();
            Unsubscribe();
            if (session == null) return;
            if (!session.Completion.Wait(1000))
                Debug.LogWarning("SkeletonPoseRecorder writer is still finishing; a new recording must wait for completion.", this);
            ReportResult();
        }

        private void ReportResult()
        {
            var session = recordingSession;
            if (session == null || !session.Completion.IsCompleted || resultReported) return;
            resultReported = true;
            if (!string.IsNullOrEmpty(session.StopReason)) Debug.LogWarning(session.StopReason, this);
        }

        public void SetOutputPath(string path) { SetRecordingFolder(path); }
        public void SetRecordingFolder(string folder) { recordingFolder = folder; }

        private bool ResolveSource()
        {
            Unsubscribe();
            if (source == null) return false;
            if (sourceMode == SkeletonRecorderSourceMode.CaptureSource || sourceMode == SkeletonRecorderSourceMode.Auto)
            {
                activeCaptureSource = source as ISkeletonCaptureSource;
                if (activeCaptureSource != null) return true;
            }
            if (sourceMode == SkeletonRecorderSourceMode.ProviderOutput || sourceMode == SkeletonRecorderSourceMode.Auto)
            {
                if (SkeletonProviderUtility.TryResolveProvider(source, this, "Source", allowSelf: false, out activeProvider))
                    return true;
            }
            Debug.LogError($"{name}: Source does not match recorder source mode '{sourceMode}'.", this);
            return false;
        }

        private void Unsubscribe()
        {
            if (activeProvider != null && frameObserver != null) activeProvider.PoseReceived -= frameObserver;
            if (activeCaptureSource != null && frameObserver != null) activeCaptureSource.FrameCaptured -= frameObserver;
            activeProvider = null;
            activeCaptureSource = null;
            frameObserver = null;
        }

        private string ResolveOutputPath()
        {
            var extension = recordingFormat == SkeletonRecordingFormat.Binary
                ? ".hexpose"
                : ".hexpose.jsonl";
            var folder = ResolveRecordingFolder();
            Directory.CreateDirectory(folder);

            var projectName = SanitizeFileName(Application.productName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return CreateUniqueRecordingPath(
                folder,
                $"{timestamp}_{projectName}",
                extension);
        }

        private string ResolveRecordingFolder()
        {
            if (!string.IsNullOrWhiteSpace(recordingFolder))
            {
                var candidate = recordingFolder.Trim();
                if (!Directory.Exists(candidate) && IsRecordingFilePath(candidate))
                {
                    var directory = Path.GetDirectoryName(candidate);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        return directory;
                    }
                }

                return candidate;
            }

            return Path.Combine(
                Application.persistentDataPath,
                "HEX Pose Recordings");
        }

        private static string CreateUniqueRecordingPath(
            string folder,
            string baseFileName,
            string extension)
        {
            var path = Path.Combine(folder, baseFileName + extension);
            if (!File.Exists(path))
            {
                return path;
            }

            for (var index = 1; index < 10000; index++)
            {
                path = Path.Combine(folder, $"{baseFileName}_{index:000}{extension}");
                if (!File.Exists(path))
                {
                    return path;
                }
            }

            return Path.Combine(
                folder,
                $"{baseFileName}_{Guid.NewGuid():N}{extension}");
        }

        private static bool IsRecordingFilePath(string path)
        {
            if (File.Exists(path))
            {
                return true;
            }

            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".hexpose", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.EndsWith(".hexpose.jsonl", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = "UnityProject";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value.Trim();
        }

    }
}
