using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
#pragma warning disable 0649
    public class DirectHumanoidBoneDriver : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private MonoBehaviour skeletonProvider;
        [SerializeField] private bool retargetSourcePose = true;
        [SerializeField] private bool logUnsupportedPoses = true;

        [Header("Avatar")]
        [SerializeField] private Animator animator;
        [SerializeField] private bool applyRootPosition = true;
        [SerializeField, Min(0.001f)] private float positionScale = 1f;

        private readonly List<BoneBinding> bindings = new List<BoneBinding>();

        private ISkeletonProvider activeSkeletonProvider;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;
        private Vector3 initialRootLocalPosition;
        private string lastUnsupportedDefinitionId;

        private struct BoneBinding
        {
            public SkeletonJointId Joint;
            public HumanBodyBones Bone;
            public Transform Transform;
            public Quaternion RestWorldRotation;
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
            if (skeletonProvider != null && !(skeletonProvider is ISkeletonProvider))
            {
                skeletonProvider = null;
            }

            positionScale = Mathf.Max(0.001f, positionScale);
        }

        public void ApplyCurrentPose()
        {
            if (hasLatestPose)
            {
                ApplyPose(latestPose);
            }
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

            activeSkeletonProvider = skeletonProvider as ISkeletonProvider;
            initialRootLocalPosition = animator != null
                ? animator.transform.localPosition
                : transform.localPosition;
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
            if (applyRootPosition &&
                frame.TryGetJoint(UnityHumanoidControlSkeleton.Hips, out var hipsPosition))
            {
                root.localPosition = initialRootLocalPosition + hipsPosition * positionScale;
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

                var targetRootRotation = targetPose.Rotation;
                if (!binding.HasRestDirection)
                {
                    binding.Transform.rotation = root.rotation * targetRootRotation;
                    continue;
                }

                var targetRootDirection = targetRootRotation * Vector3.up;
                if (targetRootDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                var restWorldDirection = root.TransformDirection(binding.RestRootDirection);
                var targetWorldDirection = root.TransformDirection(targetRootDirection.normalized);
                binding.Transform.rotation =
                    Quaternion.FromToRotation(restWorldDirection, targetWorldDirection) *
                    binding.RestWorldRotation;
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
                    RestWorldRotation = boneTransform.rotation,
                };

                if (TryGetChildJoint(joint, out var childJoint) &&
                    UnityHumanoidControlSkeleton.TryGetHumanBodyBone(childJoint, out var childBone))
                {
                    var childTransform = animator.GetBoneTransform(childBone);
                    if (childTransform != null)
                    {
                        var direction = childTransform.position - boneTransform.position;
                        if (direction.sqrMagnitude > 0.0001f)
                        {
                            binding.RestRootDirection = root.InverseTransformDirection(direction).normalized;
                            binding.HasRestDirection = true;
                        }
                    }
                }

                bindings.Add(binding);
            }
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

        private static bool TryGetChildJoint(SkeletonJointId joint, out SkeletonJointId child)
        {
            if (joint == UnityHumanoidControlSkeleton.Hips)
            {
                child = UnityHumanoidControlSkeleton.Spine;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.Spine)
            {
                child = UnityHumanoidControlSkeleton.Chest;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.Chest)
            {
                child = UnityHumanoidControlSkeleton.UpperChest;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.UpperChest)
            {
                child = UnityHumanoidControlSkeleton.Neck;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.Neck)
            {
                child = UnityHumanoidControlSkeleton.Head;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.LeftUpperArm)
            {
                child = UnityHumanoidControlSkeleton.LeftLowerArm;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.LeftLowerArm)
            {
                child = UnityHumanoidControlSkeleton.LeftHand;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.RightUpperArm)
            {
                child = UnityHumanoidControlSkeleton.RightLowerArm;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.RightLowerArm)
            {
                child = UnityHumanoidControlSkeleton.RightHand;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.LeftUpperLeg)
            {
                child = UnityHumanoidControlSkeleton.LeftLowerLeg;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.LeftLowerLeg)
            {
                child = UnityHumanoidControlSkeleton.LeftFoot;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.LeftFoot)
            {
                child = UnityHumanoidControlSkeleton.LeftToes;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.RightUpperLeg)
            {
                child = UnityHumanoidControlSkeleton.RightLowerLeg;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.RightLowerLeg)
            {
                child = UnityHumanoidControlSkeleton.RightFoot;
                return true;
            }

            if (joint == UnityHumanoidControlSkeleton.RightFoot)
            {
                child = UnityHumanoidControlSkeleton.RightToes;
                return true;
            }

            child = default;
            return false;
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
