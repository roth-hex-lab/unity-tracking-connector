namespace HEXLab.Hextrackingconnector
{
    internal interface IWireSkeletonMapper
    {
        SkeletonDefinition Definition { get; }
        bool TryMapIndex(int sourceIndex, PoseMirrorMode mirrorMode, out SkeletonJointId joint);
    }
}
