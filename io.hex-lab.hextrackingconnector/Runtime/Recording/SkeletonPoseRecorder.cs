using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
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

        private readonly ConcurrentQueue<QueuedRecordingFrame> queuedFrames =
            new ConcurrentQueue<QueuedRecordingFrame>();
        private readonly object stopReasonLock = new object();

        private ISkeletonProvider activeProvider;
        private ISkeletonCaptureSource activeCaptureSource;
        private AutoResetEvent writerSignal;
        private Thread writerThread;
        private Stopwatch recordingClock;
        private volatile bool isRecording;
        private volatile bool writerShouldStop;
        private volatile bool stopRequested;
        private string stopReason;
        private Exception writerException;
        private string activeOutputPath;
        private double lastRecordedDuration;
        private int queuedFrameCount;
        private int writtenFrameCount;
        private int droppedFrameCount;

        public bool IsRecording => isRecording;
        public string RecordingFolder => ResolveRecordingFolder();
        public string OutputPath => RecordingFolder;
        public string ActiveOutputPath => activeOutputPath ?? string.Empty;
        public SkeletonRecordingFormat RecordingFormat => recordingFormat;
        public int QueuedFrameCount => Math.Max(0, Volatile.Read(ref queuedFrameCount));
        public int WrittenFrameCount => Volatile.Read(ref writtenFrameCount);
        public int DroppedFrameCount => Volatile.Read(ref droppedFrameCount);
        public double RecordingDuration => isRecording && recordingClock != null
            ? recordingClock.Elapsed.TotalSeconds
            : lastRecordedDuration;

        private void OnEnable()
        {
            ResolveAndSubscribe();
        }

        private void Start()
        {
            if (recordOnStart)
            {
                StartRecording();
            }
        }

        private void Update()
        {
            if (writerException != null)
            {
                var exception = writerException;
                writerException = null;
                StopRecording();
                Debug.LogException(exception, this);
                return;
            }

            if (stopRequested)
            {
                var reason = ConsumeStopReason();
                StopRecording();
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.LogWarning(reason, this);
                }

                return;
            }

            if (!isRecording)
            {
                return;
            }

            if (maxDurationSeconds > 0f && RecordingDuration >= maxDurationSeconds)
            {
                StopRecording();
                return;
            }

            if (maxFrameCount > 0 && WrittenFrameCount >= maxFrameCount)
            {
                StopRecording();
            }
        }

        private void OnDisable()
        {
            StopRecording();
            Unsubscribe();
        }

        private void OnValidate()
        {
            maxDurationSeconds = Mathf.Max(0f, maxDurationSeconds);
            maxFrameCount = Mathf.Max(0, maxFrameCount);
            maxQueuedFrames = Mathf.Max(1, maxQueuedFrames);
        }

        public bool StartRecording()
        {
            if (isRecording || writerThread != null)
            {
                return false;
            }

            if (!ResolveAndSubscribe())
            {
                Debug.LogWarning("SkeletonPoseRecorder has no usable source to record.", this);
                return false;
            }

            DrainQueue();
            writerException = null;
            stopRequested = false;
            SetStopReason(null);
            Interlocked.Exchange(ref queuedFrameCount, 0);
            Interlocked.Exchange(ref writtenFrameCount, 0);
            Interlocked.Exchange(ref droppedFrameCount, 0);
            lastRecordedDuration = 0.0;
            activeOutputPath = ResolveOutputPath();
            recordingClock = Stopwatch.StartNew();
            writerSignal = new AutoResetEvent(false);
            writerShouldStop = false;
            isRecording = true;

            writerThread = new Thread(RunWriterLoop)
            {
                IsBackground = true,
                Name = "HEX Pose Recording Writer",
            };
            writerThread.Start();
            return true;
        }

        public void StopRecording()
        {
            if (!isRecording && writerThread == null)
            {
                return;
            }

            isRecording = false;
            writerShouldStop = true;
            recordingClock?.Stop();
            if (recordingClock != null)
            {
                lastRecordedDuration = recordingClock.Elapsed.TotalSeconds;
            }

            writerSignal?.Set();
            if (writerThread != null && writerThread.IsAlive && !ReferenceEquals(Thread.CurrentThread, writerThread))
            {
                if (!writerThread.Join(1000))
                {
                    Debug.LogWarning("SkeletonPoseRecorder writer thread is still flushing frames.", this);
                    return;
                }
            }

            writerThread = null;
            writerSignal?.Dispose();
            writerSignal = null;
            recordingClock = null;
        }

        public void SetOutputPath(string path)
        {
            SetRecordingFolder(path);
        }

        public void SetRecordingFolder(string folder)
        {
            recordingFolder = folder;
        }

        private bool ResolveAndSubscribe()
        {
            Unsubscribe();

            if (source == null)
            {
                return false;
            }

            if (sourceMode == SkeletonRecorderSourceMode.CaptureSource ||
                sourceMode == SkeletonRecorderSourceMode.Auto)
            {
                activeCaptureSource = source as ISkeletonCaptureSource;
                if (activeCaptureSource != null)
                {
                    activeCaptureSource.FrameCaptured += OnFrameObserved;
                    return true;
                }
            }

            if (sourceMode == SkeletonRecorderSourceMode.ProviderOutput ||
                sourceMode == SkeletonRecorderSourceMode.Auto)
            {
                if (SkeletonProviderUtility.TryResolveProvider(
                        source,
                        this,
                        "Source",
                        allowSelf: false,
                        out activeProvider))
                {
                    activeProvider.PoseReceived += OnFrameObserved;
                    return true;
                }
            }

            Debug.LogError($"{name}: Source does not match recorder source mode '{sourceMode}'.", this);
            return false;
        }

        private void Unsubscribe()
        {
            if (activeProvider != null)
            {
                activeProvider.PoseReceived -= OnFrameObserved;
                activeProvider = null;
            }

            if (activeCaptureSource != null)
            {
                activeCaptureSource.FrameCaptured -= OnFrameObserved;
                activeCaptureSource = null;
            }
        }

        private void OnFrameObserved(SkeletonFrame frame)
        {
            if (!isRecording || recordingClock == null)
            {
                return;
            }

            var queuedCount = Interlocked.Increment(ref queuedFrameCount);
            if (queuedCount > maxQueuedFrames)
            {
                Interlocked.Decrement(ref queuedFrameCount);
                HandleQueueOverflow();
                return;
            }

            queuedFrames.Enqueue(new QueuedRecordingFrame(
                frame,
                recordingClock.Elapsed.TotalSeconds));
            writerSignal?.Set();
        }

        private void HandleQueueOverflow()
        {
            Interlocked.Increment(ref droppedFrameCount);
            if (overflowMode == SkeletonRecordingOverflowMode.DropNewestFrame)
            {
                return;
            }

            isRecording = false;
            RequestStop("SkeletonPoseRecorder stopped because its recording queue filled up.");
        }

        private void RunWriterLoop()
        {
            ISkeletonRecordingWriter writer = null;
            SkeletonDefinition activeDefinition = null;
            var nextRecordIndex = 0;

            try
            {
                while (!writerShouldStop || !queuedFrames.IsEmpty)
                {
                    if (!queuedFrames.TryDequeue(out var queuedFrame))
                    {
                        writerSignal?.WaitOne(50);
                        continue;
                    }

                    Interlocked.Decrement(ref queuedFrameCount);
                    if (writer == null)
                    {
                        activeDefinition = queuedFrame.Frame.Definition;
                        writer = SkeletonRecordingWriterFactory.Create(
                            recordingFormat,
                            activeOutputPath,
                            activeDefinition);
                    }
                    else if (stopOnDefinitionChange &&
                             !string.Equals(
                                 activeDefinition.Id,
                                 queuedFrame.Frame.Definition.Id,
                                 StringComparison.Ordinal))
                    {
                        RequestStop(
                            $"SkeletonPoseRecorder stopped because source definition changed from '{activeDefinition.Id}' to '{queuedFrame.Frame.Definition.Id}'.");
                        continue;
                    }

                    writer.WriteFrame(new SkeletonRecordedFrame(
                        nextRecordIndex++,
                        queuedFrame.RecordedTime,
                        queuedFrame.Frame));
                    Interlocked.Exchange(ref writtenFrameCount, nextRecordIndex);
                }
            }
            catch (Exception exception)
            {
                writerException = exception;
            }
            finally
            {
                writer?.Dispose();
            }
        }

        private void RequestStop(string reason)
        {
            SetStopReason(reason);
            stopRequested = true;
            writerShouldStop = true;
            writerSignal?.Set();
        }

        private void SetStopReason(string reason)
        {
            lock (stopReasonLock)
            {
                stopReason = reason;
            }
        }

        private string ConsumeStopReason()
        {
            lock (stopReasonLock)
            {
                var reason = stopReason;
                stopReason = null;
                stopRequested = false;
                return reason;
            }
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

        private void DrainQueue()
        {
            while (queuedFrames.TryDequeue(out _))
            {
            }
        }

        private readonly struct QueuedRecordingFrame
        {
            public QueuedRecordingFrame(SkeletonFrame frame, double recordedTime)
            {
                Frame = frame;
                RecordedTime = recordedTime;
            }

            public SkeletonFrame Frame { get; }
            public double RecordedTime { get; }
        }
    }
}
