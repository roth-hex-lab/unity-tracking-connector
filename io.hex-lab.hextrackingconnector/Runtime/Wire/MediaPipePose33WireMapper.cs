namespace HEXLab.Hextrackingconnector
{
    internal sealed class MediaPipePose33WireMapper : IWireSkeletonMapper
    {
        public const int LandmarkCount = MediaPipePose33Landmarks.LandmarkCount;

        public SkeletonDefinition Definition => HumanPoseSkeleton33.Definition;

        public bool TryMapIndex(
            int sourceIndex,
            out SkeletonJointId joint)
        {
            return MediaPipePose33Landmarks.TryGetJoint(sourceIndex, out joint);
        }
    }
}
