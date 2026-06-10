using System;

namespace HEXLab.Hextrackingconnector
{
    public sealed class SkeletonRecordingInfo
    {
        public SkeletonRecordingInfo(
            SkeletonRecordingFormat format,
            int version,
            string createdUtc,
            SkeletonDefinition definition,
            int frameCount,
            double duration)
        {
            Format = format;
            Version = version;
            CreatedUtc = createdUtc ?? string.Empty;
            Definition = definition ?? SkeletonDefinition.Empty;
            FrameCount = frameCount;
            Duration = Math.Max(0.0, duration);
        }

        public SkeletonRecordingFormat Format { get; }
        public int Version { get; }
        public string CreatedUtc { get; }
        public SkeletonDefinition Definition { get; }
        public int FrameCount { get; }
        public double Duration { get; }
    }
}
