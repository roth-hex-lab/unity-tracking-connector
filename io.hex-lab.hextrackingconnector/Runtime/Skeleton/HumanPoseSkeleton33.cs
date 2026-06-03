using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public readonly struct SkeletonJointId : IEquatable<SkeletonJointId>
    {
        private readonly string name;

        public SkeletonJointId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A skeleton joint id needs a name.", nameof(name));
            }

            this.name = name;
        }

        public string Name => name ?? string.Empty;

        public bool Equals(SkeletonJointId other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SkeletonJointId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name;
        }

        public static bool operator ==(SkeletonJointId left, SkeletonJointId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SkeletonJointId left, SkeletonJointId right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class SkeletonLineStrip
    {
        private readonly SkeletonJointId[] joints;

        public SkeletonLineStrip(params SkeletonJointId[] joints)
        {
            if (joints == null)
            {
                throw new ArgumentNullException(nameof(joints));
            }

            if (joints.Length < 2)
            {
                throw new ArgumentException("A debug line strip needs at least two joints.", nameof(joints));
            }

            this.joints = (SkeletonJointId[])joints.Clone();
        }

        public int Count => joints.Length;
        public IReadOnlyList<SkeletonJointId> Joints => joints;

        public SkeletonJointId this[int index]
        {
            get
            {
                if (index < 0 || index >= joints.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return joints[index];
            }
        }
    }

    public readonly struct SkeletonJointPair
    {
        public SkeletonJointPair(SkeletonJointId first, SkeletonJointId second)
        {
            First = first;
            Second = second;
        }

        public SkeletonJointId First { get; }
        public SkeletonJointId Second { get; }
    }

    public readonly struct SkeletonPoint
    {
        public SkeletonPoint(Vector3 position, bool isTracked)
        {
            Position = position;
            IsTracked = isTracked;
        }

        public Vector3 Position { get; }
        public bool IsTracked { get; }
    }

    public readonly struct SkeletonHeadPose
    {
        public SkeletonHeadPose(Vector3 position, Vector3 forward, Vector3 up)
        {
            Position = position;
            Forward = forward;
            Up = up;
        }

        public Vector3 Position { get; }
        public Vector3 Forward { get; }
        public Vector3 Up { get; }
    }

    public interface ISkeletonHeadPoseProvider
    {
        bool TryGetHeadPose(
            SkeletonDefinition definition,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<bool> tracked,
            out SkeletonHeadPose headPose);
    }

    public sealed class SkeletonDefinition
    {
        public static readonly SkeletonDefinition Empty =
            new SkeletonDefinition("empty", "Empty", new SkeletonJointId[0]);

        private readonly SkeletonJointId[] joints;
        private readonly SkeletonLineStrip[] debugLineStrips;
        private readonly SkeletonJointPair[] mirrorPairs;
        private readonly Dictionary<SkeletonJointId, int> jointIndices;
        private readonly ISkeletonHeadPoseProvider headPoseProvider;

        public SkeletonDefinition(
            string name,
            SkeletonJointId[] joints)
            : this(name, name, joints, null, null, null)
        {
        }

        public SkeletonDefinition(
            string id,
            string name,
            SkeletonJointId[] joints,
            SkeletonLineStrip[] debugLineStrips = null,
            ISkeletonHeadPoseProvider headPoseProvider = null,
            SkeletonJointPair[] mirrorPairs = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A skeleton definition needs an id.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A skeleton definition needs a name.", nameof(name));
            }

            this.joints = joints != null
                ? (SkeletonJointId[])joints.Clone()
                : throw new ArgumentNullException(nameof(joints));
            this.debugLineStrips = debugLineStrips != null
                ? (SkeletonLineStrip[])debugLineStrips.Clone()
                : new SkeletonLineStrip[0];
            this.mirrorPairs = mirrorPairs != null
                ? (SkeletonJointPair[])mirrorPairs.Clone()
                : new SkeletonJointPair[0];
            jointIndices = BuildIndex(this.joints);
            ValidateDebugLineStrips(this.debugLineStrips, jointIndices);
            ValidateMirrorPairs(this.mirrorPairs, jointIndices);
            this.headPoseProvider = headPoseProvider;

            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public int JointCount => joints.Length;
        public IReadOnlyList<SkeletonJointId> Joints => joints;
        public IReadOnlyList<SkeletonLineStrip> DebugLineStrips => debugLineStrips;
        public IReadOnlyList<SkeletonJointPair> MirrorPairs => mirrorPairs;
        public bool HasHeadPose => headPoseProvider != null;

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < joints.Length;
        }

        public bool Contains(SkeletonJointId joint)
        {
            return jointIndices.ContainsKey(joint);
        }

        public int IndexOf(SkeletonJointId joint)
        {
            return jointIndices.TryGetValue(joint, out var index) ? index : -1;
        }

        public SkeletonJointId JointAt(int index)
        {
            if (!IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return joints[index];
        }

        public bool TryGetHeadPose(
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<bool> tracked,
            out SkeletonHeadPose headPose)
        {
            headPose = default;
            if (headPoseProvider == null ||
                positions == null ||
                tracked == null ||
                positions.Count < JointCount ||
                tracked.Count < JointCount)
            {
                return false;
            }

            return headPoseProvider.TryGetHeadPose(this, positions, tracked, out headPose);
        }

        private static Dictionary<SkeletonJointId, int> BuildIndex(SkeletonJointId[] joints)
        {
            var indices = new Dictionary<SkeletonJointId, int>();
            for (int i = 0; i < joints.Length; i++)
            {
                if (indices.ContainsKey(joints[i]))
                {
                    throw new ArgumentException($"Duplicate skeleton joint id '{joints[i]}'.", nameof(joints));
                }

                indices.Add(joints[i], i);
            }

            return indices;
        }

        private static void ValidateDebugLineStrips(
            SkeletonLineStrip[] lineStrips,
            Dictionary<SkeletonJointId, int> indices)
        {
            foreach (var lineStrip in lineStrips)
            {
                if (lineStrip == null)
                {
                    throw new ArgumentException("Debug line strips cannot contain null entries.", nameof(lineStrips));
                }

                foreach (var joint in lineStrip.Joints)
                {
                    if (!indices.ContainsKey(joint))
                    {
                        throw new ArgumentException($"Debug line strip references unknown joint '{joint}'.", nameof(lineStrips));
                    }
                }
            }
        }

        private static void ValidateMirrorPairs(
            SkeletonJointPair[] pairs,
            Dictionary<SkeletonJointId, int> indices)
        {
            foreach (var pair in pairs)
            {
                if (!indices.ContainsKey(pair.First))
                {
                    throw new ArgumentException($"Mirror pair references unknown joint '{pair.First}'.", nameof(pairs));
                }

                if (!indices.ContainsKey(pair.Second))
                {
                    throw new ArgumentException($"Mirror pair references unknown joint '{pair.Second}'.", nameof(pairs));
                }
            }
        }
    }

    public static class HumanPoseSkeleton33
    {
        private static readonly SkeletonJointId[] JointList = BodyJoints.CreateHumanPose33JointList();

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(BodyJoints.RightFootIndex, BodyJoints.RightHeel, BodyJoints.RightAnkle, BodyJoints.RightFootIndex),
            new SkeletonLineStrip(BodyJoints.LeftFootIndex, BodyJoints.LeftHeel, BodyJoints.LeftAnkle, BodyJoints.LeftFootIndex),
            new SkeletonLineStrip(BodyJoints.RightAnkle, BodyJoints.RightKnee, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.LeftAnkle, BodyJoints.LeftKnee, BodyJoints.LeftHip),
            new SkeletonLineStrip(BodyJoints.RightHip, BodyJoints.LeftHip, BodyJoints.LeftShoulder, BodyJoints.RightShoulder, BodyJoints.RightHip),
            new SkeletonLineStrip(BodyJoints.RightShoulder, BodyJoints.RightElbow, BodyJoints.RightWrist, BodyJoints.RightThumb),
            new SkeletonLineStrip(BodyJoints.LeftShoulder, BodyJoints.LeftElbow, BodyJoints.LeftWrist, BodyJoints.LeftThumb),
            new SkeletonLineStrip(BodyJoints.RightWrist, BodyJoints.RightPinky, BodyJoints.RightIndex, BodyJoints.RightWrist),
            new SkeletonLineStrip(BodyJoints.LeftWrist, BodyJoints.LeftPinky, BodyJoints.LeftIndex, BodyJoints.LeftWrist),
            new SkeletonLineStrip(BodyJoints.MouthRight, BodyJoints.MouthLeft),
            new SkeletonLineStrip(BodyJoints.RightEar, BodyJoints.RightEye, BodyJoints.Nose, BodyJoints.LeftEye, BodyJoints.LeftEar),
        };

        public const int JointCount = 33;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "humanpose.33",
                "HumanPoseSkeleton33",
                JointList,
                DebugLineStrips,
                new NoseEarsHeadPoseProvider(BodyJoints.Nose, BodyJoints.RightEar, BodyJoints.LeftEar),
                BodyJoints.CreateHumanPose33MirrorPairs());

        public static IReadOnlyList<SkeletonJointId> Joints => JointList;
    }

    internal sealed class NoseEarsHeadPoseProvider : ISkeletonHeadPoseProvider
    {
        private readonly SkeletonJointId nose;
        private readonly SkeletonJointId rightEar;
        private readonly SkeletonJointId leftEar;

        public NoseEarsHeadPoseProvider(
            SkeletonJointId nose,
            SkeletonJointId rightEar,
            SkeletonJointId leftEar)
        {
            this.nose = nose;
            this.rightEar = rightEar;
            this.leftEar = leftEar;
        }

        public bool TryGetHeadPose(
            SkeletonDefinition definition,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<bool> tracked,
            out SkeletonHeadPose headPose)
        {
            headPose = default;

            if (!TryGetTrackedPosition(definition, positions, tracked, nose, out var nosePosition) ||
                !TryGetTrackedPosition(definition, positions, tracked, rightEar, out var rightEarPosition) ||
                !TryGetTrackedPosition(definition, positions, tracked, leftEar, out var leftEarPosition))
            {
                return false;
            }

            var up = Vector3.Scale(
                new Vector3(.1f, 1f, .1f),
                GetNormal(nosePosition, rightEarPosition, leftEarPosition)).normalized;
            var right = Vector3.Scale(
                new Vector3(1f, .1f, 1f),
                rightEarPosition - leftEarPosition).normalized;
            var forward = Vector3.Cross(right, up).normalized;

            if (up.sqrMagnitude <= 0.0001f ||
                right.sqrMagnitude <= 0.0001f ||
                forward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            headPose = new SkeletonHeadPose(
                (rightEarPosition + leftEarPosition) / 2f,
                forward,
                up);
            return true;
        }

        private static bool TryGetTrackedPosition(
            SkeletonDefinition definition,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<bool> tracked,
            SkeletonJointId joint,
            out Vector3 position)
        {
            var index = definition.IndexOf(joint);
            if (!definition.IsValidIndex(index) || !tracked[index])
            {
                position = default;
                return false;
            }

            position = positions[index];
            return true;
        }

        private static Vector3 GetNormal(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var u = p2 - p1;
            var v = p3 - p1;
            var n = new Vector3(
                (u.y * v.z - u.z * v.y),
                (u.z * v.x - u.x * v.z),
                (u.x * v.y - u.y * v.x));
            return n.normalized;
        }
    }
}
