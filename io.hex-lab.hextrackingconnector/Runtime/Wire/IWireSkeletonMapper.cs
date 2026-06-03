namespace HEXLab.Hextrackingconnector
{
    public interface IWireSkeletonMapper
    {
        SkeletonDefinition Definition { get; }
        bool TryMapIndex(int sourceIndex, out SkeletonJointId joint);
    }
}
