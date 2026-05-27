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
    }

#pragma warning disable 0649
    public class BodyCalibration : MonoBehaviour, ISkeletonProvider
    {
        private static readonly SkeletonJointId[] GroundJoints =
        {
            HumanPoseSkeleton33.LeftAnkle,
            HumanPoseSkeleton33.RightAnkle,
            HumanPoseSkeleton33.LeftHeel,
            HumanPoseSkeleton33.RightHeel,
            HumanPoseSkeleton33.LeftFootIndex,
            HumanPoseSkeleton33.RightFootIndex,
            UnityHumanoidControlSkeleton.LeftFoot,
            UnityHumanoidControlSkeleton.RightFoot,
            UnityHumanoidControlSkeleton.LeftToes,
            UnityHumanoidControlSkeleton.RightToes,
        };

        [Header("Source")]
        [SerializeField, FormerlySerializedAs("body"), SkeletonProvider(allowSelf: false)] private MonoBehaviour skeletonProvider;
        [SerializeField] private bool publishCalibratedPose = true;

        [Header("Calibration")]
        [SerializeField] private bool autoCalibrate = true;
        [SerializeField] private BodyCalibrationMode calibrationMode = BodyCalibrationMode.CenterHips;
        [SerializeField] private Vector3 calibrationOffset = Vector3.zero;
        [SerializeField] private float groundHeight = 0f;

        private ISkeletonProvider activeSkeletonProvider;
        private bool hasCalibration;
        private SkeletonDefinition lastDefinition;
        private Vector3[] lastPositions;
        private bool[] lastTracked;
        private bool hasPose;
        private SkeletonFrame lastSourceFrame;
        private bool hasSourceFrame;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public bool AutoCalibrate => autoCalibrate;
        public bool HasCalibration => hasCalibration;
        public BodyCalibrationMode CalibrationMode => calibrationMode;
        public float GroundHeight => groundHeight;
        public Vector3 CalibrationOffset => calibrationOffset;
        public bool PublishCalibratedPose => publishCalibratedPose;

        public event Action<SkeletonFrame> PoseReceived;
        public event Action CalibrationChanged;

        private void OnEnable()
        {
            ResolveSkeletonProvider();
            Subscribe();
            TryCaptureProviderPose();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Calibrate()
        {
            if (!CalibrateLastPose())
            {
                Debug.LogWarning("BodyCalibration could not calibrate because no usable source pose is available yet.", this);
                return;
            }

            RepublishLatestSourcePose();
        }

        public void ResetCalibration()
        {
            calibrationOffset = Vector3.zero;
            hasCalibration = false;
            CalibrationChanged?.Invoke();
            RepublishLatestSourcePose();
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool Calibrate(Vector3[] positions, bool[] tracked)
        {
            return Calibrate(HumanPoseSkeleton33.Definition, positions, tracked);
        }

        public bool Calibrate(SkeletonDefinition definition, Vector3[] positions, bool[] tracked)
        {
            definition = definition ?? HumanPoseSkeleton33.Definition;
            if (!TryCalculateOffset(definition, positions, tracked, calibrationMode, groundHeight, out var offset))
            {
                return false;
            }

            calibrationOffset = offset;
            hasCalibration = true;
            CalibrationChanged?.Invoke();
            return true;
        }

        public bool Calibrate(SkeletonFrame frame)
        {
            if (!TryCalculateOffset(
                    frame.Definition,
                    frame.CopyPositions(),
                    frame.CopyTracked(),
                    calibrationMode,
                    groundHeight,
                    out var offset))
            {
                return false;
            }

            calibrationOffset = offset;
            hasCalibration = true;
            CalibrationChanged?.Invoke();
            return true;
        }

        public bool TryApply(SkeletonFrame sourceFrame, out SkeletonFrame calibratedFrame)
        {
            if (sourceFrame.Definition == null)
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

            CachePose(definition, sourcePositions, sourceTracked);

            if (autoCalibrate && !hasCalibration)
            {
                Calibrate(definition, sourcePositions, sourceTracked);
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
                tracked.Length < definition.JointCount ||
                !TryGetHipCentre(definition, positions, tracked, out var hipCentre))
            {
                return false;
            }

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

        private bool CalibrateLastPose()
        {
            TryCaptureProviderPose();

            if (hasSourceFrame && Calibrate(lastSourceFrame))
            {
                return true;
            }

            return hasPose && Calibrate(lastDefinition, lastPositions, lastTracked);
        }

        private void Subscribe()
        {
            if (activeSkeletonProvider != null)
            {
                activeSkeletonProvider.PoseReceived += OnSourcePoseReceived;
            }
        }

        private void Unsubscribe()
        {
            if (activeSkeletonProvider != null)
            {
                activeSkeletonProvider.PoseReceived -= OnSourcePoseReceived;
            }

            activeSkeletonProvider = null;
        }

        private void OnSourcePoseReceived(SkeletonFrame frame)
        {
            CachePose(frame);

            if (autoCalibrate && !hasCalibration)
            {
                Calibrate(frame);
            }

            PublishCalibratedFrame(frame);
        }

        private void TryCaptureProviderPose()
        {
            if (activeSkeletonProvider != null && activeSkeletonProvider.TryGetLatestPose(out var frame))
            {
                CachePose(frame);
            }
        }

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

            var handlers = PoseReceived;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SkeletonFrame> handler in handlers.GetInvocationList())
            {
                handler(calibratedFrame);
            }
        }

        private void CachePose(SkeletonFrame frame)
        {
            lastSourceFrame = frame;
            hasSourceFrame = true;
            CachePose(frame.Definition, frame.CopyPositions(), frame.CopyTracked());
        }

        private void CachePose(SkeletonDefinition definition, Vector3[] positions, bool[] tracked)
        {
            EnsurePoseCache(definition);
            for (int i = 0; i < definition.JointCount; i++)
            {
                lastPositions[i] = positions[i];
                lastTracked[i] = tracked[i];
            }

            lastDefinition = definition;
            hasPose = true;
        }

        private void EnsurePoseCache(SkeletonDefinition definition)
        {
            if (lastPositions == null || lastPositions.Length != definition.JointCount)
            {
                lastPositions = new Vector3[definition.JointCount];
            }

            if (lastTracked == null || lastTracked.Length != definition.JointCount)
            {
                lastTracked = new bool[definition.JointCount];
            }
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
                    HumanPoseSkeleton33.LeftHip,
                    HumanPoseSkeleton33.RightHip,
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
