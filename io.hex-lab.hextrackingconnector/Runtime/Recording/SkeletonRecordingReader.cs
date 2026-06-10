using System;
using System.IO;

namespace HEXLab.Hextrackingconnector
{
    internal interface ISkeletonRecordingReader : IDisposable
    {
        SkeletonRecordingInfo Info { get; }
        bool TryReadNextFrame(out SkeletonRecordedFrame frame);
    }

    public sealed class SkeletonRecordingReader : IDisposable
    {
        private readonly string path;
        private ISkeletonRecordingReader reader;

        private SkeletonRecordingReader(string path, ISkeletonRecordingReader reader)
        {
            this.path = path;
            this.reader = reader;
        }

        public SkeletonRecordingInfo Info => reader?.Info;
        public string Path => path;

        public static SkeletonRecordingReader Open(string path)
        {
            return new SkeletonRecordingReader(path, OpenInternal(path));
        }

        public static SkeletonRecordingInfo ReadInfo(string path)
        {
            using (var reader = OpenInternal(path))
            {
                var info = reader.Info;
                var frameCount = Math.Max(0, info.FrameCount);
                var duration = info.Duration;

                while (reader.TryReadNextFrame(out var frame))
                {
                    frameCount = Math.Max(frameCount, frame.RecordIndex + 1);
                    duration = Math.Max(duration, frame.RecordedTime);
                }

                info = reader.Info ?? info;
                frameCount = Math.Max(frameCount, info.FrameCount);
                duration = Math.Max(duration, info.Duration);

                return new SkeletonRecordingInfo(
                    info.Format,
                    info.Version,
                    info.CreatedUtc,
                    info.Definition,
                    frameCount,
                    duration);
            }
        }

        public static SkeletonRecordingFormat DetectFormat(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A recording path is required.", nameof(path));
            }

            var extension = System.IO.Path.GetExtension(path);
            if (string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                return SkeletonRecordingFormat.JsonLines;
            }

            return SkeletonRecordingFormat.Binary;
        }

        public bool TryReadNextFrame(out SkeletonRecordedFrame frame)
        {
            if (reader == null)
            {
                frame = default;
                return false;
            }

            return reader.TryReadNextFrame(out frame);
        }

        public void Reset()
        {
            reader?.Dispose();
            reader = null;
            reader = OpenInternal(path);
        }

        public void Dispose()
        {
            reader?.Dispose();
            reader = null;
        }

        private static ISkeletonRecordingReader OpenInternal(string path)
        {
            switch (DetectFormat(path))
            {
                case SkeletonRecordingFormat.JsonLines:
                    return new JsonLinesSkeletonRecordingReader(path);
                case SkeletonRecordingFormat.Binary:
                default:
                    return new BinarySkeletonRecordingReader(path);
            }
        }
    }
}
