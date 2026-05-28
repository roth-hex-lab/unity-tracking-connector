namespace HEXLab.Hextrackingconnector
{
    public interface IWireSkeletonMapper
    {
        SkeletonDefinition Definition { get; }
        bool TryMapIndex(int sourceIndex, PoseMirrorMode mirrorMode, out SkeletonJointId joint);
    }
}
