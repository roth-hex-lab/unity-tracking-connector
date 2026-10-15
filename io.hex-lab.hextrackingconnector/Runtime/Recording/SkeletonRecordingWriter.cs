using System;
using System.IO;
using System.Runtime.ExceptionServices;

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

    internal static class SkeletonRecordingWriterCleanup
    {
        internal static void DisposeAfterFailure(Exception failure, IDisposable resource)
        {
            CompleteAndDispose(() => ExceptionDispatchInfo.Capture(failure).Throw(), resource);
        }

        internal static void CompleteAndDispose(Action complete, IDisposable resource)
        {
            Exception failure = null;
            try { complete(); }
            catch (Exception exception) { failure = exception; }
            try { resource.Dispose(); }
            catch (Exception exception)
            {
                if (failure == null) throw;
                throw new AggregateException(failure, exception);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
