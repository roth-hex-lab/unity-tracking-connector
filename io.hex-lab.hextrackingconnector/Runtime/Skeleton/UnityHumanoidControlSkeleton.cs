using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class UnityHumanoidControlSkeleton
    {
        public static readonly SkeletonJointId Hips = new SkeletonJointId(nameof(HumanBodyBones.Hips));
        public static readonly SkeletonJointId Spine = new SkeletonJointId(nameof(HumanBodyBones.Spine));
        public static readonly SkeletonJointId Chest = new SkeletonJointId(nameof(HumanBodyBones.Chest));
        public static readonly SkeletonJointId UpperChest = new SkeletonJointId(nameof(HumanBodyBones.UpperChest));
        public static readonly SkeletonJointId Neck = new SkeletonJointId(nameof(HumanBodyBones.Neck));
        public static readonly SkeletonJointId Head = new SkeletonJointId(nameof(HumanBodyBones.Head));
        public static readonly SkeletonJointId LeftShoulder = new SkeletonJointId(nameof(HumanBodyBones.LeftShoulder));
        public static readonly SkeletonJointId RightShoulder = new SkeletonJointId(nameof(HumanBodyBones.RightShoulder));
        public static readonly SkeletonJointId LeftUpperArm = new SkeletonJointId(nameof(HumanBodyBones.LeftUpperArm));
        public static readonly SkeletonJointId RightUpperArm = new SkeletonJointId(nameof(HumanBodyBones.RightUpperArm));
        public static readonly SkeletonJointId LeftLowerArm = new SkeletonJointId(nameof(HumanBodyBones.LeftLowerArm));
        public static readonly SkeletonJointId RightLowerArm = new SkeletonJointId(nameof(HumanBodyBones.RightLowerArm));
        public static readonly SkeletonJointId LeftHand = new SkeletonJointId(nameof(HumanBodyBones.LeftHand));
        public static readonly SkeletonJointId RightHand = new SkeletonJointId(nameof(HumanBodyBones.RightHand));
        public static readonly SkeletonJointId LeftUpperLeg = new SkeletonJointId(nameof(HumanBodyBones.LeftUpperLeg));
        public static readonly SkeletonJointId RightUpperLeg = new SkeletonJointId(nameof(HumanBodyBones.RightUpperLeg));
        public static readonly SkeletonJointId LeftLowerLeg = new SkeletonJointId(nameof(HumanBodyBones.LeftLowerLeg));
        public static readonly SkeletonJointId RightLowerLeg = new SkeletonJointId(nameof(HumanBodyBones.RightLowerLeg));
        public static readonly SkeletonJointId LeftFoot = new SkeletonJointId(nameof(HumanBodyBones.LeftFoot));
        public static readonly SkeletonJointId RightFoot = new SkeletonJointId(nameof(HumanBodyBones.RightFoot));
        public static readonly SkeletonJointId LeftToes = new SkeletonJointId(nameof(HumanBodyBones.LeftToes));
        public static readonly SkeletonJointId RightToes = new SkeletonJointId(nameof(HumanBodyBones.RightToes));

        private static readonly SkeletonJointId[] JointList =
        {
            Hips,
            Spine,
            Chest,
            UpperChest,
            Neck,
            Head,
            LeftShoulder,
            RightShoulder,
            LeftUpperArm,
            RightUpperArm,
            LeftLowerArm,
            RightLowerArm,
            LeftHand,
            RightHand,
            LeftUpperLeg,
            RightUpperLeg,
            LeftLowerLeg,
            RightLowerLeg,
            LeftFoot,
            RightFoot,
            LeftToes,
            RightToes,
        };

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(Hips, Spine, Chest, UpperChest, Neck, Head),
            new SkeletonLineStrip(LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand),
            new SkeletonLineStrip(RightShoulder, RightUpperArm, RightLowerArm, RightHand),
            new SkeletonLineStrip(LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes),
            new SkeletonLineStrip(RightUpperLeg, RightLowerLeg, RightFoot, RightToes),
            new SkeletonLineStrip(LeftUpperArm, LeftShoulder, Chest, RightShoulder, RightUpperArm),
            new SkeletonLineStrip(LeftUpperLeg, Hips, RightUpperLeg),
        };

        private static readonly Dictionary<SkeletonJointId, HumanBodyBones> HumanBodyBoneByJoint =
            new Dictionary<SkeletonJointId, HumanBodyBones>
            {
                { Hips, HumanBodyBones.Hips },
                { Spine, HumanBodyBones.Spine },
                { Chest, HumanBodyBones.Chest },
                { UpperChest, HumanBodyBones.UpperChest },
                { Neck, HumanBodyBones.Neck },
                { Head, HumanBodyBones.Head },
                { LeftShoulder, HumanBodyBones.LeftShoulder },
                { RightShoulder, HumanBodyBones.RightShoulder },
                { LeftUpperArm, HumanBodyBones.LeftUpperArm },
                { RightUpperArm, HumanBodyBones.RightUpperArm },
                { LeftLowerArm, HumanBodyBones.LeftLowerArm },
                { RightLowerArm, HumanBodyBones.RightLowerArm },
                { LeftHand, HumanBodyBones.LeftHand },
                { RightHand, HumanBodyBones.RightHand },
                { LeftUpperLeg, HumanBodyBones.LeftUpperLeg },
                { RightUpperLeg, HumanBodyBones.RightUpperLeg },
                { LeftLowerLeg, HumanBodyBones.LeftLowerLeg },
                { RightLowerLeg, HumanBodyBones.RightLowerLeg },
                { LeftFoot, HumanBodyBones.LeftFoot },
                { RightFoot, HumanBodyBones.RightFoot },
                { LeftToes, HumanBodyBones.LeftToes },
                { RightToes, HumanBodyBones.RightToes },
            };

        public const int JointCount = 22;

        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "unity.humanoid.control",
                "Unity Humanoid Control",
                JointList,
                DebugLineStrips);

        public static IReadOnlyList<SkeletonJointId> Joints => JointList;

        public static bool TryGetHumanBodyBone(SkeletonJointId joint, out HumanBodyBones bone)
        {
            return HumanBodyBoneByJoint.TryGetValue(joint, out bone);
        }

        public static bool TryGetJoint(HumanBodyBones bone, out SkeletonJointId joint)
        {
            foreach (var pair in HumanBodyBoneByJoint)
            {
                if (pair.Value != bone)
                {
                    continue;
                }

                joint = pair.Key;
                return true;
            }

            joint = default;
            return false;
        }
    }
}
