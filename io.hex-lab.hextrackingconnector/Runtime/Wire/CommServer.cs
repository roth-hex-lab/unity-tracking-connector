using System;
using System.Threading;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum TransportMode
    {
        Pipe,
        Udp,
    }

    [SkeletonPipelineNode("CommServer")]
    public class CommServer : MonoBehaviour, ISkeletonProvider, ISkeletonCaptureSource
    {
        private const string DefaultPipeName = "UnityMediaPipeBody";

        [SerializeField] private TransportMode transportMode = TransportMode.Pipe;
        [SerializeField] private string pipeName = DefaultPipeName;
        [SerializeField, Min(1)] private int udpPort = 5000;
        [SerializeField] private InputSkeletonSelection inputSkeleton = InputSkeletonSelection.Auto;
        [SerializeField] private PoseCoordinateSource coordinateSource = PoseCoordinateSource.Free;
        [SerializeField] private PoseMirrorMode mirrorMode = PoseMirrorMode.None;
        [SerializeField] private bool logConnectionEvents = true;

        private readonly object capturedPoseLock = new object();
        private volatile TransportSession transportSession;
        private bool transportRequested;
        private SkeletonFrame pendingFrame;
        private bool hasPendingFrame;
        private int sequenceNumber;

        private SkeletonFrame latestPose;
        private bool hasLatestPose;
        private SkeletonFrame latestCapturedPose;
        private bool hasLatestCapturedPose;

        public event Action<SkeletonFrame> PoseReceived;
        public event Action<SkeletonFrame> FrameCaptured;

        public TransportMode CurrentTransportMode => transportMode;
        internal TransportSession Session => transportSession;

        public InputSkeletonSelection InputSkeleton
        {
            get => inputSkeleton;
            set => inputSkeleton = value;
        }

        public PoseCoordinateSource CoordinateSource
        {
            get => coordinateSource;
            set => coordinateSource = value;
        }

        public PoseMirrorMode MirrorMode
        {
            get => mirrorMode;
            set => mirrorMode = value;
        }

        public bool IsRunning
        {
            get
            {
                var session = transportSession;
                return session != null && !session.IsStopping && !session.Completion.IsCompleted;
            }
        }
        public int PendingFrameCount
        {
            get { lock (capturedPoseLock) return hasPendingFrame ? 1 : 0; }
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool TryGetLatestCapturedFrame(out SkeletonFrame frame)
        {
            lock (capturedPoseLock)
            {
                frame = latestCapturedPose;
                return hasLatestCapturedPose;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            transportRequested = true;
            StartTransport();
        }

        private void Update()
        {
            if (transportRequested && (transportSession == null || transportSession.Completion.IsCompleted))
                StartTransport();
            PublishPendingFrames();
        }

        private void OnDisable()
        {
            transportRequested = false;
            StopTransport();
        }

        private void OnDestroy()
        {
            transportRequested = false;
            StopTransport();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(pipeName))
            {
                pipeName = DefaultPipeName;
            }

            udpPort = Mathf.Max(1, udpPort);
        }

        private void StartTransport()
        {
            if (transportSession != null && !transportSession.Completion.IsCompleted) return;
            ClearFrames();
            var session = new TransportSession(transportMode, pipeName, udpPort,
                OnSessionPayload, LogConnection,
                exception => Debug.LogWarning($"HEX tracking transport error: {exception.Message}"));
            transportSession = session;
            session.Start();
        }

        private void StopTransport()
        {
            var session = transportSession;
            if (session == null) return;
            session.RequestStop();
            ClearFrames();
            if (session.Completion.Wait(1000))
            {
                transportSession = null;
            }
            else
            {
                Debug.LogWarning("HEX tracking transport is still stopping; restart will wait for completion.", this);
            }
        }

        private void ClearFrames()
        {
            lock (capturedPoseLock)
            {
                pendingFrame = default;
                hasPendingFrame = false;
                latestCapturedPose = default;
                hasLatestCapturedPose = false;
            }
            latestPose = default;
            hasLatestPose = false;
        }

        private void PublishPendingFrames()
        {
            SkeletonFrame frame;
            lock (capturedPoseLock)
            {
                if (!hasPendingFrame) return;
                frame = pendingFrame;
                pendingFrame = default;
                hasPendingFrame = false;
            }
            latestPose = frame;
            hasLatestPose = true;
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, frame, this);
        }

        private void OnSessionPayload(TransportSession session, string json)
        {
            if (!ReferenceEquals(transportSession, session) || session.IsStopping) return;
            if (TryParseSkeletonFrame(json, out var frame)) StoreFrame(frame, session);
        }

        private void ParseAndEnqueue(string json)
        {
            if (TryParseSkeletonFrame(json, out var frame)) StoreFrame(frame);
        }

        private void StoreFrame(SkeletonFrame frame, TransportSession owner = null)
        {
            lock (capturedPoseLock)
            {
                if (owner != null && (!ReferenceEquals(transportSession, owner) || owner.IsStopping)) return;
                latestCapturedPose = frame;
                hasLatestCapturedPose = true;
            }
            RaiseFrameCaptured(frame);
            lock (capturedPoseLock)
            {
                if (owner != null && (!ReferenceEquals(transportSession, owner) || owner.IsStopping)) return;
                pendingFrame = frame;
                hasPendingFrame = true;
            }
        }

        private void RaiseFrameCaptured(SkeletonFrame frame)
        {
            var handlers = FrameCaptured;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SkeletonFrame> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(frame);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private bool TryParseSkeletonFrame(string json, out SkeletonFrame skeletonFrame)
        {
            skeletonFrame = default;

            PoseFrame frame;
            try
            {
                frame = JsonUtility.FromJson<PoseFrame>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to parse PoseFrame JSON. Skipping frame. {exception.Message}");
                return false;
            }

            if (frame == null)
            {
                return false;
            }

            var landmarks = frame.GetLandmarks(coordinateSource);
            if (landmarks == null || landmarks.Length == 0)
            {
                return false;
            }

            if (!InputSkeletonRegistry.TryGetMapper(inputSkeleton, frame.skeleton_id, out var mapper))
            {
                Debug.LogWarning($"Unsupported input skeleton id '{frame.skeleton_id}'. Skipping frame.");
                return false;
            }

            var definition = mapper.Definition;
            var jointPoses = new SkeletonJointPose[definition.JointCount];
            var foundAny = false;

            foreach (var landmarkData in landmarks)
            {
                if (landmarkData == null ||
                    !mapper.TryMapIndex(landmarkData.index, out var joint))
                {
                    continue;
                }

                var mappedIndex = definition.IndexOf(joint);
                if (!definition.IsValidIndex(mappedIndex))
                {
                    continue;
                }

                jointPoses[mappedIndex] = landmarkData.ToJointPose();
                foundAny = true;
            }

            if (!foundAny)
            {
                return false;
            }

            var coordinateSpace = coordinateSource == PoseCoordinateSource.Anchored
                ? SkeletonCoordinateSpace.RootRelative
                : SkeletonCoordinateSpace.World;
            var pose = new SkeletonPose(definition, jointPoses, coordinateSpace);
            if (mirrorMode == PoseMirrorMode.SwapLeftRight)
            {
                pose = SkeletonPoseTransforms.MirrorLeftRight(pose);
            }

            var metadata = new SkeletonFrameMetadata(
                Interlocked.Increment(ref sequenceNumber),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                sourceId: frame.skeleton_id);

            skeletonFrame = new SkeletonFrame(
                pose,
                metadata);
            return true;
        }

        private void LogConnection(string message)
        {
            if (logConnectionEvents)
            {
                Debug.Log(message);
            }
        }
    }
}
