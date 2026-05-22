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
        private static readonly SkeletonJoint[] FootJoints =
        {
            SkeletonJoint.LeftAnkle,
            SkeletonJoint.RightAnkle,
            SkeletonJoint.LeftHeel,
            SkeletonJoint.RightHeel,
            SkeletonJoint.LeftFootIndex,
            SkeletonJoint.RightFootIndex,
        };

        [SerializeField] private BodyDebugVis body;
        [SerializeField] private bool autoCalibrate = true;
        [SerializeField] private BodyCalibrationMode calibrationMode = BodyCalibrationMode.CenterHips;
        [SerializeField] private Vector3 calibrationOffset = Vector3.zero;
        [SerializeField] private float groundHeight = 0f;

        private bool hasCalibration;
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
            if (!TryCalculateOffset(positions, tracked, calibrationMode, groundHeight, out var offset))
            {
                return false;
            }

            calibrationOffset = offset;
            hasCalibration = true;
            return true;
        }

        public void Apply(Vector3[] sourcePositions, bool[] sourceTracked, Vector3[] destinationPositions)
        {
            if (!HasValidPoseArrays(sourcePositions, sourceTracked, destinationPositions))
            {
                return;
            }

            CachePose(sourcePositions, sourceTracked);

            if (autoCalibrate && !hasCalibration)
            {
                Calibrate(sourcePositions, sourceTracked);
            }

            for (int i = 0; i < SkeletonFrame.JointCount; i++)
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
            return TryCalculateOffset(positions, tracked, mode, groundHeight, out var offset)
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
            offset = Vector3.zero;

            if (mode == BodyCalibrationMode.None)
            {
                return true;
            }

            if (positions == null ||
                tracked == null ||
                positions.Length < SkeletonFrame.JointCount ||
                tracked.Length < SkeletonFrame.JointCount ||
                !TryGetHipCentre(positions, tracked, out var hipCentre))
            {
                return false;
            }

            offset = -hipCentre;

            if (mode == BodyCalibrationMode.CenterHipsGroundFeet)
            {
                if (!TryGetLowestFootY(positions, tracked, out var lowestFootY))
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
            return hasPose && Calibrate(lastPositions, lastTracked);
        }

        private void CachePose(Vector3[] positions, bool[] tracked)
        {
            EnsurePoseCache();
            for (int i = 0; i < SkeletonFrame.JointCount; i++)
            {
                lastPositions[i] = positions[i];
                lastTracked[i] = tracked[i];
            }

            hasPose = true;
        }

        private void EnsurePoseCache()
        {
            if (lastPositions == null || lastPositions.Length != SkeletonFrame.JointCount)
            {
                lastPositions = new Vector3[SkeletonFrame.JointCount];
            }

            if (lastTracked == null || lastTracked.Length != SkeletonFrame.JointCount)
            {
                lastTracked = new bool[SkeletonFrame.JointCount];
            }
        }

        private static bool HasValidPoseArrays(
            Vector3[] sourcePositions,
            bool[] sourceTracked,
            Vector3[] destinationPositions)
        {
            return sourcePositions != null &&
                   sourceTracked != null &&
                   destinationPositions != null &&
                   sourcePositions.Length >= SkeletonFrame.JointCount &&
                   sourceTracked.Length >= SkeletonFrame.JointCount &&
                   destinationPositions.Length >= SkeletonFrame.JointCount;
        }

        private static bool TryGetHipCentre(Vector3[] positions, bool[] tracked, out Vector3 hipCentre)
        {
            if (!tracked[(int)SkeletonJoint.LeftHip] || !tracked[(int)SkeletonJoint.RightHip])
            {
                hipCentre = default;
                return false;
            }

            hipCentre = (positions[(int)SkeletonJoint.LeftHip] + positions[(int)SkeletonJoint.RightHip]) / 2f;
            return true;
        }

        private static bool TryGetLowestFootY(Vector3[] positions, bool[] tracked, out float lowestFootY)
        {
            lowestFootY = float.PositiveInfinity;

            foreach (var joint in FootJoints)
            {
                var index = (int)joint;
                if (!tracked[index])
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
