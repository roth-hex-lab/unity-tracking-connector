using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class CocoPoseSkeleton17
    {
        private static readonly SkeletonJointId[] JointList =
        {
            BodyJoints.Nose,
            BodyJoints.LeftEye,
            BodyJoints.RightEye,
            BodyJoints.LeftEar,
            BodyJoints.RightEar,
            BodyJoints.LeftShoulder,
            BodyJoints.RightShoulder,
            BodyJoints.LeftElbow,
            BodyJoints.RightElbow,
            BodyJoints.LeftWrist,
            BodyJoints.RightWrist,
            BodyJoints.LeftHip,
            BodyJoints.RightHip,
            BodyJoints.LeftKnee,
            BodyJoints.RightKnee,
            BodyJoints.LeftAnkle,
            BodyJoints.RightAnkle,
        };

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(BodyJoints.RightAnkle, BodyJoints.RightKnee, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.LeftAnkle, BodyJoints.LeftKnee, BodyJoints.LeftHip),
            new SkeletonLineStrip(BodyJoints.RightHip, BodyJoints.LeftHip, BodyJoints.LeftShoulder, BodyJoints.RightShoulder, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.RightShoulder, BodyJoints.RightElbow, BodyJoints.RightWrist),
            new SkeletonLineStrip(BodyJoints.LeftShoulder, BodyJoints.LeftElbow, BodyJoints.LeftWrist),
            new SkeletonLineStrip(BodyJoints.RightEar, BodyJoints.RightEye, BodyJoints.Nose, BodyJoints.LeftEye, BodyJoints.LeftEar),
        };

        public const int JointCount = 17;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "coco.body17",
                "COCO Body 17",
                JointList,
                DebugLineStrips,
                new NoseEarsHeadPoseProvider(BodyJoints.Nose, BodyJoints.RightEar, BodyJoints.LeftEar),
                BodyJoints.CreateCocoPose17MirrorPairs());

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

            var poses = new SkeletonJointPose[Definition.JointCount];

            for (int i = 0; i < JointList.Length; i++)
            {
                if (!source.TryGetJointPose(JointList[i], out var pose))
                {
                    poses[i] = SkeletonJointPose.Unavailable;
                    continue;
                }

                poses[i] = pose;
            }

            frame = new SkeletonFrame(
                new SkeletonPose(Definition, poses, source.CoordinateSpace),
                source.Metadata);
            return true;
        }
    }
}
