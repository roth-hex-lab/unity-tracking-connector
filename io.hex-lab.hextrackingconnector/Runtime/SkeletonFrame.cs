using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public readonly struct SkeletonFrame
    {
        private static readonly Vector3[] EmptyPositions = new Vector3[JointCount];
        private static readonly bool[] EmptyTracked = new bool[JointCount];

        private readonly SkeletonDefinition definition;
        private readonly Vector3[] positions;
        private readonly bool[] tracked;

        public const int JointCount = HumanPoseSkeleton33.JointCount;

        public SkeletonFrame(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            int sequenceNumber,
            double receivedTime)
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

            this.definition = definition;
            this.positions = (Vector3[])positions.Clone();
            this.tracked = (bool[])tracked.Clone();
            SequenceNumber = sequenceNumber;
            ReceivedTime = receivedTime;
        }

        public SkeletonDefinition Definition => definition ?? HumanPoseSkeleton33.Definition;
        public int SequenceNumber { get; }
        public double ReceivedTime { get; }
        public IReadOnlyList<Vector3> Positions => positions ?? EmptyPositions;

        public SkeletonPoint this[SkeletonJoint joint] => GetPoint(joint);

        public SkeletonPoint Nose => this[SkeletonJoint.Nose];
        public SkeletonPoint LeftEyeInner => this[SkeletonJoint.LeftEyeInner];
        public SkeletonPoint LeftEye => this[SkeletonJoint.LeftEye];
        public SkeletonPoint LeftEyeOuter => this[SkeletonJoint.LeftEyeOuter];
        public SkeletonPoint RightEyeInner => this[SkeletonJoint.RightEyeInner];
        public SkeletonPoint RightEye => this[SkeletonJoint.RightEye];
        public SkeletonPoint RightEyeOuter => this[SkeletonJoint.RightEyeOuter];
        public SkeletonPoint LeftEar => this[SkeletonJoint.LeftEar];
        public SkeletonPoint RightEar => this[SkeletonJoint.RightEar];
        public SkeletonPoint MouthLeft => this[SkeletonJoint.MouthLeft];
        public SkeletonPoint MouthRight => this[SkeletonJoint.MouthRight];
        public SkeletonPoint LeftShoulder => this[SkeletonJoint.LeftShoulder];
        public SkeletonPoint RightShoulder => this[SkeletonJoint.RightShoulder];
        public SkeletonPoint LeftElbow => this[SkeletonJoint.LeftElbow];
        public SkeletonPoint RightElbow => this[SkeletonJoint.RightElbow];
        public SkeletonPoint LeftWrist => this[SkeletonJoint.LeftWrist];
        public SkeletonPoint RightWrist => this[SkeletonJoint.RightWrist];
        public SkeletonPoint LeftPinky => this[SkeletonJoint.LeftPinky];
        public SkeletonPoint RightPinky => this[SkeletonJoint.RightPinky];
        public SkeletonPoint LeftIndex => this[SkeletonJoint.LeftIndex];
        public SkeletonPoint RightIndex => this[SkeletonJoint.RightIndex];
        public SkeletonPoint LeftThumb => this[SkeletonJoint.LeftThumb];
        public SkeletonPoint RightThumb => this[SkeletonJoint.RightThumb];
        public SkeletonPoint LeftHip => this[SkeletonJoint.LeftHip];
        public SkeletonPoint RightHip => this[SkeletonJoint.RightHip];
        public SkeletonPoint LeftKnee => this[SkeletonJoint.LeftKnee];
        public SkeletonPoint RightKnee => this[SkeletonJoint.RightKnee];
        public SkeletonPoint LeftAnkle => this[SkeletonJoint.LeftAnkle];
        public SkeletonPoint RightAnkle => this[SkeletonJoint.RightAnkle];
        public SkeletonPoint LeftHeel => this[SkeletonJoint.LeftHeel];
        public SkeletonPoint RightHeel => this[SkeletonJoint.RightHeel];
        public SkeletonPoint LeftFootIndex => this[SkeletonJoint.LeftFootIndex];
        public SkeletonPoint RightFootIndex => this[SkeletonJoint.RightFootIndex];

        public bool IsTracked(SkeletonJoint joint)
        {
            return IsTracked(Definition.IndexOf(joint));
        }

        public bool IsTracked(int index)
        {
            return Definition.IsValidIndex(index) && (tracked ?? EmptyTracked)[index];
        }

        public Vector3 GetJoint(SkeletonJoint joint)
        {
            return GetJoint(Definition.IndexOf(joint));
        }

        public Vector3 GetJoint(int index)
        {
            if (!Definition.IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (positions ?? EmptyPositions)[index];
        }

        public SkeletonPoint GetPoint(SkeletonJoint joint)
        {
            var index = Definition.IndexOf(joint);
            if (!Definition.IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(nameof(joint));
            }

            return new SkeletonPoint(GetJoint(index), IsTracked(index));
        }

        public bool TryGetJoint(SkeletonJoint joint, out Vector3 position)
        {
            return TryGetJoint(Definition.IndexOf(joint), out position);
        }

        public bool TryGetJoint(int index, out Vector3 position)
        {
            if (!IsTracked(index))
            {
                position = default;
                return false;
            }

            position = GetJoint(index);
            return true;
        }

        public Vector3[] CopyPositions()
        {
            return (Vector3[])(positions ?? EmptyPositions).Clone();
        }

        public bool[] CopyTracked()
        {
            return (bool[])(tracked ?? EmptyTracked).Clone();
        }
    }
}
