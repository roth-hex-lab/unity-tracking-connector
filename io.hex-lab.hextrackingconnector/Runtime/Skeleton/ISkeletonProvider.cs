using System;

namespace HEXLab.Hextrackingconnector
{
    public interface ISkeletonProvider
    {
        event Action<SkeletonFrame> PoseReceived;
        bool TryGetLatestPose(out SkeletonFrame pose);
    }
}
