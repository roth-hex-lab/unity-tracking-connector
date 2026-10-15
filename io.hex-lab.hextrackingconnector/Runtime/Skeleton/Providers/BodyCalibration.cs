using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace HEXLab.Hextrackingconnector
{
    public enum BodyCalibrationMode
    {
        None,
        CenterHips,
        CenterHipsGroundFeet,
        GroundFeet,
    }

#pragma warning disable 0649
    [SkeletonPipelineNode("BodyCalibration")]
    public class BodyCalibration : MonoBehaviour, ISkeletonProvider
    {
        private static readonly SkeletonJointId[] GroundJoints =
        {
            BodyJoints.LeftAnkle,
            BodyJoints.RightAnkle,
            BodyJoints.LeftHeel,
            BodyJoints.RightHeel,
            BodyJoints.LeftFootIndex,
            BodyJoints.RightFootIndex,
            UnityHumanoidControlSkeleton.LeftFoot,
            UnityHumanoidControlSkeleton.RightFoot,
            UnityHumanoidControlSkeleton.LeftToes,
            UnityHumanoidControlSkeleton.RightToes,
        };

        [Header("Source")]
        [SerializeField, FormerlySerializedAs("body"), SkeletonProvider(allowSelf: false)] private MonoBehaviour skeletonProvider;
        [SerializeField] private bool publishCalibratedPose = true;

        [Header("Calibration")]
        [Tooltip("Calibrate once when the first usable pose arrives. A successful manual calibration also satisfies this step.")]
        [SerializeField, FormerlySerializedAs("autoCalibrate")] private bool calibrateOnFirstPose = true;
        [SerializeField, FormerlySerializedAs("calibrationMode")] private BodyCalibrationMode initialCalibrationMode = BodyCalibrationMode.CenterHipsGroundFeet;
        [Tooltip("Update the selected calibration on each new source pose. Ground Feet preserves the horizontal offset.")]
        [SerializeField, FormerlySerializedAs("continuousGrounding")] private bool continuousCalibration = true;
        [SerializeField] private BodyCalibrationMode continuousCalibrationMode = BodyCalibrationMode.GroundFeet;
        [SerializeField] private bool smoothContinuousCalibration;
        [Tooltip("Exponential response time in seconds. Smaller values follow changes faster. Initial and manual calibration are always immediate.")]
        [SerializeField, Min(0.001f)] private float continuousSmoothingTime = 0.1f;
        [SerializeField] private float groundHeight;
        [SerializeField] private Vector3 calibrationOffset = Vector3.zero;

        private ISkeletonProvider activeSkeletonProvider;
        private MonoBehaviour boundProvider;
        private bool hasBoundProvider;
        private Action<SkeletonFrame> sourceHandler;
        private int subscriptionVersion;
        // Callbacks may synchronously reset calibration, change providers, or deliver another pose.
        private int stateVersion;
        private bool hasCalibration;
        private bool initialCalibrationComplete;
        private SkeletonFrame lastSourceFrame;
        private bool hasSourceFrame;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;
        private double lastPoseTime;
        private PoseClock lastPoseClock;
        private bool hasPoseTime;

        private enum PoseClock { Source, Received, Realtime }

        public bool CalibrateOnFirstPose => calibrateOnFirstPose;
        public BodyCalibrationMode InitialCalibrationMode => initialCalibrationMode;
        public bool ContinuousCalibration => continuousCalibration;
        public BodyCalibrationMode ContinuousCalibrationMode => continuousCalibrationMode;
        public bool HasCalibration => hasCalibration;
        [Obsolete("Use CalibrateOnFirstPose instead.")]
        public bool AutoCalibrate => calibrateOnFirstPose;
        [Obsolete("Use InitialCalibrationMode instead.")]
        public BodyCalibrationMode CalibrationMode => initialCalibrationMode;
        public float GroundHeight => groundHeight;
        public Vector3 CalibrationOffset => calibrationOffset;
        public bool PublishCalibratedPose => publishCalibratedPose;

        public event Action<SkeletonFrame> PoseReceived;
        public event Action CalibrationChanged;

        private void OnEnable()
        {
            SetupProviderCapture();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        // Inactive components store the source for OnEnable; active components reconnect immediately.
        public void SetSkeletonProvider(MonoBehaviour provider)
        {
            if (ReferenceEquals(skeletonProvider, provider)) return;
            Unsubscribe();
            var version = subscriptionVersion;
            skeletonProvider = provider;
            boundProvider = provider;
            hasBoundProvider = true;
            ClearSourceState();
            if (version != subscriptionVersion) return;
            if (isActiveAndEnabled) SetupProviderCapture();
        }

        public void SetupProviderCapture()
        {
            Unsubscribe();
            if (!isActiveAndEnabled) return;
            var version = subscriptionVersion;
            ResolveSkeletonProvider();
            var providerChanged = hasBoundProvider && !ReferenceEquals(boundProvider, skeletonProvider);
            boundProvider = skeletonProvider;
            hasBoundProvider = true;
            if (providerChanged) ClearSourceState();
            if (version != subscriptionVersion) return;
            Subscribe();
            if (activeSkeletonProvider != null && activeSkeletonProvider.TryGetLatestPose(out var frame))
            {
                ProcessSourceFrame(frame);
            }
        }

        public bool Calibrate(BodyCalibrationMode mode)
        {
            var version = ++stateVersion;
            if (activeSkeletonProvider != null && activeSkeletonProvider.TryGetLatestPose(out var frame))
            {
                if (!CacheSourceFrame(frame)) return false;
            }

            if (!hasSourceFrame || !TryUpdateCalibration(lastSourceFrame.Definition,
                    lastSourceFrame.CopyPositions(), lastSourceFrame.CopyTracked(), mode, 1f, completesInitial: true))
            {
                Debug.LogWarning("BodyCalibration could not calibrate because no usable source pose is available yet.", this);
                return false;
            }

            if (version == stateVersion) RepublishLatestSourcePose();
            return true;
        }

        public void ResetCalibration()
        {
            var version = ++stateVersion;
            calibrationOffset = Vector3.zero;
            hasCalibration = false;
            initialCalibrationComplete = false;
            hasPoseTime = false;
            CalibrationChanged?.Invoke();
            if (version == stateVersion) RepublishLatestSourcePose();
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool Calibrate(Vector3[] positions, bool[] tracked, BodyCalibrationMode mode)
        {
            return Calibrate(HumanPoseSkeleton33.Definition, positions, tracked, mode);
        }

        public bool Calibrate(SkeletonDefinition definition, Vector3[] positions, bool[] tracked, BodyCalibrationMode mode)
        {
            stateVersion++;
            return TryUpdateCalibration(definition, positions, tracked, mode, 1f, completesInitial: true);
        }

        public bool Calibrate(SkeletonFrame frame, BodyCalibrationMode mode)
        {
            return Calibrate(frame.Definition, frame.CopyPositions(), frame.CopyTracked(), mode);
        }

        public bool TryApply(SkeletonFrame sourceFrame, out SkeletonFrame calibratedFrame)
        {
            if (sourceFrame.JointCount == 0)
            {
                calibratedFrame = default;
                return false;
            }

            var jointPoses = sourceFrame.CopyJointPoses();
            for (int i = 0; i < jointPoses.Length; i++)
            {
                var pose = jointPoses[i];
                if (!pose.HasPosition)
                {
                    continue;
                }

                jointPoses[i] = new SkeletonJointPose(
                    pose.Channels,
                    pose.Position + calibrationOffset,
                    pose.Rotation,
                    pose.Confidence,
                    pose.Provenance,
                    pose.Source);
            }

            calibratedFrame = new SkeletonFrame(
                new SkeletonPose(sourceFrame.Definition, jointPoses, sourceFrame.CoordinateSpace),
                sourceFrame.Metadata);
            return true;
        }

        public void Apply(Vector3[] sourcePositions, bool[] sourceTracked, Vector3[] destinationPositions)
        {
            Apply(HumanPoseSkeleton33.Definition, sourcePositions, sourceTracked, destinationPositions);
        }

        public void Apply(
            SkeletonDefinition definition,
            Vector3[] sourcePositions,
            bool[] sourceTracked,
            Vector3[] destinationPositions)
        {
            definition = definition ?? HumanPoseSkeleton33.Definition;
            if (!HasValidPoseArrays(definition, sourcePositions, sourceTracked, destinationPositions))
            {
                return;
            }

            for (int i = 0; i < definition.JointCount; i++)
            {
                destinationPositions[i] = sourceTracked[i]
                    ? sourcePositions[i] + calibrationOffset
                    : Vector3.zero;
            }
        }

        public static Vector3 CalculateOffset(
            Vector3[] positions,
            bool[] tracked,
            BodyCalibrationMode mode,
            float groundHeight)
        {
            return CalculateOffset(HumanPoseSkeleton33.Definition, positions, tracked, mode, groundHeight);
        }

        public static Vector3 CalculateOffset(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            BodyCalibrationMode mode,
            float groundHeight)
        {
            return TryCalculateOffset(definition, positions, tracked, mode, groundHeight, out var offset)
                ? offset
                : Vector3.zero;
        }

        public static bool TryCalculateOffset(
            Vector3[] positions,
            bool[] tracked,
            BodyCalibrationMode mode,
            float groundHeight,
            out Vector3 offset)
        {
            return TryCalculateOffset(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                mode,
                groundHeight,
                out offset);
        }

        public static bool TryCalculateOffset(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            BodyCalibrationMode mode,
            float groundHeight,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            definition = definition ?? HumanPoseSkeleton33.Definition;

            if (mode == BodyCalibrationMode.None)
            {
                return true;
            }

            if (positions == null ||
                tracked == null ||
                positions.Length < definition.JointCount ||
                tracked.Length < definition.JointCount)
            {
                return false;
            }

            if (mode == BodyCalibrationMode.GroundFeet)
            {
                if (!TryGetLowestFootY(definition, positions, tracked, out var lowestY)) return false;
                offset.y = groundHeight - lowestY;
                return true;
            }

            if ((mode != BodyCalibrationMode.CenterHips && mode != BodyCalibrationMode.CenterHipsGroundFeet) ||
                !TryGetHipCentre(definition, positions, tracked, out var hipCentre))
            {
                return false;
            }

            // Calibration is an additive world/root-space offset. It never changes
            // joint rotations or remaps skeleton definitions.
            offset = -hipCentre;

            if (mode == BodyCalibrationMode.CenterHipsGroundFeet)
            {
                if (!TryGetLowestFootY(definition, positions, tracked, out var lowestFootY))
                {
                    offset = Vector3.zero;
                    return false;
                }

                offset.y = groundHeight - lowestFootY;
            }

            return true;
        }

        private void ResolveSkeletonProvider()
        {
            activeSkeletonProvider = null;
            if (skeletonProvider != null)
            {
                SkeletonProviderUtility.TryResolveProvider(
                    skeletonProvider,
                    this,
                    "Skeleton Provider",
                    allowSelf: false,
                    out activeSkeletonProvider);
                return;
            }

            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (component == null || ReferenceEquals(component, this) || !IsUsableProvider(component))
                {
                    continue;
                }

                skeletonProvider = component;
                activeSkeletonProvider = (ISkeletonProvider)component;
                return;
            }
        }

        private bool TryUpdateCalibration(SkeletonDefinition definition, Vector3[] positions, bool[] tracked,
            BodyCalibrationMode mode, float blend, bool completesInitial = false)
        {
            if (!TryCalculateOffset(definition, positions, tracked, mode, groundHeight, out var target))
            {
                return false;
            }

            if (mode == BodyCalibrationMode.GroundFeet)
            {
                target.x = calibrationOffset.x;
                target.z = calibrationOffset.z;
            }

            var offset = Vector3.Lerp(calibrationOffset, target, blend);
            var changed = !hasCalibration || offset != calibrationOffset;
            calibrationOffset = offset;
            hasCalibration = true;
            if (completesInitial) initialCalibrationComplete = true;
            if (changed) CalibrationChanged?.Invoke();
            return true;
        }

        private void Subscribe()
        {
            if (activeSkeletonProvider != null)
            {
                var version = subscriptionVersion;
                sourceHandler = frame =>
                {
                    if (this != null && isActiveAndEnabled && version == subscriptionVersion)
                    {
                        ProcessSourceFrame(frame);
                    }
                };
                activeSkeletonProvider.PoseReceived += sourceHandler;
            }
        }

        private void Unsubscribe()
        {
            subscriptionVersion++;
            stateVersion++;
            if (activeSkeletonProvider != null)
            {
                activeSkeletonProvider.PoseReceived -= sourceHandler;
            }

            sourceHandler = null;
            activeSkeletonProvider = null;
        }

        private void ProcessSourceFrame(SkeletonFrame frame)
        {
            if (frame.JointCount == 0 || IsLastSourceFrame(frame)) return;
            var version = ++stateVersion;
            if (!CacheSourceFrame(frame)) return;
            var blend = GetContinuousBlend(frame);
            var positions = frame.CopyPositions();
            var tracked = frame.CopyTracked();

            var initialized = false;
            if (calibrateOnFirstPose && !initialCalibrationComplete &&
                TryUpdateCalibration(frame.Definition, positions, tracked, initialCalibrationMode, 1f, completesInitial: true))
            {
                initialized = true;
            }
            if (version != stateVersion) return;

            if (continuousCalibration && !initialized)
            {
                TryUpdateCalibration(frame.Definition, positions, tracked, continuousCalibrationMode, blend);
            }

            if (version != stateVersion) return;
            PublishCalibratedFrame(frame);
        }

        private float GetContinuousBlend(SkeletonFrame frame)
        {
            double time;
            PoseClock clock;
            if (IsPositiveFinite(frame.Metadata.SourceTimestamp))
            {
                time = frame.Metadata.SourceTimestamp;
                clock = PoseClock.Source;
            }
            else if (IsPositiveFinite(frame.ReceivedTime))
            {
                time = frame.ReceivedTime;
                clock = PoseClock.Received;
            }
            else
            {
                time = Time.realtimeSinceStartupAsDouble;
                clock = PoseClock.Realtime;
            }

            var sameClock = hasPoseTime && clock == lastPoseClock;
            var elapsed = sameClock ? time - lastPoseTime : 0d;
            lastPoseTime = time;
            lastPoseClock = clock;
            hasPoseTime = true;

            // First poses and restarted timelines snap to their target. Applying a
            // cached pose never reaches this method and cannot advance the filter.
            return !smoothContinuousCalibration || !hasCalibration || !sameClock || elapsed < 0d
                ? 1f
                : (float)(1d - Math.Exp(-elapsed / Mathf.Max(0.001f, continuousSmoothingTime)));
        }

        private static bool IsPositiveFinite(double value) => value > 0d && !double.IsInfinity(value);

        private void RepublishLatestSourcePose()
        {
            if (hasSourceFrame)
            {
                PublishCalibratedFrame(lastSourceFrame);
            }
        }

        private void PublishCalibratedFrame(SkeletonFrame frame)
        {
            if (!TryApply(frame, out var calibratedFrame))
            {
                return;
            }

            latestPose = calibratedFrame;
            hasLatestPose = true;

            if (!publishCalibratedPose)
            {
                return;
            }

            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, calibratedFrame, this);
        }

        private bool IsLastSourceFrame(SkeletonFrame frame)
        {
            return hasSourceFrame && ReferenceEquals(lastSourceFrame.JointPoses, frame.JointPoses) &&
                   lastSourceFrame.Metadata.Equals(frame.Metadata);
        }

        private bool CacheSourceFrame(SkeletonFrame frame)
        {
            var version = stateVersion;
            if (hasSourceFrame &&
                (!string.Equals(lastSourceFrame.Definition.Id, frame.Definition.Id, StringComparison.Ordinal) ||
                 !string.Equals(lastSourceFrame.Metadata.SourceId, frame.Metadata.SourceId, StringComparison.Ordinal) ||
                 lastSourceFrame.CoordinateSpace != frame.CoordinateSpace))
            {
                ClearSourceState();
            }

            if (version != stateVersion) return false;
            lastSourceFrame = frame;
            hasSourceFrame = true;
            return true;
        }

        private void ClearSourceState()
        {
            var changed = hasCalibration || calibrationOffset != Vector3.zero;
            calibrationOffset = Vector3.zero;
            hasCalibration = false;
            initialCalibrationComplete = false;
            hasPoseTime = false;
            lastSourceFrame = default;
            hasSourceFrame = false;
            latestPose = default;
            hasLatestPose = false;
            if (changed) CalibrationChanged?.Invoke();
        }

        private static bool HasValidPoseArrays(
            SkeletonDefinition definition,
            Vector3[] sourcePositions,
            bool[] sourceTracked,
            Vector3[] destinationPositions)
        {
            return sourcePositions != null &&
                   sourceTracked != null &&
                   destinationPositions != null &&
                   sourcePositions.Length >= definition.JointCount &&
                   sourceTracked.Length >= definition.JointCount &&
                   destinationPositions.Length >= definition.JointCount;
        }

        private bool IsUsableProvider(MonoBehaviour component)
        {
            return SkeletonProviderUtility.IsValidProvider(component, this, allowSelf: false);
        }

        private static bool TryGetHipCentre(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out Vector3 hipCentre)
        {
            if (TryGetTrackedJointPosition(definition, positions, tracked, UnityHumanoidControlSkeleton.Hips, out hipCentre))
            {
                return true;
            }

            if (TryGetTrackedMidpoint(
                    definition,
                    positions,
                    tracked,
                    BodyJoints.LeftHip,
                    BodyJoints.RightHip,
                    out hipCentre))
            {
                return true;
            }

            return TryGetTrackedCentre(definition, positions, tracked, out hipCentre);
        }

        private static bool TryGetLowestFootY(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out float lowestFootY)
        {
            lowestFootY = float.PositiveInfinity;

            foreach (var joint in GroundJoints)
            {
                var index = definition.IndexOf(joint);
                if (!definition.IsValidIndex(index) || !tracked[index])
                {
                    continue;
                }

                lowestFootY = Mathf.Min(lowestFootY, positions[index].y);
            }

            return !float.IsPositiveInfinity(lowestFootY) ||
                   TryGetLowestTrackedY(definition, positions, tracked, out lowestFootY);
        }

        private static bool TryGetTrackedJointPosition(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            SkeletonJointId joint,
            out Vector3 position)
        {
            var index = definition.IndexOf(joint);
            if (!definition.IsValidIndex(index) || !tracked[index])
            {
                position = default;
                return false;
            }

            position = positions[index];
            return true;
        }

        private static bool TryGetTrackedMidpoint(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            SkeletonJointId firstJoint,
            SkeletonJointId secondJoint,
            out Vector3 midpoint)
        {
            if (!TryGetTrackedJointPosition(definition, positions, tracked, firstJoint, out var first) ||
                !TryGetTrackedJointPosition(definition, positions, tracked, secondJoint, out var second))
            {
                midpoint = default;
                return false;
            }

            midpoint = (first + second) / 2f;
            return true;
        }

        private static bool TryGetTrackedCentre(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out Vector3 centre)
        {
            centre = Vector3.zero;
            var trackedCount = 0;
            for (int i = 0; i < definition.JointCount; i++)
            {
                if (!tracked[i])
                {
                    continue;
                }

                centre += positions[i];
                trackedCount++;
            }

            if (trackedCount == 0)
            {
                return false;
            }

            centre /= trackedCount;
            return true;
        }

        private static bool TryGetLowestTrackedY(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out float lowestTrackedY)
        {
            lowestTrackedY = float.PositiveInfinity;
            for (int i = 0; i < definition.JointCount; i++)
            {
                if (tracked[i])
                {
                    lowestTrackedY = Mathf.Min(lowestTrackedY, positions[i].y);
                }
            }

            return !float.IsPositiveInfinity(lowestTrackedY);
        }
    }
#pragma warning restore 0649
}
