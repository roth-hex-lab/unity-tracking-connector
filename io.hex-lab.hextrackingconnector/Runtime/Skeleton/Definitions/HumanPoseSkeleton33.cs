using System.Collections.Generic;

namespace HEXLab.Hextrackingconnector
{
    public static class HumanPoseSkeleton33
    {
        private static readonly SkeletonJointId[] JointList = BodyJoints.CreateHumanPose33JointList();

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(BodyJoints.RightFootIndex, BodyJoints.RightHeel, BodyJoints.RightAnkle, BodyJoints.RightFootIndex),
            new SkeletonLineStrip(BodyJoints.LeftFootIndex, BodyJoints.LeftHeel, BodyJoints.LeftAnkle, BodyJoints.LeftFootIndex),
            new SkeletonLineStrip(BodyJoints.RightAnkle, BodyJoints.RightKnee, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.LeftAnkle, BodyJoints.LeftKnee, BodyJoints.LeftHip),
            new SkeletonLineStrip(BodyJoints.RightHip, BodyJoints.LeftHip, BodyJoints.LeftShoulder, BodyJoints.RightShoulder, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.RightShoulder, BodyJoints.RightElbow, BodyJoints.RightWrist, BodyJoints.RightThumb),
            new SkeletonLineStrip(BodyJoints.LeftShoulder, BodyJoints.LeftElbow, BodyJoints.LeftWrist, BodyJoints.LeftThumb),
            new SkeletonLineStrip(BodyJoints.RightWrist, BodyJoints.RightPinky, BodyJoints.RightIndex, BodyJoints.RightWrist),
            new SkeletonLineStrip(BodyJoints.LeftWrist, BodyJoints.LeftPinky, BodyJoints.LeftIndex, BodyJoints.LeftWrist),
            new SkeletonLineStrip(BodyJoints.MouthRight, BodyJoints.MouthLeft),
            new SkeletonLineStrip(BodyJoints.RightEar, BodyJoints.RightEye, BodyJoints.Nose, BodyJoints.LeftEye, BodyJoints.LeftEar),
        };

        public const int JointCount = 33;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "humanpose.33",
                "HumanPoseSkeleton33",
                JointList,
                DebugLineStrips,
                new NoseEarsHeadPoseProvider(BodyJoints.Nose, BodyJoints.RightEar, BodyJoints.LeftEar),
                BodyJoints.CreateHumanPose33MirrorPairs());

        public static IReadOnlyList<SkeletonJointId> Joints => JointList;
    }
}
