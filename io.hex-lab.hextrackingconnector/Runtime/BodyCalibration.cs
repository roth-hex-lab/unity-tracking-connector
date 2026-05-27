using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum BodyCalibrationMode
    {
        None,
        CenterHips,
        CenterHipsGroundFeet,
    }

#pragma warning disable 0649
    public class BodyCalibration : MonoBehaviour
    {
        private static readonly SkeletonJointId[] FootJoints =
        {
            HumanPoseSkeleton33.LeftAnkle,
            HumanPoseSkeleton33.RightAnkle,
            HumanPoseSkeleton33.LeftHeel,
            HumanPoseSkeleton33.RightHeel,
            HumanPoseSkeleton33.LeftFootIndex,
            HumanPoseSkeleton33.RightFootIndex,
        };

        [SerializeField] private BodyDebugVis body;
        [SerializeField] private bool autoCalibrate = true;
        [SerializeField] private BodyCalibrationMode calibrationMode = BodyCalibrationMode.CenterHips;
        [SerializeField] private Vector3 calibrationOffset = Vector3.zero;
        [SerializeField] private float groundHeight = 0f;

        private bool hasCalibration;
        private SkeletonDefinition lastDefinition;
        private Vector3[] lastPositions;
        private bool[] lastTracked;
        private bool hasPose;

        public bool AutoCalibrate => autoCalibrate;
        public bool HasCalibration => hasCalibration;
        public BodyCalibrationMode CalibrationMode => calibrationMode;
        public float GroundHeight => groundHeight;
        public Vector3 CalibrationOffset => calibrationOffset;

        private void Awake()
        {
            ResolveBody();
        }

        private void OnValidate()
        {
            ResolveBody();
        }

        public void Calibrate()
        {
            if (!CalibrateLastPose())
            {
                Debug.LogWarning("BodyCalibration could not calibrate because BodyDebugVis has no usable pose yet.", this);
                return;
            }

            ResolveBody();
            body?.ApplyCurrentPose();
        }

        public void ResetCalibration()
        {
            calibrationOffset = Vector3.zero;
            hasCalibration = false;
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

        private void ResolveBody()
        {
            if (body == null)
            {
                body = GetComponent<BodyDebugVis>();
            }
        }

        private bool CalibrateLastPose()
        {
            return hasPose && Calibrate(lastDefinition, lastPositions, lastTracked);
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

        private static bool TryGetHipCentre(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out Vector3 hipCentre)
        {
            var leftHip = definition.IndexOf(HumanPoseSkeleton33.LeftHip);
            var rightHip = definition.IndexOf(HumanPoseSkeleton33.RightHip);
            if (!definition.IsValidIndex(leftHip) ||
                !definition.IsValidIndex(rightHip) ||
                !tracked[leftHip] ||
                !tracked[rightHip])
            {
                hipCentre = default;
                return false;
            }

            hipCentre = (positions[leftHip] + positions[rightHip]) / 2f;
            return true;
        }

        private static bool TryGetLowestFootY(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            out float lowestFootY)
        {
            lowestFootY = float.PositiveInfinity;

            foreach (var joint in FootJoints)
            {
                var index = definition.IndexOf(joint);
                if (!definition.IsValidIndex(index) || !tracked[index])
                {
                    continue;
                }

                lowestFootY = Mathf.Min(lowestFootY, positions[index].y);
            }

            return !float.IsPositiveInfinity(lowestFootY);
        }
    }
#pragma warning restore 0649
}
