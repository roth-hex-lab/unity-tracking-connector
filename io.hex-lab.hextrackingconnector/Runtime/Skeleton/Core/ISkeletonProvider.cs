using System;

namespace HEXLab.Hextrackingconnector
{
    /// <summary>
    /// Exposes skeleton frames from a pipeline block.
    /// </summary>
    /// <remarks>
    /// Implementations publish new frames through <see cref="PoseReceived"/> and let late
    /// subscribers query the newest frame through <see cref="TryGetLatestPose"/>.
    /// 
    /// The interface cannot enforce how an event is raised because only the declaring
    /// class can invoke its own event. Provider implementations should call
    /// <see cref="SkeletonProviderUtility.RaisePoseReceived"/> so one failing subscriber
    /// is logged without stopping the rest of the pipeline.
    /// </remarks>
    public interface ISkeletonProvider
    {
        /// <summary>
        /// Raised when this provider has a new skeleton frame.
        /// </summary>
        event Action<SkeletonFrame> PoseReceived;

        /// <summary>
        /// Gets the most recent frame published by this provider, if one is available.
        /// </summary>
        bool TryGetLatestPose(out SkeletonFrame pose);
    }
}
