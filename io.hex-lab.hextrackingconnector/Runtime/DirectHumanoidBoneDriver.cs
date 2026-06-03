using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum AvatarFitMode
    {
        Off,
        FitOnce,
        Continuous,
    }

#pragma warning disable 0649
    [SkeletonPipelineNode("DirectHumanoidBoneDriver")]
    public class DirectHumanoidBoneDriver : MonoBehaviour
    {
        private static readonly SkeletonJointId[] AvatarFitFootJoints =
        {
            UnityHumanoidControlSkeleton.LeftToes,
            UnityHumanoidControlSkeleton.RightToes,
            UnityHumanoidControlSkeleton.LeftFoot,
            UnityHumanoidControlSkeleton.RightFoot,
        };

        private static readonly HumanBodyBones[] AvatarFitFootBones =
        {
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };

        [Header("Source")]
        [SerializeField, SkeletonProvider] private MonoBehaviour skeletonProvider;
        [SerializeField] private bool retargetSourcePose = true;
        [SerializeField] private bool logUnsupportedPoses = true;

        [Header("Avatar")]
        [SerializeField] private Animator animator;
        [SerializeField] private bool applyRootPosition = true;
        [SerializeField, Min(0.001f)] private float positionScale = 1f;

        [Header("Avatar Fit")]
        [SerializeField] private AvatarFitMode avatarFitMode = AvatarFitMode.Off;
        [SerializeField, Min(0.001f)] private float minAvatarFitScale = 0.25f;
        [SerializeField, Min(0.001f)] private float maxAvatarFitScale = 4f;
        [Tooltip("Current effective uniform scale applied by Avatar Fit. Edit in Play Mode or set AvatarFitScale from code to override it.")]
        [SerializeField, Min(0.001f)] private float avatarFitScale = 1f;

        private readonly List<BoneBinding> bindings = new List<BoneBinding>();

        private ISkeletonProvider activeSkeletonProvider;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;
        private Vector3 initialRootLocalPosition;
        private Quaternion initialRootLocalRotation;
        private Vector3 initialRootLocalScale = Vector3.one;
        private Vector3 restHipsRootPosition;
        private bool hasRestHipsRootPosition;
        private float restAvatarHeight;
        private bool hasRestAvatarHeight;
        private bool hasAvatarFit;
        private string lastUnsupportedDefinitionId;

        private struct BoneBinding
        {
            public SkeletonJointId Joint;
            public HumanBodyBones Bone;
            public Transform Transform;
            public Quaternion RestRootRotation;
            public Vector3 RestRootDirection;
            public bool HasRestDirection;
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheBindings();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (hasLatestPose)
            {
                ApplyPose(latestPose);
            }
        }

        private void OnValidate()
        {
            positionScale = Mathf.Max(0.001f, positionScale);
            minAvatarFitScale = Mathf.Max(0.001f, minAvatarFitScale);
            maxAvatarFitScale = Mathf.Max(minAvatarFitScale, maxAvatarFitScale);
            avatarFitScale = ClampAvatarFitScale(avatarFitScale, minAvatarFitScale, maxAvatarFitScale);

            if (Application.isPlaying && hasAvatarFit)
            {
                ApplyAvatarLocalScale();
            }
        }

        public void ApplyCurrentPose()
        {
            if (hasLatestPose)
            {
                ApplyPose(latestPose);
            }
        }

        public float AvatarFitScale
        {
            get => avatarFitScale;
            set => SetAvatarFitScale(value);
        }

        public bool HasAvatarFit => hasAvatarFit;

        public void SetAvatarFitScale(float scale)
        {
            avatarFitScale = ClampAvatarFitScale(scale, minAvatarFitScale, maxAvatarFitScale);
            hasAvatarFit = true;
            ApplyAvatarLocalScale();
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (skeletonProvider == null)
            {
                skeletonProvider = FindFirstObjectByType<SkeletonConverter>();
            }

            if (skeletonProvider == null)
            {
                skeletonProvider = FindFirstObjectByType<CommServer>();
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
            initialRootLocalPosition = animator != null
                ? animator.transform.localPosition
                : transform.localPosition;
            initialRootLocalRotation = animator != null
                ? animator.transform.localRotation
                : transform.localRotation;
            initialRootLocalScale = animator != null
                ? animator.transform.localScale
                : transform.localScale;
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

        private void OnPoseReceived(SkeletonFrame frame)
        {
            if (!TryGetHumanoidPose(frame, out var humanoidFrame))
            {
                LogUnsupportedPose(frame.Definition);
                return;
            }

            latestPose = humanoidFrame;
            hasLatestPose = true;
        }

        private bool TryGetHumanoidPose(SkeletonFrame frame, out SkeletonFrame humanoidFrame)
        {
            if (string.Equals(frame.Definition.Id, UnityHumanoidControlSkeleton.Definition.Id, StringComparison.Ordinal))
            {
                humanoidFrame = frame;
                return true;
            }

            if (retargetSourcePose)
            {
                return UnityHumanoidPoseRetargeter.TryCreateFrom(frame, out humanoidFrame);
            }

            humanoidFrame = default;
            return false;
        }

        private void ApplyPose(SkeletonFrame frame)
        {
            if (animator == null)
            {
                return;
            }

            var root = animator.transform;
            ApplyAvatarFit(frame, root);

            if (applyRootPosition &&
                frame.TryGetJoint(UnityHumanoidControlSkeleton.Hips, out var hipsPosition))
            {
                root.localPosition = CalculateRootLocalPosition(
                    initialRootLocalPosition,
                    initialRootLocalRotation,
                    initialRootLocalScale,
                    hasRestHipsRootPosition ? restHipsRootPosition : Vector3.zero,
                    hipsPosition,
                    positionScale,
                    hasAvatarFit ? avatarFitScale : 1f);
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.Transform == null ||
                    !frame.TryGetJointPose(binding.Joint, out var targetPose) ||
                    !ShouldDriveRotation(targetPose))
                {
                    continue;
                }

                binding.Transform.rotation = CalculateBoneWorldRotation(
                    root.rotation,
                    binding.RestRootRotation,
                    binding.RestRootDirection,
                    binding.HasRestDirection,
                    targetPose.Rotation);
            }
        }

        private void CacheBindings()
        {
            bindings.Clear();
            if (animator == null)
            {
                return;
            }

            var root = animator.transform;
            var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
            hasRestHipsRootPosition = hipsTransform != null;
            restHipsRootPosition = hasRestHipsRootPosition
                ? root.InverseTransformPoint(hipsTransform.position)
                : Vector3.zero;
            hasRestAvatarHeight = TryCalculateRestAvatarHeight(root, out restAvatarHeight);
            hasAvatarFit = false;
            avatarFitScale = 1f;

            foreach (var joint in UnityHumanoidControlSkeleton.Joints)
            {
                if (!UnityHumanoidControlSkeleton.TryGetHumanBodyBone(joint, out var bone))
                {
                    continue;
                }

                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null)
                {
                    continue;
                }

                var binding = new BoneBinding
                {
                    Joint = joint,
                    Bone = bone,
                    Transform = boneTransform,
                    RestRootRotation = Quaternion.Inverse(root.rotation) * boneTransform.rotation,
                };

                if (TryGetRestDirection(root, boneTransform, joint, out var restRootDirection))
                {
                    binding.RestRootDirection = restRootDirection;
                    binding.HasRestDirection = true;
                }

                bindings.Add(binding);
            }
        }

        public void ResetAvatarFit()
        {
            hasAvatarFit = false;
            avatarFitScale = 1f;
            ApplyAvatarLocalScale();
        }

        private void ApplyAvatarLocalScale()
        {
            if (animator == null)
            {
                return;
            }

            animator.transform.localScale = initialRootLocalScale * avatarFitScale;
        }

        private void ApplyAvatarFit(SkeletonFrame frame, Transform root)
        {
            if (avatarFitMode == AvatarFitMode.Off)
            {
                if (hasAvatarFit)
                {
                    ResetAvatarFit();
                }

                return;
            }

            if (!hasRestAvatarHeight)
            {
                return;
            }

            if (avatarFitMode == AvatarFitMode.FitOnce && hasAvatarFit)
            {
                ApplyAvatarLocalScale();
                return;
            }

            if (!TryCalculateAvatarFitScale(
                    frame,
                    restAvatarHeight,
                    positionScale,
                    minAvatarFitScale,
                    maxAvatarFitScale,
                    out var calculatedScale))
            {
                return;
            }

            avatarFitScale = calculatedScale;
            hasAvatarFit = true;
            ApplyAvatarLocalScale();
        }

        private bool TryCalculateRestAvatarHeight(Transform root, out float height)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null || !TryGetLowestRestFootY(root, out var lowestFootY))
            {
                height = 0f;
                return false;
            }

            var localHeight = root.InverseTransformPoint(head.position).y - lowestFootY;
            height = localHeight * Mathf.Abs(initialRootLocalScale.y);
            return height > 0.0001f;
        }

        private bool TryGetLowestRestFootY(Transform root, out float lowestFootY)
        {
            lowestFootY = float.PositiveInfinity;
            var foundFoot = false;

            for (int i = 0; i < AvatarFitFootBones.Length; i++)
            {
                var foot = animator.GetBoneTransform(AvatarFitFootBones[i]);
                if (foot == null)
                {
                    continue;
                }

                lowestFootY = Mathf.Min(lowestFootY, root.InverseTransformPoint(foot.position).y);
                foundFoot = true;
            }

            if (foundFoot)
            {
                return true;
            }

            lowestFootY = 0f;
            return false;
        }

        private void LogUnsupportedPose(SkeletonDefinition definition)
        {
            if (!logUnsupportedPoses || definition == null)
            {
                return;
            }

            if (string.Equals(lastUnsupportedDefinitionId, definition.Id, StringComparison.Ordinal))
            {
                return;
            }

            lastUnsupportedDefinitionId = definition.Id;
            Debug.LogWarning(
                $"DirectHumanoidBoneDriver cannot use skeleton definition '{definition.Name}'.",
                this);
        }

        private bool TryGetRestDirection(
            Transform root,
            Transform boneTransform,
            SkeletonJointId joint,
            out Vector3 restRootDirection)
        {
            foreach (var childJoint in GetChildJointCandidates(joint))
            {
                if (!UnityHumanoidControlSkeleton.TryGetHumanBodyBone(childJoint, out var childBone))
                {
                    continue;
                }

                var childTransform = animator.GetBoneTransform(childBone);
                if (childTransform == null)
                {
                    continue;
                }

                var direction = childTransform.position - boneTransform.position;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                restRootDirection = root.InverseTransformDirection(direction).normalized;
                return true;
            }

            restRootDirection = default;
            return false;
        }

        private static IEnumerable<SkeletonJointId> GetChildJointCandidates(SkeletonJointId joint)
        {
            if (joint == UnityHumanoidControlSkeleton.Hips)
            {
                yield return UnityHumanoidControlSkeleton.Spine;
                yield return UnityHumanoidControlSkeleton.Chest;
                yield return UnityHumanoidControlSkeleton.UpperChest;
                yield return UnityHumanoidControlSkeleton.Neck;
                yield return UnityHumanoidControlSkeleton.Head;
            }
            else if (joint == UnityHumanoidControlSkeleton.Spine)
            {
                yield return UnityHumanoidControlSkeleton.Chest;
                yield return UnityHumanoidControlSkeleton.UpperChest;
                yield return UnityHumanoidControlSkeleton.Neck;
                yield return UnityHumanoidControlSkeleton.Head;
            }
            else if (joint == UnityHumanoidControlSkeleton.Chest)
            {
                yield return UnityHumanoidControlSkeleton.UpperChest;
                yield return UnityHumanoidControlSkeleton.Neck;
                yield return UnityHumanoidControlSkeleton.Head;
            }
            else if (joint == UnityHumanoidControlSkeleton.UpperChest)
            {
                yield return UnityHumanoidControlSkeleton.Neck;
                yield return UnityHumanoidControlSkeleton.Head;
            }
            else if (joint == UnityHumanoidControlSkeleton.Neck)
            {
                yield return UnityHumanoidControlSkeleton.Head;
            }
            else if (joint == UnityHumanoidControlSkeleton.LeftUpperArm)
            {
                yield return UnityHumanoidControlSkeleton.LeftLowerArm;
                yield return UnityHumanoidControlSkeleton.LeftHand;
            }
            else if (joint == UnityHumanoidControlSkeleton.LeftLowerArm)
            {
                yield return UnityHumanoidControlSkeleton.LeftHand;
            }
            else if (joint == UnityHumanoidControlSkeleton.RightUpperArm)
            {
                yield return UnityHumanoidControlSkeleton.RightLowerArm;
                yield return UnityHumanoidControlSkeleton.RightHand;
            }
            else if (joint == UnityHumanoidControlSkeleton.RightLowerArm)
            {
                yield return UnityHumanoidControlSkeleton.RightHand;
            }
            else if (joint == UnityHumanoidControlSkeleton.LeftUpperLeg)
            {
                yield return UnityHumanoidControlSkeleton.LeftLowerLeg;
                yield return UnityHumanoidControlSkeleton.LeftFoot;
                yield return UnityHumanoidControlSkeleton.LeftToes;
            }
            else if (joint == UnityHumanoidControlSkeleton.LeftLowerLeg)
            {
                yield return UnityHumanoidControlSkeleton.LeftFoot;
                yield return UnityHumanoidControlSkeleton.LeftToes;
            }
            else if (joint == UnityHumanoidControlSkeleton.LeftFoot)
            {
                yield return UnityHumanoidControlSkeleton.LeftToes;
            }
            else if (joint == UnityHumanoidControlSkeleton.RightUpperLeg)
            {
                yield return UnityHumanoidControlSkeleton.RightLowerLeg;
                yield return UnityHumanoidControlSkeleton.RightFoot;
                yield return UnityHumanoidControlSkeleton.RightToes;
            }
            else if (joint == UnityHumanoidControlSkeleton.RightLowerLeg)
            {
                yield return UnityHumanoidControlSkeleton.RightFoot;
                yield return UnityHumanoidControlSkeleton.RightToes;
            }
            else if (joint == UnityHumanoidControlSkeleton.RightFoot)
            {
                yield return UnityHumanoidControlSkeleton.RightToes;
            }
        }

        private static Vector3 CalculateRootLocalPosition(
            Vector3 initialRootLocalPosition,
            Quaternion initialRootLocalRotation,
            Vector3 initialRootLocalScale,
            Vector3 restHipsRootPosition,
            Vector3 targetHipsPosition,
            float positionScale,
            float avatarScale)
        {
            var scaledRestHipsPosition = Vector3.Scale(
                restHipsRootPosition,
                initialRootLocalScale * avatarScale);
            return initialRootLocalPosition +
                   initialRootLocalRotation *
                   ((targetHipsPosition * positionScale) - scaledRestHipsPosition);
        }

        private static bool TryCalculateAvatarFitScale(
            SkeletonFrame frame,
            float restAvatarHeight,
            float positionScale,
            float minScale,
            float maxScale,
            out float fitScale)
        {
            fitScale = 1f;

            if (restAvatarHeight <= 0.0001f ||
                positionScale <= 0.0001f ||
                !TryGetSkeletonHeight(frame, out var skeletonHeight))
            {
                return false;
            }

            var calculatedScale = skeletonHeight * positionScale / restAvatarHeight;
            if (calculatedScale <= 0.0001f ||
                float.IsNaN(calculatedScale) ||
                float.IsInfinity(calculatedScale))
            {
                return false;
            }

            var lowerBound = Mathf.Min(minScale, maxScale);
            var upperBound = Mathf.Max(minScale, maxScale);
            fitScale = Mathf.Clamp(calculatedScale, lowerBound, upperBound);
            return true;
        }

        private static float ClampAvatarFitScale(float scale, float minScale, float maxScale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale))
            {
                return 1f;
            }

            var lowerBound = Mathf.Min(minScale, maxScale);
            var upperBound = Mathf.Max(minScale, maxScale);
            return Mathf.Clamp(Mathf.Max(0.001f, scale), lowerBound, upperBound);
        }

        private static bool TryGetSkeletonHeight(SkeletonFrame frame, out float height)
        {
            height = 0f;

            if (!frame.TryGetJoint(UnityHumanoidControlSkeleton.Head, out var head) ||
                !TryGetLowestSkeletonFootY(frame, out var lowestFootY))
            {
                return false;
            }

            height = head.y - lowestFootY;
            return height > 0.0001f;
        }

        private static bool TryGetLowestSkeletonFootY(SkeletonFrame frame, out float lowestFootY)
        {
            lowestFootY = float.PositiveInfinity;
            var foundFoot = false;

            for (int i = 0; i < AvatarFitFootJoints.Length; i++)
            {
                if (!frame.TryGetJoint(AvatarFitFootJoints[i], out var foot))
                {
                    continue;
                }

                lowestFootY = Mathf.Min(lowestFootY, foot.y);
                foundFoot = true;
            }

            if (foundFoot)
            {
                return true;
            }

            lowestFootY = 0f;
            return false;
        }

        private static Quaternion CalculateBoneWorldRotation(
            Quaternion rootRotation,
            Quaternion restRootRotation,
            Vector3 restRootDirection,
            bool hasRestDirection,
            Quaternion targetRootRotation)
        {
            if (!hasRestDirection)
            {
                return rootRotation * targetRootRotation;
            }

            var targetRootDirection = targetRootRotation * Vector3.up;
            if (targetRootDirection.sqrMagnitude <= 0.0001f ||
                restRootDirection.sqrMagnitude <= 0.0001f)
            {
                return rootRotation * restRootRotation;
            }

            var rootDelta = Quaternion.FromToRotation(
                restRootDirection.normalized,
                targetRootDirection.normalized);
            return rootRotation * rootDelta * restRootRotation;
        }

        private static bool ShouldDriveRotation(SkeletonJointPose pose)
        {
            if (!pose.HasRotation || pose.Provenance == SkeletonDataProvenance.Rest)
            {
                return false;
            }

            return !pose.HasConfidence || pose.Confidence > 0.0001f;
        }
    }
#pragma warning restore 0649
}
