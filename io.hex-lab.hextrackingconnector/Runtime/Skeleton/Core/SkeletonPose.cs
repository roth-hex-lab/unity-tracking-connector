using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    /// <summary>
    /// Flags that describe which values in a <see cref="SkeletonJointPose"/> are meaningful.
    /// </summary>
    /// <remarks>
    /// Each channel uses one bit so callers can combine available data with bitwise flags,
    /// for example <c>Position | Rotation</c> without inventing confidence.
    /// </remarks>
    [Flags]
    public enum SkeletonJointChannels
    {
        /// <summary>No pose channels are available.</summary>
        None = 0,

        /// <summary>The joint pose contains a meaningful position.</summary>
        Position = 1 << 0,

        /// <summary>The joint pose contains a meaningful rotation.</summary>
        Rotation = 1 << 1,

        /// <summary>The joint pose contains a meaningful confidence value.</summary>
        Confidence = 1 << 2,
    }

    /// <summary>
    /// Describes where a joint value came from before it reached Unity.
    /// </summary>
    public enum SkeletonDataProvenance
    {
        /// <summary>The source of the value is not known.</summary>
        Unknown,

        /// <summary>The value came directly from the tracker or sender for this frame.</summary>
        Direct,

        /// <summary>The value was calculated from other tracked data, such as a limb rotation from landmarks.</summary>
        Inferred,

        /// <summary>The sender reused a previous value because no fresh value was available this frame.</summary>
        Held,

        /// <summary>
        /// The value describes a default/rest pose rather than live tracking data.
        /// Consumers such as <see cref="DirectHumanoidBoneDriver"/> avoid driving rotations from rest data.
        /// </summary>
        Rest,
    }

    /// <summary>
    /// Coordinate space during use.
    /// </summary>
    public enum SkeletonCoordinateSpace
    {
        Unspecified,
        World,
        RootRelative,
        ParentLocal,
        CameraRelative,
    }

    public readonly struct SkeletonFrameMetadata
    {
        private readonly string sourceId;

        public SkeletonFrameMetadata(
            int sequenceNumber,
            double receivedTime,
            double sourceTimestamp = 0.0,
            string sourceId = null)
        {
            SequenceNumber = sequenceNumber;
            ReceivedTime = receivedTime;
            SourceTimestamp = sourceTimestamp;
            this.sourceId = sourceId;
        }

        public int SequenceNumber { get; }
        public double ReceivedTime { get; }
        public double SourceTimestamp { get; }
        public string SourceId => sourceId ?? string.Empty;
    }

    public readonly struct SkeletonJointPose
    {
        public static readonly SkeletonJointPose Unavailable =
            new SkeletonJointPose(SkeletonJointChannels.None, default, Quaternion.identity, 0f, SkeletonDataProvenance.Unknown);

        private readonly string source;

        // Channels say which values are meaningful. A pose can carry position,
        // rotation, confidence, or any useful subset without inventing missing data.
        public SkeletonJointPose(
            SkeletonJointChannels channels,
            Vector3 position,
            Quaternion rotation,
            float confidence,
            SkeletonDataProvenance provenance,
            string source = null)
        {
            Channels = channels;
            Position = position;
            Rotation = IsZero(rotation) ? Quaternion.identity : rotation;
            Confidence = Mathf.Clamp01(confidence);
            Provenance = provenance;
            this.source = source;
        }

        public SkeletonJointChannels Channels { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Confidence { get; }
        public SkeletonDataProvenance Provenance { get; }
        public string Source => source ?? string.Empty;
        public bool HasPosition => HasChannel(SkeletonJointChannels.Position);
        public bool HasRotation => HasChannel(SkeletonJointChannels.Rotation);
        public bool HasConfidence => HasChannel(SkeletonJointChannels.Confidence);
        public bool IsAvailable => Channels != SkeletonJointChannels.None;

        // Legacy name used by the original position-only API.
        public bool IsTracked => HasPosition;

        public static SkeletonJointPose FromPosition(
            Vector3 position,
            float confidence = 1f,
            SkeletonDataProvenance provenance = SkeletonDataProvenance.Direct,
            string source = null)
        {
            return new SkeletonJointPose(
                SkeletonJointChannels.Position | SkeletonJointChannels.Confidence,
                position,
                Quaternion.identity,
                confidence,
                provenance,
                source);
        }

        public static SkeletonJointPose FromRotation(
            Quaternion rotation,
            float confidence = 1f,
            SkeletonDataProvenance provenance = SkeletonDataProvenance.Direct,
            string source = null)
        {
            return new SkeletonJointPose(
                SkeletonJointChannels.Rotation | SkeletonJointChannels.Confidence,
                default,
                rotation,
                confidence,
                provenance,
                source);
        }

        public static SkeletonJointPose FromPositionAndRotation(
            Vector3 position,
            Quaternion rotation,
            float confidence = 1f,
            SkeletonDataProvenance provenance = SkeletonDataProvenance.Direct,
            string source = null)
        {
            return new SkeletonJointPose(
                SkeletonJointChannels.Position | SkeletonJointChannels.Rotation | SkeletonJointChannels.Confidence,
                position,
                rotation,
                confidence,
                provenance,
                source);
        }

        public bool HasChannel(SkeletonJointChannels channel)
        {
            return (Channels & channel) == channel;
        }

        private static bool IsZero(Quaternion rotation)
        {
            return Mathf.Approximately(rotation.x, 0f) &&
                   Mathf.Approximately(rotation.y, 0f) &&
                   Mathf.Approximately(rotation.z, 0f) &&
                   Mathf.Approximately(rotation.w, 0f);
        }
    }

    public readonly struct SkeletonPose
    {
        private static readonly SkeletonJointPose[] EmptyJointPoses = new SkeletonJointPose[0];
        private static readonly Vector3[] EmptyPositions = new Vector3[0];
        private static readonly bool[] EmptyTracked = new bool[0];

        private readonly SkeletonDefinition definition;
        private readonly SkeletonJointPose[] jointPoses;
        private readonly Vector3[] positions;
        private readonly bool[] positionTracked;

        public SkeletonPose(
            SkeletonDefinition definition,
            SkeletonJointPose[] jointPoses,
            SkeletonCoordinateSpace coordinateSpace = SkeletonCoordinateSpace.Unspecified)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (jointPoses == null)
            {
                throw new ArgumentNullException(nameof(jointPoses));
            }

            if (jointPoses.Length != definition.JointCount)
            {
                throw new ArgumentException($"Expected {definition.JointCount} joint poses.", nameof(jointPoses));
            }

            this.definition = definition;
            this.jointPoses = (SkeletonJointPose[])jointPoses.Clone();
            CoordinateSpace = coordinateSpace;

            positions = new Vector3[definition.JointCount];
            positionTracked = new bool[definition.JointCount];
            for (int i = 0; i < this.jointPoses.Length; i++)
            {
                if (!this.jointPoses[i].HasPosition)
                {
                    continue;
                }

                positions[i] = this.jointPoses[i].Position;
                positionTracked[i] = true;
            }
        }

        public SkeletonPose(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            SkeletonCoordinateSpace coordinateSpace = SkeletonCoordinateSpace.Unspecified)
            : this(definition, ToJointPoses(definition, positions, tracked), coordinateSpace)
        {
        }

        public SkeletonDefinition Definition => definition ?? SkeletonDefinition.Empty;
        public int JointCount => Definition.JointCount;
        public SkeletonCoordinateSpace CoordinateSpace { get; }
        public IReadOnlyList<SkeletonJointPose> JointPoses => jointPoses ?? EmptyJointPoses;
        public IReadOnlyList<Vector3> Positions => positions ?? EmptyPositions;

        public SkeletonJointPose this[SkeletonJointId joint] => GetPoint(joint);

        public bool IsTracked(SkeletonJointId joint)
        {
            return IsTracked(Definition.IndexOf(joint));
        }

        public bool IsTracked(int index)
        {
            return Definition.IsValidIndex(index) && (positionTracked ?? EmptyTracked)[index];
        }

        public Vector3 GetJoint(SkeletonJointId joint)
        {
            var index = Definition.IndexOf(joint);
            if (!Definition.IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(joint));
            }

            return GetJoint(index);
        }

        public Vector3 GetJoint(int index)
        {
            if (!Definition.IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (positions ?? EmptyPositions)[index];
        }

        public SkeletonJointPose GetPoint(SkeletonJointId joint)
        {
            return GetPoint(Definition.IndexOf(joint));
        }

        public SkeletonJointPose GetPoint(int index)
        {
            if (!Definition.IsValidIndex(index))
            {
                return SkeletonJointPose.Unavailable;
            }

            return (jointPoses ?? EmptyJointPoses)[index];
        }

        public bool TryGetJointPose(SkeletonJointId joint, out SkeletonJointPose pose)
        {
            return TryGetJointPose(Definition.IndexOf(joint), out pose);
        }

        public bool TryGetJointPose(int index, out SkeletonJointPose pose)
        {
            pose = GetPoint(index);
            return pose.IsAvailable;
        }

        public bool TryGetJoint(SkeletonJointId joint, out Vector3 position)
        {
            return TryGetJoint(Definition.IndexOf(joint), out position);
        }

        public bool TryGetJoint(int index, out Vector3 position)
        {
            var pose = GetPoint(index);
            if (!pose.HasPosition)
            {
                position = default;
                return false;
            }

            position = pose.Position;
            return true;
        }

        public bool TryGetRotation(SkeletonJointId joint, out Quaternion rotation)
        {
            return TryGetRotation(Definition.IndexOf(joint), out rotation);
        }

        public bool TryGetRotation(int index, out Quaternion rotation)
        {
            var pose = GetPoint(index);
            if (!pose.HasRotation)
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = pose.Rotation;
            return true;
        }

        public bool TryGetHeadPose(out SkeletonHeadPose headPose)
        {
            return Definition.TryGetHeadPose(Positions, positionTracked ?? EmptyTracked, out headPose);
        }

        public Vector3[] CopyPositions()
        {
            return (Vector3[])(positions ?? EmptyPositions).Clone();
        }

        public bool[] CopyTracked()
        {
            return (bool[])(positionTracked ?? EmptyTracked).Clone();
        }

        public SkeletonJointPose[] CopyJointPoses()
        {
            return (SkeletonJointPose[])(jointPoses ?? EmptyJointPoses).Clone();
        }

        private static SkeletonJointPose[] ToJointPoses(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            if (tracked == null)
            {
                throw new ArgumentNullException(nameof(tracked));
            }

            if (positions.Length != definition.JointCount)
            {
                throw new ArgumentException($"Expected {definition.JointCount} positions.", nameof(positions));
            }

            if (tracked.Length != definition.JointCount)
            {
                throw new ArgumentException($"Expected {definition.JointCount} tracked flags.", nameof(tracked));
            }

            var poses = new SkeletonJointPose[definition.JointCount];
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = tracked[i]
                    ? SkeletonJointPose.FromPosition(positions[i])
                    : SkeletonJointPose.Unavailable;
            }

            return poses;
        }
    }
}
