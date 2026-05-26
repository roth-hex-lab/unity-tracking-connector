using UnityEngine;
using UnityEngine.Serialization;

namespace HEXLab.Hextrackingconnector
{
#pragma warning disable 0649
    public class BodyDebugVis : MonoBehaviour
    {
        private const int LineCount = 11;

        private static readonly SkeletonJoint[][] LineStrips =
        {
            new[] { SkeletonJoint.RightFootIndex, SkeletonJoint.RightHeel, SkeletonJoint.RightAnkle, SkeletonJoint.RightFootIndex },
            new[] { SkeletonJoint.LeftFootIndex, SkeletonJoint.LeftHeel, SkeletonJoint.LeftAnkle, SkeletonJoint.LeftFootIndex },
            new[] { SkeletonJoint.RightAnkle, SkeletonJoint.RightKnee, SkeletonJoint.RightHip },
            new[] { SkeletonJoint.LeftAnkle, SkeletonJoint.LeftKnee, SkeletonJoint.LeftHip },
            new[] { SkeletonJoint.RightHip, SkeletonJoint.LeftHip, SkeletonJoint.LeftShoulder, SkeletonJoint.RightShoulder, SkeletonJoint.RightHip },
            new[] { SkeletonJoint.RightShoulder, SkeletonJoint.RightElbow, SkeletonJoint.RightWrist, SkeletonJoint.RightThumb },
            new[] { SkeletonJoint.LeftShoulder, SkeletonJoint.LeftElbow, SkeletonJoint.LeftWrist, SkeletonJoint.LeftThumb },
            new[] { SkeletonJoint.RightWrist, SkeletonJoint.RightPinky, SkeletonJoint.RightIndex, SkeletonJoint.RightWrist },
            new[] { SkeletonJoint.LeftWrist, SkeletonJoint.LeftPinky, SkeletonJoint.LeftIndex, SkeletonJoint.LeftWrist },
            new[] { SkeletonJoint.MouthRight, SkeletonJoint.MouthLeft },
            new[] { SkeletonJoint.RightEar, SkeletonJoint.RightEye, SkeletonJoint.Nose, SkeletonJoint.LeftEye, SkeletonJoint.LeftEar },
        };

        [Header("Source")]
        [SerializeField] private CommServer commServer;
        [SerializeField] private BodyCalibration calibration;

        [Header("Prefabs")]
        [SerializeField] private Transform parent;
        [SerializeField, FormerlySerializedAs("landmarkPrefab")] private GameObject jointPrefab;
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private GameObject headPrefab;
        [SerializeField] private bool enableHead = true;
        [SerializeField] private Color jointColor = new Color(0.34f, 0.34f, 0.34f, 1f);

        [Header("Scale")]
        [SerializeField, Min(0.001f)] private float bodyScale = 1f;
        [SerializeField, FormerlySerializedAs("landmarkScale"), Min(0.001f)] private float jointScale = 0.12f;
        [SerializeField, Min(0.001f)] private float headScale = 0.28f;
        [SerializeField, Min(0.001f)] private float connectionScale = 0.04f;

        private readonly Vector3[] currentPositions = new Vector3[SkeletonFrame.JointCount];
        private readonly Vector3[] displayPositions = new Vector3[SkeletonFrame.JointCount];
        private readonly bool[] tracked = new bool[SkeletonFrame.JointCount];
        private readonly GameObject[] jointInstances = new GameObject[SkeletonFrame.JointCount];
        private readonly LineRenderer[] lines = new LineRenderer[LineCount];

        private GameObject headInstance;
        private bool visualsCreated;

        public Vector3 CalibrationOffset => calibration == null ? Vector3.zero : calibration.CalibrationOffset;
        public Vector3 VirtualHeadPosition { get; private set; }

        private void OnEnable()
        {
            ResolveCommServer();
            Subscribe();
            EnsureVisuals();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            DestroyVisuals();
        }

        public void ResetCalibration()
        {
            calibration?.ResetCalibration();
            ApplyPose();
        }

        public void ApplyCurrentPose()
        {
            ApplyCalibration();
            ApplyPose();
        }

        private void ResolveCommServer()
        {
            if (commServer == null)
            {
                commServer = FindFirstObjectByType<CommServer>();
            }

            if (calibration == null)
            {
                calibration = GetComponent<BodyCalibration>();
            }
        }

        private void Subscribe()
        {
            if (commServer != null)
            {
                commServer.PoseReceived += OnPoseReceived;
            }
        }

        private void Unsubscribe()
        {
            if (commServer != null)
            {
                commServer.PoseReceived -= OnPoseReceived;
            }
        }

        private void OnPoseReceived(SkeletonFrame frame)
        {
            EnsureVisuals();

            if (!visualsCreated)
            {
                return;
            }

            for (int i = 0; i < SkeletonFrame.JointCount; i++)
            {
                if (frame.TryGetJoint(i, out var position))
                {
                    currentPositions[i] = position * bodyScale;
                    tracked[i] = true;
                }
                else
                {
                    currentPositions[i] = Vector3.zero;
                    tracked[i] = false;
                }
            }

            ApplyCurrentPose();
        }

        private void ApplyCalibration()
        {
            if (calibration != null)
            {
                calibration.Apply(currentPositions, tracked, displayPositions);
                return;
            }

            for (int i = 0; i < SkeletonFrame.JointCount; i++)
            {
                displayPositions[i] = tracked[i] ? currentPositions[i] : Vector3.zero;
            }
        }

        private void ApplyPose()
        {
            for (int i = 0; i < SkeletonFrame.JointCount; i++)
            {
                if (jointInstances[i] == null)
                {
                    continue;
                }

                jointInstances[i].SetActive(tracked[i]);
                jointInstances[i].transform.localPosition = displayPositions[i];
                jointInstances[i].transform.localScale = ShouldHideFacialJoint(i)
                    ? Vector3.zero
                    : Vector3.one * jointScale;
            }

            UpdateLines();
            UpdateHead();
        }

        private bool ShouldHideFacialJoint(int index)
        {
            return enableHead && headInstance != null && index <= (int)SkeletonJoint.MouthRight;
        }

        private void EnsureVisuals()
        {
            if (visualsCreated)
            {
                return;
            }

            if (jointPrefab == null || linePrefab == null)
            {
                Debug.LogWarning("BodyDebugVis needs joint and line prefabs.", this);
                return;
            }

            var targetParent = parent == null ? transform : parent;
            var generatedJointMaterial = BodyDebugMaterials.GetOrCreateJointMaterial(jointColor);

            for (int i = 0; i < jointInstances.Length; i++)
            {
                jointInstances[i] = Instantiate(jointPrefab, targetParent);
                ApplyJointMaterial(jointInstances[i], generatedJointMaterial);

                jointInstances[i].transform.localScale = Vector3.one * jointScale;
                jointInstances[i].name = ((SkeletonJoint)i).ToString();
                jointInstances[i].SetActive(false);
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var lineObject = Instantiate(linePrefab, targetParent);
                lines[i] = lineObject.GetComponent<LineRenderer>();
                if (lines[i] != null)
                {
                    lines[i].positionCount = 0;
                    lines[i].widthMultiplier = connectionScale;
                }
            }

            if (enableHead && headPrefab != null)
            {
                headInstance = Instantiate(headPrefab, targetParent);
                ApplyJointMaterial(headInstance, generatedJointMaterial);

                headInstance.transform.localPosition = headPrefab.transform.localPosition;
                headInstance.transform.localRotation = headPrefab.transform.localRotation;
                headInstance.transform.localScale = Vector3.one * headScale;
                headInstance.SetActive(false);
            }

            visualsCreated = true;
        }

        private static void ApplyJointMaterial(GameObject jointInstance, Material material)
        {
            if (jointInstance == null || material == null)
            {
                return;
            }

            // Keep child renderers free to use their own prefab materials.
            var renderers = jointInstance.GetComponents<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer is LineRenderer)
                {
                    continue;
                }

                renderer.sharedMaterial = material;
            }
        }

        private void DestroyVisuals()
        {
            for (int i = 0; i < jointInstances.Length; i++)
            {
                DestroyIfPresent(jointInstances[i]);
                jointInstances[i] = null;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null)
                {
                    DestroyIfPresent(lines[i].gameObject);
                    lines[i] = null;
                }
            }

            DestroyIfPresent(headInstance);
            headInstance = null;
            visualsCreated = false;
        }

        private static void DestroyIfPresent(Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private void UpdateLines()
        {
            for (int i = 0; i < LineStrips.Length; i++)
            {
                if (lines[i] == null)
                {
                    continue;
                }

                if (headInstance != null && i >= 9)
                {
                    lines[i].positionCount = 0;
                    continue;
                }

                var strip = LineStrips[i];
                if (!HasTrackedJoints(strip))
                {
                    lines[i].positionCount = 0;
                    continue;
                }

                lines[i].positionCount = strip.Length;
                lines[i].widthMultiplier = connectionScale;
                for (int pointIndex = 0; pointIndex < strip.Length; pointIndex++)
                {
                    lines[i].SetPosition(pointIndex, Position(strip[pointIndex]));
                }
            }
        }

        private bool HasTrackedJoints(SkeletonJoint[] joints)
        {
            foreach (var joint in joints)
            {
                if (!tracked[(int)joint])
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateHead()
        {
            if (headInstance == null)
            {
                return;
            }

            if (!TryGetPosition(SkeletonJoint.RightEar, out var rightEar) ||
                !TryGetPosition(SkeletonJoint.LeftEar, out var leftEar) ||
                !TryGetPosition(SkeletonJoint.Nose, out var nose) ||
                !TryGetPosition(SkeletonJoint.RightEyeInner, out var rightEyeInner) ||
                !TryGetPosition(SkeletonJoint.LeftEyeInner, out var leftEyeInner))
            {
                headInstance.SetActive(false);
                return;
            }

            VirtualHeadPosition = (rightEar + leftEar) / 2f;
            headInstance.SetActive(true);
            headInstance.transform.position = VirtualHeadPosition;
            headInstance.transform.localScale = Vector3.one * headScale;

            var up = Vector3.Scale(
                new Vector3(.1f, 1f, .1f),
                BodyMath.GetNormal(nose, rightEar, leftEar)).normalized;
            var forward = Vector3.Scale(
                new Vector3(1f, .1f, 1f),
                BodyMath.GetNormal(nose, rightEyeInner, leftEyeInner)).normalized;

            if (up.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
            {
                headInstance.transform.rotation = Quaternion.LookRotation(-forward, up);
            }
        }

        private bool TryGetPosition(SkeletonJoint joint, out Vector3 position)
        {
            var index = (int)joint;
            if (!tracked[index] || jointInstances[index] == null)
            {
                position = default;
                return false;
            }

            position = jointInstances[index].transform.position;
            return true;
        }

        private Vector3 Position(SkeletonJoint joint)
        {
            return jointInstances[(int)joint].transform.position;
        }
    }
#pragma warning restore 0649
}
