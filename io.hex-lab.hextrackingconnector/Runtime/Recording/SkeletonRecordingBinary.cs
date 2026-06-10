using System;
using System.IO;
using System.Text;

namespace HEXLab.Hextrackingconnector
{
    internal sealed class BinarySkeletonRecordingWriter : ISkeletonRecordingWriter
    {
        private const byte FrameRecord = 1;
        private const byte FooterRecord = 255;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HXPB");

        private readonly BinaryWriter writer;
        private bool disposed;

        public BinarySkeletonRecordingWriter(string path, SkeletonDefinition definition)
        {
            writer = new BinaryWriter(
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete),
                Encoding.UTF8);
            WriteHeader(definition);
        }

        public int FrameCount { get; private set; }
        public double Duration { get; private set; }

        public void WriteFrame(SkeletonRecordedFrame frame)
        {
            ThrowIfDisposed();

            var dto = SkeletonRecordingDtoMapper.ToFrameDto(frame);
            writer.Write(FrameRecord);
            writer.Write(dto.recordIndex);
            writer.Write(dto.recordedTime);
            WriteString(dto.coordinateSpace);
            WriteMetadata(dto.metadata);

            var joints = dto.joints ?? new SkeletonRecordingJointPoseDto[0];
            writer.Write(joints.Length);
            for (int i = 0; i < joints.Length; i++)
            {
                WriteJoint(joints[i]);
            }

            FrameCount++;
            Duration = Math.Max(Duration, frame.RecordedTime);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            writer.Write(FooterRecord);
            writer.Write(FrameCount);
            writer.Write(Duration);
            writer.Dispose();
            disposed = true;
        }

        private void WriteHeader(SkeletonDefinition definition)
        {
            writer.Write(Magic);
            writer.Write(SkeletonRecordingDtoMapper.Version);
            WriteString(DateTimeOffset.UtcNow.ToString("O"));

            var dto = SkeletonRecordingDtoMapper.ToDefinitionDto(definition);
            WriteString(dto.id);
            WriteString(dto.name);
            var joints = dto.joints ?? new string[0];
            writer.Write(joints.Length);
            for (int i = 0; i < joints.Length; i++)
            {
                WriteString(joints[i]);
            }
        }

        private void WriteMetadata(SkeletonRecordingMetadataDto metadata)
        {
            metadata = metadata ?? new SkeletonRecordingMetadataDto();
            writer.Write(metadata.sequenceNumber);
            writer.Write(metadata.receivedTime);
            writer.Write(metadata.sourceTimestamp);
            WriteString(metadata.sourceId);
        }

        private void WriteJoint(SkeletonRecordingJointPoseDto joint)
        {
            joint = joint ?? new SkeletonRecordingJointPoseDto();
            writer.Write(joint.channels);
            writer.Write(joint.px);
            writer.Write(joint.py);
            writer.Write(joint.pz);
            writer.Write(joint.qx);
            writer.Write(joint.qy);
            writer.Write(joint.qz);
            writer.Write(joint.qw);
            writer.Write(joint.confidence);
            WriteString(joint.provenance);
            WriteString(joint.source);
        }

        private void WriteString(string value)
        {
            writer.Write(value ?? string.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(BinarySkeletonRecordingWriter));
            }
        }

        public static bool HasBinaryMagic(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < Magic.Length)
                {
                    return false;
                }

                for (int i = 0; i < Magic.Length; i++)
                {
                    if (stream.ReadByte() != Magic[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static void ReadAndValidateMagic(BinaryReader reader)
        {
            for (int i = 0; i < Magic.Length; i++)
            {
                if (reader.ReadByte() != Magic[i])
                {
                    throw new InvalidDataException("Recording is not a HEX binary pose file.");
                }
            }
        }
    }

    internal sealed class BinarySkeletonRecordingReader : ISkeletonRecordingReader
    {
        private const byte FrameRecord = 1;
        private const byte FooterRecord = 255;

        private readonly BinaryReader reader;
        private readonly SkeletonDefinition definition;
        private SkeletonRecordingInfo info;

        public BinarySkeletonRecordingReader(string path)
        {
            reader = new BinaryReader(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete),
                Encoding.UTF8);
            BinarySkeletonRecordingWriter.ReadAndValidateMagic(reader);

            var version = reader.ReadInt32();
            var createdUtc = ReadString();
            var definitionDto = ReadDefinition();
            definition = SkeletonRecordingDtoMapper.ToDefinition(definitionDto);
            info = new SkeletonRecordingInfo(
                SkeletonRecordingFormat.Binary,
                version,
                createdUtc,
                definition,
                frameCount: -1,
                duration: 0.0);
        }

        public SkeletonRecordingInfo Info => info;

        public bool TryReadNextFrame(out SkeletonRecordedFrame frame)
        {
            frame = default;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte recordType;
                try
                {
                    recordType = reader.ReadByte();
                }
                catch (EndOfStreamException)
                {
                    return false;
                }

                if (recordType == FooterRecord)
                {
                    var frameCount = reader.ReadInt32();
                    var duration = reader.ReadDouble();
                    info = new SkeletonRecordingInfo(
                        info.Format,
                        info.Version,
                        info.CreatedUtc,
                        info.Definition,
                        frameCount,
                        duration);
                    return false;
                }

                if (recordType != FrameRecord)
                {
                    throw new InvalidDataException($"Unknown binary recording record type {recordType}.");
                }

                frame = ReadFrame();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            reader.Dispose();
        }

        private SkeletonRecordingDefinitionDto ReadDefinition()
        {
            var id = ReadString();
            var name = ReadString();
            var jointCount = reader.ReadInt32();
            var joints = new string[Math.Max(0, jointCount)];
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = ReadString();
            }

            return new SkeletonRecordingDefinitionDto
            {
                id = id,
                name = name,
                joints = joints,
            };
        }

        private SkeletonRecordedFrame ReadFrame()
        {
            var dto = new SkeletonRecordingFrameDto
            {
                recordIndex = reader.ReadInt32(),
                recordedTime = reader.ReadDouble(),
                coordinateSpace = ReadString(),
                metadata = ReadMetadata(),
            };

            var jointCount = reader.ReadInt32();
            dto.joints = new SkeletonRecordingJointPoseDto[Math.Max(0, jointCount)];
            for (int i = 0; i < dto.joints.Length; i++)
            {
                dto.joints[i] = ReadJoint();
            }

            return SkeletonRecordingDtoMapper.ToRecordedFrame(dto, definition);
        }

        private SkeletonRecordingMetadataDto ReadMetadata()
        {
            return new SkeletonRecordingMetadataDto
            {
                sequenceNumber = reader.ReadInt32(),
                receivedTime = reader.ReadDouble(),
                sourceTimestamp = reader.ReadDouble(),
                sourceId = ReadString(),
            };
        }

        private SkeletonRecordingJointPoseDto ReadJoint()
        {
            return new SkeletonRecordingJointPoseDto
            {
                channels = reader.ReadInt32(),
                px = reader.ReadSingle(),
                py = reader.ReadSingle(),
                pz = reader.ReadSingle(),
                qx = reader.ReadSingle(),
                qy = reader.ReadSingle(),
                qz = reader.ReadSingle(),
                qw = reader.ReadSingle(),
                confidence = reader.ReadSingle(),
                provenance = ReadString(),
                source = ReadString(),
            };
        }

        private string ReadString()
        {
            return reader.ReadString();
        }
    }
}
