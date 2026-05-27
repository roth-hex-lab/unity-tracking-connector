using System;

namespace HEXLab.Hextrackingconnector
{
    public static class SkeletonFrameConversions
    {
        public static bool TryConvert(
            SkeletonFrame source,
            SkeletonDefinition targetDefinition,
            out SkeletonFrame convertedFrame)
        {
            if (targetDefinition == null)
            {
                convertedFrame = default;
                return false;
            }

            if (string.Equals(source.Definition.Id, targetDefinition.Id, StringComparison.Ordinal))
            {
                convertedFrame = source;
                return true;
            }

            if (targetDefinition == CocoPoseSkeleton17.Definition)
            {
                return CocoPoseSkeleton17.TryCreateFrom(source, out convertedFrame);
            }

            if (targetDefinition == UnityHumanoidControlSkeleton.Definition)
            {
                return UnityHumanoidPoseRetargeter.TryCreateFrom(source, out convertedFrame);
            }

            convertedFrame = default;
            return false;
        }
    }
}
