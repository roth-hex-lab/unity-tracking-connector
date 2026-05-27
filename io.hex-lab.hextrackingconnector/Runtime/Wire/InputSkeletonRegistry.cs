namespace HEXLab.Hextrackingconnector
{
    internal static class InputSkeletonRegistry
    {
        private const string MediaPipePose33Id = "mediapipe.pose.33";

        private static readonly MediaPipePose33WireMapper MediaPipePose33Mapper =
            new MediaPipePose33WireMapper();

        public static bool TryResolve(string skeletonId, out SkeletonDefinition definition)
        {
            if (IsMediaPipePose33(skeletonId))
            {
                definition = HumanPoseSkeleton33.Definition;
                return true;
            }

            definition = null;
            return false;
        }

        public static bool TryGetMapper(
            InputSkeletonSelection selection,
            string skeletonId,
            out IWireSkeletonMapper mapper)
        {
            switch (selection)
            {
                case InputSkeletonSelection.MediaPipePose33:
                    mapper = MediaPipePose33Mapper;
                    return true;
                case InputSkeletonSelection.Auto:
                default:
                    if (string.IsNullOrWhiteSpace(skeletonId) || IsMediaPipePose33(skeletonId))
                    {
                        mapper = MediaPipePose33Mapper;
                        return true;
                    }

                    mapper = null;
                    return false;
            }
        }

        private static bool IsMediaPipePose33(string skeletonId)
        {
            return string.Equals(skeletonId, MediaPipePose33Id, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(skeletonId, "mediapipe_pose_33", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(skeletonId, "HumanPoseSkeleton33", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
