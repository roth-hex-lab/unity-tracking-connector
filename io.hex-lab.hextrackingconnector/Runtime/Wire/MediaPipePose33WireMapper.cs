namespace HEXLab.Hextrackingconnector
{
    internal enum MediaPipePoseLandmark
    {
        NOSE = 0,
        LEFT_EYE_INNER = 1,
        LEFT_EYE = 2,
        LEFT_EYE_OUTER = 3,
        RIGHT_EYE_INNER = 4,
        RIGHT_EYE = 5,
        RIGHT_EYE_OUTER = 6,
        LEFT_EAR = 7,
        RIGHT_EAR = 8,
        MOUTH_LEFT = 9,
        MOUTH_RIGHT = 10,
        LEFT_SHOULDER = 11,
        RIGHT_SHOULDER = 12,
        LEFT_ELBOW = 13,
        RIGHT_ELBOW = 14,
        LEFT_WRIST = 15,
        RIGHT_WRIST = 16,
        LEFT_PINKY = 17,
        RIGHT_PINKY = 18,
        LEFT_INDEX = 19,
        RIGHT_INDEX = 20,
        LEFT_THUMB = 21,
        RIGHT_THUMB = 22,
        LEFT_HIP = 23,
        RIGHT_HIP = 24,
        LEFT_KNEE = 25,
        RIGHT_KNEE = 26,
        LEFT_ANKLE = 27,
        RIGHT_ANKLE = 28,
        LEFT_HEEL = 29,
        RIGHT_HEEL = 30,
        LEFT_FOOT_INDEX = 31,
        RIGHT_FOOT_INDEX = 32,
    }

    internal sealed class MediaPipePose33WireMapper : IWireSkeletonMapper
    {
        public const int LandmarkCount = HumanPoseSkeleton33.JointCount;

        public SkeletonDefinition Definition => HumanPoseSkeleton33.Definition;

        public bool TryMapIndex(
            int sourceIndex,
            out SkeletonJointId joint)
        {
            if (!IsValidIndex(sourceIndex))
            {
                joint = default;
                return false;
            }

            joint = ToSkeletonJoint((MediaPipePoseLandmark)sourceIndex);
            return true;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < LandmarkCount;
        }

        private SkeletonJointId ToSkeletonJoint(MediaPipePoseLandmark landmark)
        {
            switch (landmark)
            {
                case MediaPipePoseLandmark.NOSE: return BodyJoints.Nose;
                case MediaPipePoseLandmark.LEFT_EYE_INNER: return BodyJoints.LeftEyeInner;
                case MediaPipePoseLandmark.LEFT_EYE: return BodyJoints.LeftEye;
                case MediaPipePoseLandmark.LEFT_EYE_OUTER: return BodyJoints.LeftEyeOuter;
                case MediaPipePoseLandmark.RIGHT_EYE_INNER: return BodyJoints.RightEyeInner;
                case MediaPipePoseLandmark.RIGHT_EYE: return BodyJoints.RightEye;
                case MediaPipePoseLandmark.RIGHT_EYE_OUTER: return BodyJoints.RightEyeOuter;
                case MediaPipePoseLandmark.LEFT_EAR: return BodyJoints.LeftEar;
                case MediaPipePoseLandmark.RIGHT_EAR: return BodyJoints.RightEar;
                case MediaPipePoseLandmark.MOUTH_LEFT: return BodyJoints.MouthLeft;
                case MediaPipePoseLandmark.MOUTH_RIGHT: return BodyJoints.MouthRight;
                case MediaPipePoseLandmark.LEFT_SHOULDER: return BodyJoints.LeftShoulder;
                case MediaPipePoseLandmark.RIGHT_SHOULDER: return BodyJoints.RightShoulder;
                case MediaPipePoseLandmark.LEFT_ELBOW: return BodyJoints.LeftElbow;
                case MediaPipePoseLandmark.RIGHT_ELBOW: return BodyJoints.RightElbow;
                case MediaPipePoseLandmark.LEFT_WRIST: return BodyJoints.LeftWrist;
                case MediaPipePoseLandmark.RIGHT_WRIST: return BodyJoints.RightWrist;
                case MediaPipePoseLandmark.LEFT_PINKY: return BodyJoints.LeftPinky;
                case MediaPipePoseLandmark.RIGHT_PINKY: return BodyJoints.RightPinky;
                case MediaPipePoseLandmark.LEFT_INDEX: return BodyJoints.LeftIndex;
                case MediaPipePoseLandmark.RIGHT_INDEX: return BodyJoints.RightIndex;
                case MediaPipePoseLandmark.LEFT_THUMB: return BodyJoints.LeftThumb;
                case MediaPipePoseLandmark.RIGHT_THUMB: return BodyJoints.RightThumb;
                case MediaPipePoseLandmark.LEFT_HIP: return BodyJoints.LeftHip;
                case MediaPipePoseLandmark.RIGHT_HIP: return BodyJoints.RightHip;
                case MediaPipePoseLandmark.LEFT_KNEE: return BodyJoints.LeftKnee;
                case MediaPipePoseLandmark.RIGHT_KNEE: return BodyJoints.RightKnee;
                case MediaPipePoseLandmark.LEFT_ANKLE: return BodyJoints.LeftAnkle;
                case MediaPipePoseLandmark.RIGHT_ANKLE: return BodyJoints.RightAnkle;
                case MediaPipePoseLandmark.LEFT_HEEL: return BodyJoints.LeftHeel;
                case MediaPipePoseLandmark.RIGHT_HEEL: return BodyJoints.RightHeel;
                case MediaPipePoseLandmark.LEFT_FOOT_INDEX: return BodyJoints.LeftFootIndex;
                case MediaPipePoseLandmark.RIGHT_FOOT_INDEX: return BodyJoints.RightFootIndex;
                default: return default;
            }
        }
    }
}
