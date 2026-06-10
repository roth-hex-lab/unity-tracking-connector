using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    [SkeletonPipelineNode("SkeletonPosePlayback")]
    public class SkeletonPosePlayback : MonoBehaviour, ISkeletonProvider
    {
        [SerializeField] private string recordingPath;
        [SerializeField] private bool playOnStart;
        [SerializeField] private bool loop;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField] private SkeletonPlaybackTimeSource timeSource = SkeletonPlaybackTimeSource.RecordedTime;
        [SerializeField] private SkeletonPlaybackCatchUpMode catchUpMode = SkeletonPlaybackCatchUpMode.LatestDueFrame;
        [SerializeField, Min(1)] private int maxFramesPerUpdate = 4;
        [SerializeField] private SkeletonPlaybackEndBehavior endBehavior = SkeletonPlaybackEndBehavior.HoldLastFrame;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Min(0.01f)] private float fixedFrameRate = 30f;

        private SkeletonRecordingReader reader;
        private SkeletonRecordingInfo recordingInfo;
        private SkeletonRecordedFrame pendingFrame;
        private SkeletonFrame latestPose;
        private SkeletonPlaybackState state = SkeletonPlaybackState.Stopped;
        private bool hasPendingFrame;
        private bool hasLatestPose;
        private bool hasTimingBase;
        private double timingBase;
        private double playbackTime;

        public event Action<SkeletonFrame> PoseReceived;

        public SkeletonPlaybackState State => state;
        public SkeletonRecordingInfo RecordingInfo => recordingInfo;
        public string RecordingPath => recordingPath;
        public double PlaybackTime => playbackTime;
        public bool IsLoaded => reader != null;
        public bool IsPlaying => state == SkeletonPlaybackState.Playing;

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (state != SkeletonPlaybackState.Playing)
            {
                return;
            }

            playbackTime += GetDeltaTime() * playbackSpeed;
            PublishDueFrames();
        }

        private void OnDisable()
        {
            Unload();
        }

        private void OnValidate()
        {
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            maxFramesPerUpdate = Mathf.Max(1, maxFramesPerUpdate);
            fixedFrameRate = Mathf.Max(0.01f, fixedFrameRate);
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public void SetRecordingPath(string path)
        {
            if (string.Equals(recordingPath, path, StringComparison.Ordinal))
            {
                return;
            }

            recordingPath = path;
            Unload();
        }

        public bool Load()
        {
            Unload();

            if (string.IsNullOrWhiteSpace(recordingPath))
            {
                Debug.LogWarning("SkeletonPosePlayback needs a recording file path.", this);
                return false;
            }

            try
            {
                recordingInfo = SkeletonRecordingReader.ReadInfo(recordingPath);
                reader = SkeletonRecordingReader.Open(recordingPath);
                ResetPlaybackCursor();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                recordingInfo = null;
                reader = null;
                return false;
            }
        }

        public bool Play()
        {
            if (reader == null && !Load())
            {
                return false;
            }

            state = SkeletonPlaybackState.Playing;
            return true;
        }

        public void Pause()
        {
            if (state == SkeletonPlaybackState.Playing)
            {
                state = SkeletonPlaybackState.Paused;
            }
        }

        public void StopPlayback()
        {
            state = SkeletonPlaybackState.Stopped;
            TryResetReader();
            ResetPlaybackCursor();
        }

        public void Restart()
        {
            if (reader == null && !Load())
            {
                return;
            }

            if (!TryResetReader())
            {
                return;
            }

            ResetPlaybackCursor();
            state = SkeletonPlaybackState.Playing;
        }

        public void Seek(double targetTime)
        {
            if (reader == null && !Load())
            {
                return;
            }

            if (!TryResetReader())
            {
                return;
            }

            ResetPlaybackCursor();
            playbackTime = Math.Max(0.0, targetTime);

            SkeletonRecordedFrame latestDue = default;
            var hasDueFrame = false;
            while (TryReadFrame(out var frame))
            {
                var frameTime = GetFramePlaybackTime(frame);
                if (frameTime > playbackTime)
                {
                    SetPendingFrame(frame);
                    break;
                }

                latestDue = frame;
                hasDueFrame = true;
            }

            if (hasDueFrame)
            {
                PublishFrame(latestDue.Frame);
            }
        }

        public void Unload()
        {
            reader?.Dispose();
            reader = null;
            recordingInfo = null;
            state = SkeletonPlaybackState.Stopped;
            ResetPlaybackCursor();
        }

        private void PublishDueFrames()
        {
            var emittedFrames = 0;
            var reachedEnd = false;
            var hasLatestDueFrame = false;
            var latestDueFrame = default(SkeletonRecordedFrame);

            while (emittedFrames < maxFramesPerUpdate)
            {
                if (!TryReadFrame(out var frame))
                {
                    reachedEnd = true;
                    break;
                }

                var frameTime = GetFramePlaybackTime(frame);
                if (frameTime > playbackTime)
                {
                    SetPendingFrame(frame);
                    break;
                }

                latestDueFrame = frame;
                hasLatestDueFrame = true;
                emittedFrames++;

                if (catchUpMode == SkeletonPlaybackCatchUpMode.AllDueFrames)
                {
                    PublishFrame(frame.Frame);
                }
            }

            if (catchUpMode == SkeletonPlaybackCatchUpMode.LatestDueFrame && hasLatestDueFrame)
            {
                PublishFrame(latestDueFrame.Frame);
            }

            if (reachedEnd && !hasPendingFrame)
            {
                HandlePlaybackEnded();
            }
        }

        private bool TryReadFrame(out SkeletonRecordedFrame frame)
        {
            if (hasPendingFrame)
            {
                frame = pendingFrame;
                hasPendingFrame = false;
                return true;
            }

            if (reader == null)
            {
                frame = default;
                return false;
            }

            return reader.TryReadNextFrame(out frame);
        }

        private void SetPendingFrame(SkeletonRecordedFrame frame)
        {
            pendingFrame = frame;
            hasPendingFrame = true;
        }

        private void HandlePlaybackEnded()
        {
            if (loop)
            {
                if (!TryResetReader())
                {
                    state = SkeletonPlaybackState.Stopped;
                    return;
                }

                ResetPlaybackCursor();
                state = SkeletonPlaybackState.Playing;
                return;
            }

            state = SkeletonPlaybackState.Stopped;
            if (endBehavior == SkeletonPlaybackEndBehavior.ClearPose)
            {
                latestPose = default;
                hasLatestPose = false;
            }
        }

        private void PublishFrame(SkeletonFrame frame)
        {
            latestPose = frame;
            hasLatestPose = true;
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, frame, this);
        }

        private double GetFramePlaybackTime(SkeletonRecordedFrame frame)
        {
            if (timeSource == SkeletonPlaybackTimeSource.FixedFrameRate)
            {
                return frame.RecordIndex / (double)fixedFrameRate;
            }

            var rawTime = GetRawFrameTime(frame);
            if (!hasTimingBase)
            {
                timingBase = rawTime;
                hasTimingBase = true;
            }

            return Math.Max(0.0, rawTime - timingBase);
        }

        private double GetRawFrameTime(SkeletonRecordedFrame frame)
        {
            switch (timeSource)
            {
                case SkeletonPlaybackTimeSource.SourceTimestamp:
                    return frame.Frame.Metadata.SourceTimestamp > 0.0
                        ? frame.Frame.Metadata.SourceTimestamp
                        : frame.RecordedTime;
                case SkeletonPlaybackTimeSource.ReceivedTime:
                    return frame.Frame.Metadata.ReceivedTime > 0.0
                        ? frame.Frame.Metadata.ReceivedTime
                        : frame.RecordedTime;
                case SkeletonPlaybackTimeSource.RecordedTime:
                default:
                    return frame.RecordedTime;
            }
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private bool TryResetReader()
        {
            if (reader == null)
            {
                return false;
            }

            try
            {
                reader.Reset();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                reader?.Dispose();
                reader = null;
                recordingInfo = null;
                return false;
            }
        }

        private void ResetPlaybackCursor()
        {
            playbackTime = 0.0;
            hasPendingFrame = false;
            hasTimingBase = false;
            timingBase = 0.0;
        }
    }
}
