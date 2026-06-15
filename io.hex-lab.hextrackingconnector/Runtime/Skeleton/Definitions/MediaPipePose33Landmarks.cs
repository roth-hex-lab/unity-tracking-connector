namespace HEXLab.Hextrackingconnector
{
    public enum MediaPipePose33Landmark
    {
        Nose = 0,
        LeftEyeInner = 1,
        LeftEye = 2,
        LeftEyeOuter = 3,
        RightEyeInner = 4,
        RightEye = 5,
        RightEyeOuter = 6,
        LeftEar = 7,
        RightEar = 8,
        MouthLeft = 9,
        MouthRight = 10,
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftPinky = 17,
        RightPinky = 18,
        LeftIndex = 19,
        RightIndex = 20,
        LeftThumb = 21,
        RightThumb = 22,
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
        LeftHeel = 29,
        RightHeel = 30,
        LeftFootIndex = 31,
        RightFootIndex = 32,
    }

    public static class MediaPipePose33Landmarks
    {
        public const int LandmarkCount = HumanPoseSkeleton33.JointCount;

        public static bool TryGetJoint(int landmarkIndex, out SkeletonJointId joint)
        {
            if (landmarkIndex < 0 || landmarkIndex >= LandmarkCount)
            {
                joint = default;
                return false;
            }

            joint = ToSkeletonJoint((MediaPipePose33Landmark)landmarkIndex);
            return true;
        }

        public static SkeletonJointId ToSkeletonJoint(MediaPipePose33Landmark landmark)
        {
            switch (landmark)
            {
                case MediaPipePose33Landmark.Nose: return BodyJoints.Nose;
                case MediaPipePose33Landmark.LeftEyeInner: return BodyJoints.LeftEyeInner;
                case MediaPipePose33Landmark.LeftEye: return BodyJoints.LeftEye;
                case MediaPipePose33Landmark.LeftEyeOuter: return BodyJoints.LeftEyeOuter;
                case MediaPipePose33Landmark.RightEyeInner: return BodyJoints.RightEyeInner;
                case MediaPipePose33Landmark.RightEye: return BodyJoints.RightEye;
                case MediaPipePose33Landmark.RightEyeOuter: return BodyJoints.RightEyeOuter;
                case MediaPipePose33Landmark.LeftEar: return BodyJoints.LeftEar;
                case MediaPipePose33Landmark.RightEar: return BodyJoints.RightEar;
                case MediaPipePose33Landmark.MouthLeft: return BodyJoints.MouthLeft;
                case MediaPipePose33Landmark.MouthRight: return BodyJoints.MouthRight;
                case MediaPipePose33Landmark.LeftShoulder: return BodyJoints.LeftShoulder;
                case MediaPipePose33Landmark.RightShoulder: return BodyJoints.RightShoulder;
                case MediaPipePose33Landmark.LeftElbow: return BodyJoints.LeftElbow;
                case MediaPipePose33Landmark.RightElbow: return BodyJoints.RightElbow;
                case MediaPipePose33Landmark.LeftWrist: return BodyJoints.LeftWrist;
                case MediaPipePose33Landmark.RightWrist: return BodyJoints.RightWrist;
                case MediaPipePose33Landmark.LeftPinky: return BodyJoints.LeftPinky;
                case MediaPipePose33Landmark.RightPinky: return BodyJoints.RightPinky;
                case MediaPipePose33Landmark.LeftIndex: return BodyJoints.LeftIndex;
                case MediaPipePose33Landmark.RightIndex: return BodyJoints.RightIndex;
                case MediaPipePose33Landmark.LeftThumb: return BodyJoints.LeftThumb;
                case MediaPipePose33Landmark.RightThumb: return BodyJoints.RightThumb;
                case MediaPipePose33Landmark.LeftHip: return BodyJoints.LeftHip;
                case MediaPipePose33Landmark.RightHip: return BodyJoints.RightHip;
                case MediaPipePose33Landmark.LeftKnee: return BodyJoints.LeftKnee;
                case MediaPipePose33Landmark.RightKnee: return BodyJoints.RightKnee;
                case MediaPipePose33Landmark.LeftAnkle: return BodyJoints.LeftAnkle;
                case MediaPipePose33Landmark.RightAnkle: return BodyJoints.RightAnkle;
                case MediaPipePose33Landmark.LeftHeel: return BodyJoints.LeftHeel;
                case MediaPipePose33Landmark.RightHeel: return BodyJoints.RightHeel;
                case MediaPipePose33Landmark.LeftFootIndex: return BodyJoints.LeftFootIndex;
                case MediaPipePose33Landmark.RightFootIndex: return BodyJoints.RightFootIndex;
                default: return default;
            }
        }
    }
}
