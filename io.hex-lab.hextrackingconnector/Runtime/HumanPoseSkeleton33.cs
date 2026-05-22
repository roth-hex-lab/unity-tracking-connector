using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
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

    public sealed class SkeletonDefinition
    {
        private readonly SkeletonJoint[] joints;

        public SkeletonDefinition(
            string name,
            SkeletonJoint[] joints)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A skeleton definition needs a name.", nameof(name));
            }

            this.joints = joints != null
                ? (SkeletonJoint[])joints.Clone()
                : throw new ArgumentNullException(nameof(joints));

            Name = name;
        }

        public string Name { get; }
        public int JointCount => joints.Length;
        public IReadOnlyList<SkeletonJoint> Joints => joints;

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < joints.Length;
        }

        public bool Contains(SkeletonJoint joint)
        {
            var index = (int)joint;
            return IsValidIndex(index) && joints[index] == joint;
        }

        public int IndexOf(SkeletonJoint joint)
        {
            return Contains(joint) ? (int)joint : -1;
        }

        public SkeletonJoint JointAt(int index)
        {
            if (!IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return joints[index];
        }
    }

    public static class HumanPoseSkeleton33
    {
        private static readonly SkeletonJoint[] JointList =
        {
            SkeletonJoint.Nose,
            SkeletonJoint.LeftEyeInner,
            SkeletonJoint.LeftEye,
            SkeletonJoint.LeftEyeOuter,
            SkeletonJoint.RightEyeInner,
            SkeletonJoint.RightEye,
            SkeletonJoint.RightEyeOuter,
            SkeletonJoint.LeftEar,
            SkeletonJoint.RightEar,
            SkeletonJoint.MouthLeft,
            SkeletonJoint.MouthRight,
            SkeletonJoint.LeftShoulder,
            SkeletonJoint.RightShoulder,
            SkeletonJoint.LeftElbow,
            SkeletonJoint.RightElbow,
            SkeletonJoint.LeftWrist,
            SkeletonJoint.RightWrist,
            SkeletonJoint.LeftPinky,
            SkeletonJoint.RightPinky,
            SkeletonJoint.LeftIndex,
            SkeletonJoint.RightIndex,
            SkeletonJoint.LeftThumb,
            SkeletonJoint.RightThumb,
            SkeletonJoint.LeftHip,
            SkeletonJoint.RightHip,
            SkeletonJoint.LeftKnee,
            SkeletonJoint.RightKnee,
            SkeletonJoint.LeftAnkle,
            SkeletonJoint.RightAnkle,
            SkeletonJoint.LeftHeel,
            SkeletonJoint.RightHeel,
            SkeletonJoint.LeftFootIndex,
            SkeletonJoint.RightFootIndex,
        };

        public const int JointCount = 33;
        public static readonly SkeletonDefinition Definition =
            new SkeletonDefinition("HumanPoseSkeleton33", JointList);

        public static IReadOnlyList<SkeletonJoint> Joints => JointList;
    }
}
