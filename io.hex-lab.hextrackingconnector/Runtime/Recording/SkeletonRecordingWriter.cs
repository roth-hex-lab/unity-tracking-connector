using System;
using System.IO;

namespace HEXLab.Hextrackingconnector
{
    internal interface ISkeletonRecordingWriter : IDisposable
    {
        int FrameCount { get; }
        double Duration { get; }
        void WriteFrame(SkeletonRecordedFrame frame);
    }

    internal static class SkeletonRecordingWriterFactory
    {
        public static ISkeletonRecordingWriter Create(
            SkeletonRecordingFormat format,
            string path,
            SkeletonDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A recording path is required.", nameof(path));
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            switch (format)
            {
                case SkeletonRecordingFormat.Binary:
                    return new BinarySkeletonRecordingWriter(path, definition);
                case SkeletonRecordingFormat.JsonLines:
                default:
                    return new JsonLinesSkeletonRecordingWriter(path, definition);
            }
        }
    }
}
