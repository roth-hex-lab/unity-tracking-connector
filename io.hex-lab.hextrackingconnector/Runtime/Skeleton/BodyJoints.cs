namespace HEXLab.Hextrackingconnector
{
    public static class BodyJoints
    {
        public static readonly SkeletonJointId Nose = new SkeletonJointId(nameof(Nose));
        public static readonly SkeletonJointId LeftEyeInner = new SkeletonJointId(nameof(LeftEyeInner));
        public static readonly SkeletonJointId LeftEye = new SkeletonJointId(nameof(LeftEye));
        public static readonly SkeletonJointId LeftEyeOuter = new SkeletonJointId(nameof(LeftEyeOuter));
        public static readonly SkeletonJointId RightEyeInner = new SkeletonJointId(nameof(RightEyeInner));
        public static readonly SkeletonJointId RightEye = new SkeletonJointId(nameof(RightEye));
        public static readonly SkeletonJointId RightEyeOuter = new SkeletonJointId(nameof(RightEyeOuter));
        public static readonly SkeletonJointId LeftEar = new SkeletonJointId(nameof(LeftEar));
        public static readonly SkeletonJointId RightEar = new SkeletonJointId(nameof(RightEar));
        public static readonly SkeletonJointId MouthLeft = new SkeletonJointId(nameof(MouthLeft));
        public static readonly SkeletonJointId MouthRight = new SkeletonJointId(nameof(MouthRight));
        public static readonly SkeletonJointId LeftShoulder = new SkeletonJointId(nameof(LeftShoulder));
        public static readonly SkeletonJointId RightShoulder = new SkeletonJointId(nameof(RightShoulder));
        public static readonly SkeletonJointId LeftElbow = new SkeletonJointId(nameof(LeftElbow));
        public static readonly SkeletonJointId RightElbow = new SkeletonJointId(nameof(RightElbow));
        public static readonly SkeletonJointId LeftWrist = new SkeletonJointId(nameof(LeftWrist));
        public static readonly SkeletonJointId RightWrist = new SkeletonJointId(nameof(RightWrist));
        public static readonly SkeletonJointId LeftPinky = new SkeletonJointId(nameof(LeftPinky));
        public static readonly SkeletonJointId RightPinky = new SkeletonJointId(nameof(RightPinky));
        public static readonly SkeletonJointId LeftIndex = new SkeletonJointId(nameof(LeftIndex));
        public static readonly SkeletonJointId RightIndex = new SkeletonJointId(nameof(RightIndex));
        public static readonly SkeletonJointId LeftThumb = new SkeletonJointId(nameof(LeftThumb));
        public static readonly SkeletonJointId RightThumb = new SkeletonJointId(nameof(RightThumb));
        public static readonly SkeletonJointId LeftHip = new SkeletonJointId(nameof(LeftHip));
        public static readonly SkeletonJointId RightHip = new SkeletonJointId(nameof(RightHip));
        public static readonly SkeletonJointId LeftKnee = new SkeletonJointId(nameof(LeftKnee));
        public static readonly SkeletonJointId RightKnee = new SkeletonJointId(nameof(RightKnee));
        public static readonly SkeletonJointId LeftAnkle = new SkeletonJointId(nameof(LeftAnkle));
        public static readonly SkeletonJointId RightAnkle = new SkeletonJointId(nameof(RightAnkle));
        public static readonly SkeletonJointId LeftHeel = new SkeletonJointId(nameof(LeftHeel));
        public static readonly SkeletonJointId RightHeel = new SkeletonJointId(nameof(RightHeel));
        public static readonly SkeletonJointId LeftFootIndex = new SkeletonJointId(nameof(LeftFootIndex));
        public static readonly SkeletonJointId RightFootIndex = new SkeletonJointId(nameof(RightFootIndex));

        internal static SkeletonJointId[] CreateHumanPose33JointList()
        {
            return new[]
            {
                Nose,
                LeftEyeInner,
                LeftEye,
                LeftEyeOuter,
                RightEyeInner,
                RightEye,
                RightEyeOuter,
                LeftEar,
                RightEar,
                MouthLeft,
                MouthRight,
                LeftShoulder,
                RightShoulder,
                LeftElbow,
                RightElbow,
                LeftWrist,
                RightWrist,
                LeftPinky,
                RightPinky,
                LeftIndex,
                RightIndex,
                LeftThumb,
                RightThumb,
                LeftHip,
                RightHip,
                LeftKnee,
                RightKnee,
                LeftAnkle,
                RightAnkle,
                LeftHeel,
                RightHeel,
                LeftFootIndex,
                RightFootIndex,
            };
        }

        internal static SkeletonJointPair[] CreateHumanPose33MirrorPairs()
        {
            return new[]
            {
                new SkeletonJointPair(LeftEyeInner, RightEyeInner),
                new SkeletonJointPair(LeftEye, RightEye),
                new SkeletonJointPair(LeftEyeOuter, RightEyeOuter),
                new SkeletonJointPair(LeftEar, RightEar),
                new SkeletonJointPair(MouthLeft, MouthRight),
                new SkeletonJointPair(LeftShoulder, RightShoulder),
                new SkeletonJointPair(LeftElbow, RightElbow),
                new SkeletonJointPair(LeftWrist, RightWrist),
                new SkeletonJointPair(LeftPinky, RightPinky),
                new SkeletonJointPair(LeftIndex, RightIndex),
                new SkeletonJointPair(LeftThumb, RightThumb),
                new SkeletonJointPair(LeftHip, RightHip),
                new SkeletonJointPair(LeftKnee, RightKnee),
                new SkeletonJointPair(LeftAnkle, RightAnkle),
                new SkeletonJointPair(LeftHeel, RightHeel),
                new SkeletonJointPair(LeftFootIndex, RightFootIndex),
            };
        }

        internal static SkeletonJointPair[] CreateCocoPose17MirrorPairs()
        {
            return new[]
            {
                new SkeletonJointPair(LeftEye, RightEye),
                new SkeletonJointPair(LeftEar, RightEar),
                new SkeletonJointPair(LeftShoulder, RightShoulder),
                new SkeletonJointPair(LeftElbow, RightElbow),
                new SkeletonJointPair(LeftWrist, RightWrist),
                new SkeletonJointPair(LeftHip, RightHip),
                new SkeletonJointPair(LeftKnee, RightKnee),
                new SkeletonJointPair(LeftAnkle, RightAnkle),
            };
        }
    }
}
