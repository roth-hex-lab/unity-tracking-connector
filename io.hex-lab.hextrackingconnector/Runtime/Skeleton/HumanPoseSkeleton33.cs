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

    public enum SkeletonJoint
    {
        Nose = 0,
        LeftEyeInner = 1,
        LeftEye = 2,
        LeftEyeOuter = 3,
        RightEyeInner = 4,
        RightEye = 5,
        RightEyeOuter = 6,
        LeftEar = 7,
        RightEar = 8,
        MouthLeft = 9,
        MouthRight = 10,
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftPinky = 17,
        RightPinky = 18,
        LeftIndex = 19,
        RightIndex = 20,
        LeftThumb = 21,
        RightThumb = 22,
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
        LeftHeel = 29,
        RightHeel = 30,
        LeftFootIndex = 31,
        RightFootIndex = 32,
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
        private readonly Dictionary<SkeletonJointId, int> jointIndices;
        private readonly ISkeletonHeadPoseProvider headPoseProvider;

        public SkeletonDefinition(
            string name,
            SkeletonJoint[] joints)
            : this(name, name, ToJointIds(joints), null, null)
        {
        }

        public SkeletonDefinition(
            string name,
            SkeletonJointId[] joints)
            : this(name, name, joints, null, null)
        {
        }

        public SkeletonDefinition(
            string id,
            string name,
            SkeletonJointId[] joints,
            SkeletonLineStrip[] debugLineStrips = null,
            ISkeletonHeadPoseProvider headPoseProvider = null)
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
            jointIndices = BuildIndex(this.joints);
            ValidateDebugLineStrips(this.debugLineStrips, jointIndices);
            this.headPoseProvider = headPoseProvider;

            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public int JointCount => joints.Length;
        public IReadOnlyList<SkeletonJointId> Joints => joints;
        public IReadOnlyList<SkeletonLineStrip> DebugLineStrips => debugLineStrips;
        public bool HasHeadPose => headPoseProvider != null;

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < joints.Length;
        }

        public bool Contains(SkeletonJointId joint)
        {
            return jointIndices.ContainsKey(joint);
        }

        public bool Contains(SkeletonJoint joint)
        {
            return Contains(HumanPoseSkeleton33.ToJointId(joint));
        }

        public int IndexOf(SkeletonJointId joint)
        {
            return jointIndices.TryGetValue(joint, out var index) ? index : -1;
        }

        public int IndexOf(SkeletonJoint joint)
        {
            return IndexOf(HumanPoseSkeleton33.ToJointId(joint));
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

        private static SkeletonJointId[] ToJointIds(SkeletonJoint[] joints)
        {
            if (joints == null)
            {
                throw new ArgumentNullException(nameof(joints));
            }

            var jointIds = new SkeletonJointId[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                jointIds[i] = HumanPoseSkeleton33.ToJointId(joints[i]);
            }

            return jointIds;
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
    }

    public static class HumanPoseSkeleton33
    {
        public static readonly SkeletonJointId Nose = new SkeletonJointId(nameof(SkeletonJoint.Nose));
        public static readonly SkeletonJointId LeftEyeInner = new SkeletonJointId(nameof(SkeletonJoint.LeftEyeInner));
        public static readonly SkeletonJointId LeftEye = new SkeletonJointId(nameof(SkeletonJoint.LeftEye));
        public static readonly SkeletonJointId LeftEyeOuter = new SkeletonJointId(nameof(SkeletonJoint.LeftEyeOuter));
        public static readonly SkeletonJointId RightEyeInner = new SkeletonJointId(nameof(SkeletonJoint.RightEyeInner));
        public static readonly SkeletonJointId RightEye = new SkeletonJointId(nameof(SkeletonJoint.RightEye));
        public static readonly SkeletonJointId RightEyeOuter = new SkeletonJointId(nameof(SkeletonJoint.RightEyeOuter));
        public static readonly SkeletonJointId LeftEar = new SkeletonJointId(nameof(SkeletonJoint.LeftEar));
        public static readonly SkeletonJointId RightEar = new SkeletonJointId(nameof(SkeletonJoint.RightEar));
        public static readonly SkeletonJointId MouthLeft = new SkeletonJointId(nameof(SkeletonJoint.MouthLeft));
        public static readonly SkeletonJointId MouthRight = new SkeletonJointId(nameof(SkeletonJoint.MouthRight));
        public static readonly SkeletonJointId LeftShoulder = new SkeletonJointId(nameof(SkeletonJoint.LeftShoulder));
        public static readonly SkeletonJointId RightShoulder = new SkeletonJointId(nameof(SkeletonJoint.RightShoulder));
        public static readonly SkeletonJointId LeftElbow = new SkeletonJointId(nameof(SkeletonJoint.LeftElbow));
        public static readonly SkeletonJointId RightElbow = new SkeletonJointId(nameof(SkeletonJoint.RightElbow));
        public static readonly SkeletonJointId LeftWrist = new SkeletonJointId(nameof(SkeletonJoint.LeftWrist));
        public static readonly SkeletonJointId RightWrist = new SkeletonJointId(nameof(SkeletonJoint.RightWrist));
        public static readonly SkeletonJointId LeftPinky = new SkeletonJointId(nameof(SkeletonJoint.LeftPinky));
        public static readonly SkeletonJointId RightPinky = new SkeletonJointId(nameof(SkeletonJoint.RightPinky));
        public static readonly SkeletonJointId LeftIndex = new SkeletonJointId(nameof(SkeletonJoint.LeftIndex));
        public static readonly SkeletonJointId RightIndex = new SkeletonJointId(nameof(SkeletonJoint.RightIndex));
        public static readonly SkeletonJointId LeftThumb = new SkeletonJointId(nameof(SkeletonJoint.LeftThumb));
        public static readonly SkeletonJointId RightThumb = new SkeletonJointId(nameof(SkeletonJoint.RightThumb));
        public static readonly SkeletonJointId LeftHip = new SkeletonJointId(nameof(SkeletonJoint.LeftHip));
        public static readonly SkeletonJointId RightHip = new SkeletonJointId(nameof(SkeletonJoint.RightHip));
        public static readonly SkeletonJointId LeftKnee = new SkeletonJointId(nameof(SkeletonJoint.LeftKnee));
        public static readonly SkeletonJointId RightKnee = new SkeletonJointId(nameof(SkeletonJoint.RightKnee));
        public static readonly SkeletonJointId LeftAnkle = new SkeletonJointId(nameof(SkeletonJoint.LeftAnkle));
        public static readonly SkeletonJointId RightAnkle = new SkeletonJointId(nameof(SkeletonJoint.RightAnkle));
        public static readonly SkeletonJointId LeftHeel = new SkeletonJointId(nameof(SkeletonJoint.LeftHeel));
        public static readonly SkeletonJointId RightHeel = new SkeletonJointId(nameof(SkeletonJoint.RightHeel));
        public static readonly SkeletonJointId LeftFootIndex = new SkeletonJointId(nameof(SkeletonJoint.LeftFootIndex));
        public static readonly SkeletonJointId RightFootIndex = new SkeletonJointId(nameof(SkeletonJoint.RightFootIndex));

        private static readonly SkeletonJointId[] JointList =
        {
            Nose,
            LeftEyeInner,
            LeftEye,
            LeftEyeOuter,
            RightEyeInner,
            RightEye,
            RightEyeOuter,
            LeftEar,
            RightEar,
            MouthLeft,
            MouthRight,
            LeftShoulder,
            RightShoulder,
            LeftElbow,
            RightElbow,
            LeftWrist,
            RightWrist,
            LeftPinky,
            RightPinky,
            LeftIndex,
            RightIndex,
            LeftThumb,
            RightThumb,
            LeftHip,
            RightHip,
            LeftKnee,
            RightKnee,
            LeftAnkle,
            RightAnkle,
            LeftHeel,
            RightHeel,
            LeftFootIndex,
            RightFootIndex,
        };

        private static readonly SkeletonLineStrip[] DebugLineStrips =
        {
            new SkeletonLineStrip(RightFootIndex, RightHeel, RightAnkle, RightFootIndex),
            new SkeletonLineStrip(LeftFootIndex, LeftHeel, LeftAnkle, LeftFootIndex),
            new SkeletonLineStrip(RightAnkle, RightKnee, RightHip),
            new SkeletonLineStrip(LeftAnkle, LeftKnee, LeftHip),
            new SkeletonLineStrip(RightHip, LeftHip, LeftShoulder, RightShoulder, RightHip),
            new SkeletonLineStrip(RightShoulder, RightElbow, RightWrist, RightThumb),
            new SkeletonLineStrip(LeftShoulder, LeftElbow, LeftWrist, LeftThumb),
            new SkeletonLineStrip(RightWrist, RightPinky, RightIndex, RightWrist),
            new SkeletonLineStrip(LeftWrist, LeftPinky, LeftIndex, LeftWrist),
            new SkeletonLineStrip(MouthRight, MouthLeft),
            new SkeletonLineStrip(RightEar, RightEye, Nose, LeftEye, LeftEar),
        };

        public const int JointCount = 33;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition(
                "humanpose.33",
                "HumanPoseSkeleton33",
                JointList,
                DebugLineStrips,
                new NoseEarsHeadPoseProvider(Nose, RightEar, LeftEar));

        public static IReadOnlyList<SkeletonJointId> Joints => JointList;

        public static SkeletonJointId ToJointId(SkeletonJoint joint)
        {
            var index = (int)joint;
            if (index < 0 || index >= JointList.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(joint));
            }

            return JointList[index];
        }
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
