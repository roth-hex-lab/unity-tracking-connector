using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    [Serializable]
    internal sealed class SkeletonRecordingLineDto
    {
        public string type;
        public SkeletonRecordingHeaderDto header;
        public SkeletonRecordingFrameDto frame;
        public SkeletonRecordingFooterDto footer;
    }

    [Serializable]
    internal sealed class SkeletonRecordingHeaderDto
    {
        public string format;
        public int version;
        public string createdUtc;
        public SkeletonRecordingDefinitionDto definition;
    }

    [Serializable]
    internal sealed class SkeletonRecordingFooterDto
    {
        public int frameCount;
        public double duration;
    }

    [Serializable]
    internal sealed class SkeletonRecordingDefinitionDto
    {
        public string id;
        public string name;
        public string[] joints;
    }

    [Serializable]
    internal sealed class SkeletonRecordingFrameDto
    {
        public int recordIndex;
        public double recordedTime;
        public string coordinateSpace;
        public SkeletonRecordingMetadataDto metadata;
        public SkeletonRecordingJointPoseDto[] joints;
    }

    [Serializable]
    internal sealed class SkeletonRecordingMetadataDto
    {
        public int sequenceNumber;
        public double receivedTime;
        public double sourceTimestamp;
        public string sourceId;
    }

    [Serializable]
    internal sealed class SkeletonRecordingJointPoseDto
    {
        public int channels;
        public float px;
        public float py;
        public float pz;
        public float qx;
        public float qy;
        public float qz;
        public float qw;
        public float confidence;
        public string provenance;
        public string source;
    }

    internal static class SkeletonRecordingDtoMapper
    {
        public const int Version = 1;
        public const string JsonLinesFormatName = "hexpose-jsonl";
        public const string BinaryFormatName = "hexpose-binary";

        public static SkeletonRecordingDefinitionDto ToDefinitionDto(SkeletonDefinition definition)
        {
            definition = definition ?? SkeletonDefinition.Empty;
            var joints = new string[definition.JointCount];
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = definition.JointAt(i).Name;
            }

            return new SkeletonRecordingDefinitionDto
            {
                id = definition.Id,
                name = definition.Name,
                joints = joints,
            };
        }

        public static SkeletonDefinition ToDefinition(SkeletonRecordingDefinitionDto dto)
        {
            if (dto == null)
            {
                return SkeletonDefinition.Empty;
            }

            return SkeletonRecordingDefinitionUtility.ResolveDefinition(
                dto.id,
                dto.name,
                dto.joints);
        }

        public static SkeletonRecordingFrameDto ToFrameDto(SkeletonRecordedFrame recordedFrame)
        {
            var frame = recordedFrame.Frame;
            var jointPoses = frame.CopyJointPoses();
            var joints = new SkeletonRecordingJointPoseDto[jointPoses.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = ToJointPoseDto(jointPoses[i]);
            }

            return new SkeletonRecordingFrameDto
            {
                recordIndex = recordedFrame.RecordIndex,
                recordedTime = recordedFrame.RecordedTime,
                coordinateSpace = frame.CoordinateSpace.ToString(),
                metadata = new SkeletonRecordingMetadataDto
                {
                    sequenceNumber = frame.Metadata.SequenceNumber,
                    receivedTime = frame.Metadata.ReceivedTime,
                    sourceTimestamp = frame.Metadata.SourceTimestamp,
                    sourceId = frame.Metadata.SourceId,
                },
                joints = joints,
            };
        }

        public static SkeletonRecordedFrame ToRecordedFrame(
            SkeletonRecordingFrameDto dto,
            SkeletonDefinition definition)
        {
            if (dto == null)
            {
                return default;
            }

            definition = definition ?? SkeletonDefinition.Empty;
            var sourceJoints = dto.joints ?? new SkeletonRecordingJointPoseDto[0];
            var jointPoses = new SkeletonJointPose[definition.JointCount];
            for (int i = 0; i < jointPoses.Length; i++)
            {
                jointPoses[i] = i < sourceJoints.Length
                    ? ToJointPose(sourceJoints[i])
                    : SkeletonJointPose.Unavailable;
            }

            var metadata = dto.metadata ?? new SkeletonRecordingMetadataDto();
            var coordinateSpace = ParseEnum(dto.coordinateSpace, SkeletonCoordinateSpace.Unspecified);
            var frame = new SkeletonFrame(
                new SkeletonPose(definition, jointPoses, coordinateSpace),
                new SkeletonFrameMetadata(
                    metadata.sequenceNumber,
                    metadata.receivedTime,
                    metadata.sourceTimestamp,
                    metadata.sourceId));

            return new SkeletonRecordedFrame(dto.recordIndex, dto.recordedTime, frame);
        }

        private static SkeletonRecordingJointPoseDto ToJointPoseDto(SkeletonJointPose pose)
        {
            return new SkeletonRecordingJointPoseDto
            {
                channels = (int)pose.Channels,
                px = pose.Position.x,
                py = pose.Position.y,
                pz = pose.Position.z,
                qx = pose.Rotation.x,
                qy = pose.Rotation.y,
                qz = pose.Rotation.z,
                qw = pose.Rotation.w,
                confidence = pose.Confidence,
                provenance = pose.Provenance.ToString(),
                source = pose.Source,
            };
        }

        private static SkeletonJointPose ToJointPose(SkeletonRecordingJointPoseDto dto)
        {
            if (dto == null)
            {
                return SkeletonJointPose.Unavailable;
            }

            var channels = (SkeletonJointChannels)dto.channels;
            if (channels == SkeletonJointChannels.None)
            {
                return SkeletonJointPose.Unavailable;
            }

            return new SkeletonJointPose(
                channels,
                new Vector3(dto.px, dto.py, dto.pz),
                new Quaternion(dto.qx, dto.qy, dto.qz, dto.qw),
                dto.confidence,
                ParseEnum(dto.provenance, SkeletonDataProvenance.Unknown),
                dto.source);
        }

        public static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
            where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
                ? parsed
                : fallback;
        }
    }
}
