using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    internal sealed class JsonLinesSkeletonRecordingWriter : ISkeletonRecordingWriter
    {
        private readonly StreamWriter writer;
        private bool disposed;

        public JsonLinesSkeletonRecordingWriter(string path, SkeletonDefinition definition)
        {
            writer = new StreamWriter(
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete),
                Encoding.UTF8);
            WriteLine(new SkeletonRecordingLineDto
            {
                type = "header",
                header = new SkeletonRecordingHeaderDto
                {
                    format = SkeletonRecordingDtoMapper.JsonLinesFormatName,
                    version = SkeletonRecordingDtoMapper.Version,
                    createdUtc = DateTimeOffset.UtcNow.ToString("O"),
                    definition = SkeletonRecordingDtoMapper.ToDefinitionDto(definition),
                },
            });
        }

        public int FrameCount { get; private set; }
        public double Duration { get; private set; }

        public void WriteFrame(SkeletonRecordedFrame frame)
        {
            ThrowIfDisposed();
            WriteLine(new SkeletonRecordingLineDto
            {
                type = "frame",
                frame = SkeletonRecordingDtoMapper.ToFrameDto(frame),
            });
            FrameCount++;
            Duration = Math.Max(Duration, frame.RecordedTime);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            WriteLine(new SkeletonRecordingLineDto
            {
                type = "footer",
                footer = new SkeletonRecordingFooterDto
                {
                    frameCount = FrameCount,
                    duration = Duration,
                },
            });
            writer.Dispose();
            disposed = true;
        }

        private void WriteLine(SkeletonRecordingLineDto line)
        {
            writer.WriteLine(JsonUtility.ToJson(line));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(JsonLinesSkeletonRecordingWriter));
            }
        }
    }

    internal sealed class JsonLinesSkeletonRecordingReader : ISkeletonRecordingReader
    {
        private readonly StreamReader reader;
        private SkeletonDefinition definition;
        private SkeletonRecordingInfo info;

        public JsonLinesSkeletonRecordingReader(string path)
        {
            reader = new StreamReader(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete),
                Encoding.UTF8);
            ReadHeader();
        }

        public SkeletonRecordingInfo Info => info;

        public bool TryReadNextFrame(out SkeletonRecordedFrame frame)
        {
            frame = default;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var dto = JsonUtility.FromJson<SkeletonRecordingLineDto>(line);
                if (dto == null)
                {
                    continue;
                }

                if (string.Equals(dto.type, "frame", StringComparison.OrdinalIgnoreCase))
                {
                    frame = SkeletonRecordingDtoMapper.ToRecordedFrame(dto.frame, definition);
                    return true;
                }

                if (string.Equals(dto.type, "footer", StringComparison.OrdinalIgnoreCase) &&
                    dto.footer != null)
                {
                    info = new SkeletonRecordingInfo(
                        info.Format,
                        info.Version,
                        info.CreatedUtc,
                        info.Definition,
                        dto.footer.frameCount,
                        dto.footer.duration);
                }
            }

            return false;
        }

        public void Dispose()
        {
            reader.Dispose();
        }

        private void ReadHeader()
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var dto = JsonUtility.FromJson<SkeletonRecordingLineDto>(line);
                if (dto == null ||
                    !string.Equals(dto.type, "header", StringComparison.OrdinalIgnoreCase) ||
                    dto.header == null)
                {
                    continue;
                }

                definition = SkeletonRecordingDtoMapper.ToDefinition(dto.header.definition);
                info = new SkeletonRecordingInfo(
                    SkeletonRecordingFormat.JsonLines,
                    dto.header.version,
                    dto.header.createdUtc,
                    definition,
                    frameCount: -1,
                    duration: 0.0);
                return;
            }

            throw new InvalidDataException("Recording is missing a JSONL header.");
        }
    }
}
