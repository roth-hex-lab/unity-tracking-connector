using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public readonly struct SkeletonFrame
    {
        private readonly SkeletonPose pose;
        private readonly SkeletonFrameMetadata metadata;

        public SkeletonFrame(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            int sequenceNumber,
            double receivedTime)
            : this(
                new SkeletonPose(definition, positions, tracked),
                new SkeletonFrameMetadata(sequenceNumber, receivedTime))
        {
        }

        public SkeletonFrame(
            SkeletonDefinition definition,
            Vector3[] positions,
            bool[] tracked,
            int sequenceNumber,
            double receivedTime,
            SkeletonCoordinateSpace coordinateSpace,
            string sourceId = null,
            double sourceTimestamp = 0.0)
            : this(
                new SkeletonPose(definition, positions, tracked, coordinateSpace),
                new SkeletonFrameMetadata(sequenceNumber, receivedTime, sourceTimestamp, sourceId))
        {
        }

        public SkeletonFrame(SkeletonPose pose, SkeletonFrameMetadata metadata)
        {
            if (pose.Definition == null)
            {
                throw new ArgumentException("A skeleton frame needs a pose definition.", nameof(pose));
            }

            this.pose = pose;
            this.metadata = metadata;
        }

        public SkeletonPose Pose => pose.Definition == null ? new SkeletonPose(SkeletonDefinition.Empty, new SkeletonJointPose[0]) : pose;
        public SkeletonFrameMetadata Metadata => metadata;
        public SkeletonDefinition Definition => Pose.Definition;
        public int JointCount => Pose.JointCount;
        public int SequenceNumber => metadata.SequenceNumber;
        public double ReceivedTime => metadata.ReceivedTime;
        public SkeletonCoordinateSpace CoordinateSpace => Pose.CoordinateSpace;
        public IReadOnlyList<Vector3> Positions => Pose.Positions;
        public IReadOnlyList<SkeletonJointPose> JointPoses => Pose.JointPoses;

        public SkeletonJointPose this[SkeletonJointId joint] => GetPoint(joint);
        public SkeletonJointPose this[SkeletonJoint joint] => GetPoint(joint);

        public SkeletonJointPose Nose => this[SkeletonJoint.Nose];
        public SkeletonJointPose LeftEyeInner => this[SkeletonJoint.LeftEyeInner];
        public SkeletonJointPose LeftEye => this[SkeletonJoint.LeftEye];
        public SkeletonJointPose LeftEyeOuter => this[SkeletonJoint.LeftEyeOuter];
        public SkeletonJointPose RightEyeInner => this[SkeletonJoint.RightEyeInner];
        public SkeletonJointPose RightEye => this[SkeletonJoint.RightEye];
        public SkeletonJointPose RightEyeOuter => this[SkeletonJoint.RightEyeOuter];
        public SkeletonJointPose LeftEar => this[SkeletonJoint.LeftEar];
        public SkeletonJointPose RightEar => this[SkeletonJoint.RightEar];
        public SkeletonJointPose MouthLeft => this[SkeletonJoint.MouthLeft];
        public SkeletonJointPose MouthRight => this[SkeletonJoint.MouthRight];
        public SkeletonJointPose LeftShoulder => this[SkeletonJoint.LeftShoulder];
        public SkeletonJointPose RightShoulder => this[SkeletonJoint.RightShoulder];
        public SkeletonJointPose LeftElbow => this[SkeletonJoint.LeftElbow];
        public SkeletonJointPose RightElbow => this[SkeletonJoint.RightElbow];
        public SkeletonJointPose LeftWrist => this[SkeletonJoint.LeftWrist];
        public SkeletonJointPose RightWrist => this[SkeletonJoint.RightWrist];
        public SkeletonJointPose LeftPinky => this[SkeletonJoint.LeftPinky];
        public SkeletonJointPose RightPinky => this[SkeletonJoint.RightPinky];
        public SkeletonJointPose LeftIndex => this[SkeletonJoint.LeftIndex];
        public SkeletonJointPose RightIndex => this[SkeletonJoint.RightIndex];
        public SkeletonJointPose LeftThumb => this[SkeletonJoint.LeftThumb];
        public SkeletonJointPose RightThumb => this[SkeletonJoint.RightThumb];
        public SkeletonJointPose LeftHip => this[SkeletonJoint.LeftHip];
        public SkeletonJointPose RightHip => this[SkeletonJoint.RightHip];
        public SkeletonJointPose LeftKnee => this[SkeletonJoint.LeftKnee];
        public SkeletonJointPose RightKnee => this[SkeletonJoint.RightKnee];
        public SkeletonJointPose LeftAnkle => this[SkeletonJoint.LeftAnkle];
        public SkeletonJointPose RightAnkle => this[SkeletonJoint.RightAnkle];
        public SkeletonJointPose LeftHeel => this[SkeletonJoint.LeftHeel];
        public SkeletonJointPose RightHeel => this[SkeletonJoint.RightHeel];
        public SkeletonJointPose LeftFootIndex => this[SkeletonJoint.LeftFootIndex];
        public SkeletonJointPose RightFootIndex => this[SkeletonJoint.RightFootIndex];

        public bool IsTracked(SkeletonJoint joint)
        {
            return Pose.IsTracked(joint);
        }

        public bool IsTracked(SkeletonJointId joint)
        {
            return Pose.IsTracked(joint);
        }

        public bool IsTracked(int index)
        {
            return Pose.IsTracked(index);
        }

        public Vector3 GetJoint(SkeletonJoint joint)
        {
            return Pose.GetJoint(joint);
        }

        public Vector3 GetJoint(SkeletonJointId joint)
        {
            return Pose.GetJoint(joint);
        }

        public Vector3 GetJoint(int index)
        {
            return Pose.GetJoint(index);
        }

        public SkeletonJointPose GetPoint(SkeletonJoint joint)
        {
            return Pose.GetPoint(joint);
        }

        public SkeletonJointPose GetPoint(SkeletonJointId joint)
        {
            return Pose.GetPoint(joint);
        }

        public SkeletonJointPose GetPoint(int index)
        {
            return Pose.GetPoint(index);
        }

        public bool TryGetJointPose(SkeletonJointId joint, out SkeletonJointPose jointPose)
        {
            return Pose.TryGetJointPose(joint, out jointPose);
        }

        public bool TryGetJointPose(int index, out SkeletonJointPose jointPose)
        {
            return Pose.TryGetJointPose(index, out jointPose);
        }

        public bool TryGetJoint(SkeletonJoint joint, out Vector3 position)
        {
            return Pose.TryGetJoint(joint, out position);
        }

        public bool TryGetJoint(SkeletonJointId joint, out Vector3 position)
        {
            return Pose.TryGetJoint(joint, out position);
        }

        public bool TryGetJoint(int index, out Vector3 position)
        {
            return Pose.TryGetJoint(index, out position);
        }

        public bool TryGetRotation(SkeletonJointId joint, out Quaternion rotation)
        {
            return Pose.TryGetRotation(joint, out rotation);
        }

        public bool TryGetRotation(int index, out Quaternion rotation)
        {
            return Pose.TryGetRotation(index, out rotation);
        }

        public bool TryConvertTo(SkeletonDefinition targetDefinition, out SkeletonFrame convertedFrame)
        {
            return SkeletonFrameConversions.TryConvert(this, targetDefinition, out convertedFrame);
        }

        public bool TryGetHeadPose(out SkeletonHeadPose headPose)
        {
            return Pose.TryGetHeadPose(out headPose);
        }

        public Vector3[] CopyPositions()
        {
            return Pose.CopyPositions();
        }

        public bool[] CopyTracked()
        {
            return Pose.CopyTracked();
        }

        public SkeletonJointPose[] CopyJointPoses()
        {
            return Pose.CopyJointPoses();
        }
    }
}
