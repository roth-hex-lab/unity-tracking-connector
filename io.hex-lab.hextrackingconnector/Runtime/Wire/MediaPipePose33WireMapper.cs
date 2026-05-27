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
            PoseMirrorMode mirrorMode,
            out SkeletonJointId joint)
        {
            if (!IsValidIndex(sourceIndex))
            {
                joint = default;
                return false;
            }

            var mappedLandmark = Map((MediaPipePoseLandmark)sourceIndex, mirrorMode);
            joint = ToSkeletonJoint(mappedLandmark);
            return true;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < LandmarkCount;
        }

        private SkeletonJointId ToSkeletonJoint(MediaPipePoseLandmark landmark)
        {
            return HumanPoseSkeleton33.ToJointId((SkeletonJoint)(int)landmark);
        }

        private MediaPipePoseLandmark Map(
            MediaPipePoseLandmark landmark,
            PoseMirrorMode mirrorMode)
        {
            if (mirrorMode != PoseMirrorMode.SwapLeftRight)
            {
                return landmark;
            }

            switch (landmark)
            {
                case MediaPipePoseLandmark.LEFT_EYE_INNER: return MediaPipePoseLandmark.RIGHT_EYE_INNER;
                case MediaPipePoseLandmark.LEFT_EYE: return MediaPipePoseLandmark.RIGHT_EYE;
                case MediaPipePoseLandmark.LEFT_EYE_OUTER: return MediaPipePoseLandmark.RIGHT_EYE_OUTER;
                case MediaPipePoseLandmark.LEFT_EAR: return MediaPipePoseLandmark.RIGHT_EAR;
                case MediaPipePoseLandmark.MOUTH_LEFT: return MediaPipePoseLandmark.MOUTH_RIGHT;
                case MediaPipePoseLandmark.LEFT_SHOULDER: return MediaPipePoseLandmark.RIGHT_SHOULDER;
                case MediaPipePoseLandmark.LEFT_ELBOW: return MediaPipePoseLandmark.RIGHT_ELBOW;
                case MediaPipePoseLandmark.LEFT_WRIST: return MediaPipePoseLandmark.RIGHT_WRIST;
                case MediaPipePoseLandmark.LEFT_PINKY: return MediaPipePoseLandmark.RIGHT_PINKY;
                case MediaPipePoseLandmark.LEFT_INDEX: return MediaPipePoseLandmark.RIGHT_INDEX;
                case MediaPipePoseLandmark.LEFT_THUMB: return MediaPipePoseLandmark.RIGHT_THUMB;
                case MediaPipePoseLandmark.LEFT_HIP: return MediaPipePoseLandmark.RIGHT_HIP;
                case MediaPipePoseLandmark.LEFT_KNEE: return MediaPipePoseLandmark.RIGHT_KNEE;
                case MediaPipePoseLandmark.LEFT_ANKLE: return MediaPipePoseLandmark.RIGHT_ANKLE;
                case MediaPipePoseLandmark.LEFT_HEEL: return MediaPipePoseLandmark.RIGHT_HEEL;
                case MediaPipePoseLandmark.LEFT_FOOT_INDEX: return MediaPipePoseLandmark.RIGHT_FOOT_INDEX;

                case MediaPipePoseLandmark.RIGHT_EYE_INNER: return MediaPipePoseLandmark.LEFT_EYE_INNER;
                case MediaPipePoseLandmark.RIGHT_EYE: return MediaPipePoseLandmark.LEFT_EYE;
                case MediaPipePoseLandmark.RIGHT_EYE_OUTER: return MediaPipePoseLandmark.LEFT_EYE_OUTER;
                case MediaPipePoseLandmark.RIGHT_EAR: return MediaPipePoseLandmark.LEFT_EAR;
                case MediaPipePoseLandmark.MOUTH_RIGHT: return MediaPipePoseLandmark.MOUTH_LEFT;
                case MediaPipePoseLandmark.RIGHT_SHOULDER: return MediaPipePoseLandmark.LEFT_SHOULDER;
                case MediaPipePoseLandmark.RIGHT_ELBOW: return MediaPipePoseLandmark.LEFT_ELBOW;
                case MediaPipePoseLandmark.RIGHT_WRIST: return MediaPipePoseLandmark.LEFT_WRIST;
                case MediaPipePoseLandmark.RIGHT_PINKY: return MediaPipePoseLandmark.LEFT_PINKY;
                case MediaPipePoseLandmark.RIGHT_INDEX: return MediaPipePoseLandmark.LEFT_INDEX;
                case MediaPipePoseLandmark.RIGHT_THUMB: return MediaPipePoseLandmark.LEFT_THUMB;
                case MediaPipePoseLandmark.RIGHT_HIP: return MediaPipePoseLandmark.LEFT_HIP;
                case MediaPipePoseLandmark.RIGHT_KNEE: return MediaPipePoseLandmark.LEFT_KNEE;
                case MediaPipePoseLandmark.RIGHT_ANKLE: return MediaPipePoseLandmark.LEFT_ANKLE;
                case MediaPipePoseLandmark.RIGHT_HEEL: return MediaPipePoseLandmark.LEFT_HEEL;
                case MediaPipePoseLandmark.RIGHT_FOOT_INDEX: return MediaPipePoseLandmark.LEFT_FOOT_INDEX;

                default: return landmark;
            }
        }
    }
}
