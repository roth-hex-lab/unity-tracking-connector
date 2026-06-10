using System;

namespace HEXLab.Hextrackingconnector
{
    /// <summary>
    /// Exposes every frame captured by a producer before provider-side throttling or processing.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ISkeletonProvider.PoseReceived"/>, this event may be raised from a
    /// background producer thread. Subscribers should do only thread-safe, quick work such as
    /// enqueueing the frame for later processing.
    /// </remarks>
    public interface ISkeletonCaptureSource
    {
        event Action<SkeletonFrame> FrameCaptured;

        bool TryGetLatestCapturedFrame(out SkeletonFrame frame);
    }
}
