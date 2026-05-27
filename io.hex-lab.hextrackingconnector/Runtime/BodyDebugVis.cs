using UnityEngine;
using UnityEngine.Serialization;

namespace HEXLab.Hextrackingconnector
{
#pragma warning disable 0649
    public class BodyDebugVis : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField, FormerlySerializedAs("commServer"), SkeletonProvider] private MonoBehaviour skeletonProvider;
        [SerializeField] private bool applyCalibration = true;
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

        private SkeletonDefinition currentDefinition = SkeletonDefinition.Empty;
        private Vector3[] currentPositions = new Vector3[0];
        private Vector3[] displayPositions = new Vector3[0];
        private bool[] tracked = new bool[0];
        private GameObject[] jointInstances = new GameObject[0];
        private LineRenderer[] lines = new LineRenderer[0];

        private ISkeletonProvider activeSkeletonProvider;
        private GameObject headInstance;
        private bool visualsCreated;

        public Vector3 CalibrationOffset => calibration == null ? Vector3.zero : calibration.CalibrationOffset;
        public Vector3 VirtualHeadPosition { get; private set; }
        public bool UsesLocalOneShotCalibration
        {
            get
            {
                var candidate = calibration == null ? GetComponent<BodyCalibration>() : calibration;
                return applyCalibration &&
                       candidate != null &&
                       candidate.gameObject == gameObject &&
                       !ReferenceEquals(skeletonProvider, candidate);
            }
        }

        private void OnEnable()
        {
            ResolveSkeletonProvider();
            Subscribe();
            SubscribeCalibration();
            EnsureVisuals(HumanPoseSkeleton33.Definition);
        }

        private void OnDisable()
        {
            UnsubscribeCalibration();
            Unsubscribe();
        }

        private void OnDestroy()
        {
            DestroyVisuals();
        }

        public void ResetCalibration()
        {
            if (applyCalibration)
            {
                calibration?.ResetCalibration();
            }

            ApplyPose();
        }

        public void ApplyCurrentPose()
        {
            ApplyCalibration();
            ApplyPose();
        }

        private void ResolveSkeletonProvider()
        {
            if (skeletonProvider == null)
            {
                skeletonProvider = FindFirstObjectByType<CommServer>();
            }

            if (applyCalibration && calibration == null)
            {
                calibration = GetComponent<BodyCalibration>();
            }

            activeSkeletonProvider = null;
            if (skeletonProvider != null)
            {
                SkeletonProviderUtility.TryResolveProvider(
                    skeletonProvider,
                    this,
                    "Skeleton Provider",
                    allowSelf: true,
                    out activeSkeletonProvider);
            }
        }

        private void Subscribe()
        {
            if (activeSkeletonProvider != null)
            {
                activeSkeletonProvider.PoseReceived += OnPoseReceived;
            }
        }

        private void Unsubscribe()
        {
            if (activeSkeletonProvider != null)
            {
                activeSkeletonProvider.PoseReceived -= OnPoseReceived;
            }

            activeSkeletonProvider = null;
        }

        private void SubscribeCalibration()
        {
            if (calibration != null)
            {
                calibration.CalibrationChanged += OnCalibrationChanged;
            }
        }

        private void UnsubscribeCalibration()
        {
            if (calibration != null)
            {
                calibration.CalibrationChanged -= OnCalibrationChanged;
            }
        }

        private void OnCalibrationChanged()
        {
            if (ShouldApplyOneShotCalibration)
            {
                ApplyCurrentPose();
            }
        }

        private void OnPoseReceived(SkeletonFrame frame)
        {
            EnsureVisuals(frame.Definition);

            if (!visualsCreated)
            {
                return;
            }

            EnsurePoseBuffers(frame.Definition);
            for (int i = 0; i < frame.Definition.JointCount; i++)
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
            if (ShouldApplyOneShotCalibration)
            {
                calibration.Apply(currentDefinition, currentPositions, tracked, displayPositions);
                return;
            }

            for (int i = 0; i < currentDefinition.JointCount; i++)
            {
                displayPositions[i] = tracked[i] ? currentPositions[i] : Vector3.zero;
            }
        }

        private bool ShouldApplyOneShotCalibration =>
            applyCalibration &&
            calibration != null &&
            !ReferenceEquals(activeSkeletonProvider, calibration);

        private void ApplyPose()
        {
            for (int i = 0; i < jointInstances.Length; i++)
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
            return enableHead &&
                   headInstance != null &&
                   currentDefinition.IsValidIndex(index) &&
                   IsFacialJoint(currentDefinition.JointAt(index));
        }

        private void EnsureVisuals(SkeletonDefinition definition)
        {
            definition = definition ?? HumanPoseSkeleton33.Definition;
            if (visualsCreated && string.Equals(currentDefinition.Id, definition.Id, System.StringComparison.Ordinal))
            {
                return;
            }

            DestroyVisuals();

            if (jointPrefab == null || linePrefab == null)
            {
                Debug.LogWarning("BodyDebugVis needs joint and line prefabs.", this);
                currentDefinition = definition;
                EnsurePoseBuffers(definition);
                return;
            }

            currentDefinition = definition;
            EnsurePoseBuffers(definition);

            var targetParent = parent == null ? transform : parent;
            var generatedJointMaterial = BodyDebugMaterials.GetOrCreateJointMaterial(jointColor);

            jointInstances = new GameObject[definition.JointCount];
            for (int i = 0; i < jointInstances.Length; i++)
            {
                jointInstances[i] = Instantiate(jointPrefab, targetParent);
                ApplyJointMaterial(jointInstances[i], generatedJointMaterial);

                jointInstances[i].transform.localScale = Vector3.one * jointScale;
                jointInstances[i].name = definition.JointAt(i).ToString();
                jointInstances[i].SetActive(false);
            }

            lines = new LineRenderer[definition.DebugLineStrips.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                var lineObject = Instantiate(linePrefab, targetParent);
                lineObject.transform.localPosition = Vector3.zero;
                lineObject.transform.localRotation = Quaternion.identity;
                lineObject.transform.localScale = Vector3.one;

                lines[i] = lineObject.GetComponent<LineRenderer>();
                if (lines[i] != null)
                {
                    lines[i].useWorldSpace = false;
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

        private void EnsurePoseBuffers(SkeletonDefinition definition)
        {
            var jointCount = definition.JointCount;
            if (currentPositions.Length != jointCount)
            {
                currentPositions = new Vector3[jointCount];
            }

            if (displayPositions.Length != jointCount)
            {
                displayPositions = new Vector3[jointCount];
            }

            if (tracked.Length != jointCount)
            {
                tracked = new bool[jointCount];
            }
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
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null)
                {
                    DestroyIfPresent(lines[i].gameObject);
                }
            }

            DestroyIfPresent(headInstance);
            headInstance = null;
            jointInstances = new GameObject[0];
            lines = new LineRenderer[0];
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
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null)
                {
                    continue;
                }

                var strip = currentDefinition.DebugLineStrips[i];
                if (headInstance != null && IsFacialLineStrip(strip))
                {
                    lines[i].positionCount = 0;
                    continue;
                }

                if (!HasTrackedJoints(strip))
                {
                    lines[i].positionCount = 0;
                    continue;
                }

                lines[i].positionCount = strip.Count;
                lines[i].widthMultiplier = connectionScale;
                lines[i].useWorldSpace = false;
                for (int pointIndex = 0; pointIndex < strip.Count; pointIndex++)
                {
                    lines[i].SetPosition(pointIndex, LocalPosition(strip[pointIndex]));
                }
            }
        }

        private bool HasTrackedJoints(SkeletonLineStrip strip)
        {
            foreach (var joint in strip.Joints)
            {
                var index = currentDefinition.IndexOf(joint);
                if (!currentDefinition.IsValidIndex(index) || !tracked[index])
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

            if (!currentDefinition.TryGetHeadPose(displayPositions, tracked, out var headPose))
            {
                headInstance.SetActive(false);
                return;
            }

            VirtualHeadPosition = headPose.Position;
            headInstance.SetActive(true);
            headInstance.transform.localPosition = VirtualHeadPosition;
            headInstance.transform.localScale = Vector3.one * headScale;
            headInstance.transform.localRotation = Quaternion.LookRotation(-headPose.Forward, headPose.Up);
        }

        private Vector3 LocalPosition(SkeletonJointId joint)
        {
            var index = currentDefinition.IndexOf(joint);
            return currentDefinition.IsValidIndex(index)
                ? displayPositions[index]
                : Vector3.zero;
        }

        private static bool IsFacialLineStrip(SkeletonLineStrip strip)
        {
            foreach (var joint in strip.Joints)
            {
                if (!IsFacialJoint(joint))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFacialJoint(SkeletonJointId joint)
        {
            return joint == HumanPoseSkeleton33.Nose ||
                   joint == HumanPoseSkeleton33.LeftEyeInner ||
                   joint == HumanPoseSkeleton33.LeftEye ||
                   joint == HumanPoseSkeleton33.LeftEyeOuter ||
                   joint == HumanPoseSkeleton33.RightEyeInner ||
                   joint == HumanPoseSkeleton33.RightEye ||
                   joint == HumanPoseSkeleton33.RightEyeOuter ||
                   joint == HumanPoseSkeleton33.LeftEar ||
                   joint == HumanPoseSkeleton33.RightEar ||
                   joint == HumanPoseSkeleton33.MouthLeft ||
                   joint == HumanPoseSkeleton33.MouthRight;
        }
    }
#pragma warning restore 0649
}
