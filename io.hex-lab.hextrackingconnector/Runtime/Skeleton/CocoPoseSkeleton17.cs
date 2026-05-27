using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class CocoPoseSkeleton17
    {
        public static readonly SkeletonJointId Nose = HumanPoseSkeleton33.Nose;
        public static readonly SkeletonJointId LeftEye = HumanPoseSkeleton33.LeftEye;
        public static readonly SkeletonJointId RightEye = HumanPoseSkeleton33.RightEye;
        public static readonly SkeletonJointId LeftEar = HumanPoseSkeleton33.LeftEar;
        public static readonly SkeletonJointId RightEar = HumanPoseSkeleton33.RightEar;
        public static readonly SkeletonJointId LeftShoulder = HumanPoseSkeleton33.LeftShoulder;
        public static readonly SkeletonJointId RightShoulder = HumanPoseSkeleton33.RightShoulder;
        public static readonly SkeletonJointId LeftElbow = HumanPoseSkeleton33.LeftElbow;
        public static readonly SkeletonJointId RightElbow = HumanPoseSkeleton33.RightElbow;
        public static readonly SkeletonJointId LeftWrist = HumanPoseSkeleton33.LeftWrist;
        public static readonly SkeletonJointId RightWrist = HumanPoseSkeleton33.RightWrist;
        public static readonly SkeletonJointId LeftHip = HumanPoseSkeleton33.LeftHip;
        public static readonly SkeletonJointId RightHip = HumanPoseSkeleton33.RightHip;
        public static readonly SkeletonJointId LeftKnee = HumanPoseSkeleton33.LeftKnee;
        public static readonly SkeletonJointId RightKnee = HumanPoseSkeleton33.RightKnee;
        public static readonly SkeletonJointId LeftAnkle = HumanPoseSkeleton33.LeftAnkle;
        public static readonly SkeletonJointId RightAnkle = HumanPoseSkeleton33.RightAnkle;

        private static readonly SkeletonJointId[] JointList =
        {
            Nose,
            LeftEye,
            RightEye,
            LeftEar,
            RightEar,
            LeftShoulder,
            RightShoulder,
            LeftElbow,
            RightElbow,
            LeftWrist,
            RightWrist,
            LeftHip,
            RightHip,
            LeftKnee,
            RightKnee,
            LeftAnkle,
            RightAnkle,
        };

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(RightAnkle, RightKnee, RightHip),
            new SkeletonLineStrip(LeftAnkle, LeftKnee, LeftHip),
            new SkeletonLineStrip(RightHip, LeftHip, LeftShoulder, RightShoulder, RightHip),
            new SkeletonLineStrip(RightShoulder, RightElbow, RightWrist),
            new SkeletonLineStrip(LeftShoulder, LeftElbow, LeftWrist),
            new SkeletonLineStrip(RightEar, RightEye, Nose, LeftEye, LeftEar),
        };

        public const int JointCount = 17;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "coco.body17",
                "COCO Body 17",
                JointList,
                DebugLineStrips,
                new NoseEarsHeadPoseProvider(Nose, RightEar, LeftEar));

        public static IReadOnlyList<SkeletonJointId> Joints => JointList;

        public static bool TryCreateFrom(SkeletonFrame source, out SkeletonFrame frame)
        {
            if (source.Definition == Definition)
            {
                frame = source;
                return true;
            }

            if (!string.Equals(source.Definition.Id, HumanPoseSkeleton33.Definition.Id, StringComparison.Ordinal))
            {
                frame = default;
                return false;
            }

            var positions = new Vector3[Definition.JointCount];
            var tracked = new bool[Definition.JointCount];

            for (int i = 0; i < JointList.Length; i++)
            {
                if (!source.TryGetJoint(JointList[i], out var position))
                {
                    continue;
                }

                positions[i] = position;
                tracked[i] = true;
            }

            frame = new SkeletonFrame(
                Definition,
                positions,
                tracked,
                source.SequenceNumber,
                source.ReceivedTime);
            return true;
        }
    }
}
