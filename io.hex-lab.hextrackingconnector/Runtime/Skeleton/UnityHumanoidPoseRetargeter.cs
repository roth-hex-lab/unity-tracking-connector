using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class UnityHumanoidPoseRetargeter
    {
        private const float InferredRotationConfidence = 0.6f;
        private const float InferredBodyConfidence = 0.7f;
        private const float InferredHandConfidence = 0.55f;

        public static bool TryCreateFrom(SkeletonFrame source, out SkeletonFrame frame)
        {
            if (string.Equals(source.Definition.Id, UnityHumanoidControlSkeleton.Definition.Id, StringComparison.Ordinal))
            {
                frame = source;
                return true;
            }

            var poses = CreateRestPoseBuffer();
            var hasAnySource = false;
            var bodyRotation = Quaternion.identity;
            var hipCentre = Vector3.zero;
            var shoulderCentre = Vector3.up;
            var bodyForward = Vector3.forward;

            if (TryGetBodyBasis(source, out hipCentre, out shoulderCentre, out bodyRotation, out bodyForward))
            {
                hasAnySource = true;
                const string bodyBasisSource = "LeftHip+RightHip+LeftShoulder+RightShoulder";
                SetPositionAndRotation(
                    poses,
                    UnityHumanoidControlSkeleton.Hips,
                    hipCentre,
                    bodyRotation,
                    InferredBodyConfidence,
                    bodyBasisSource);
                SetPositionAndRotation(
                    poses,
                    UnityHumanoidControlSkeleton.Spine,
                    Vector3.Lerp(hipCentre, shoulderCentre, 0.35f),
                    bodyRotation,
                    InferredBodyConfidence,
                    bodyBasisSource);
                SetPositionAndRotation(
                    poses,
                    UnityHumanoidControlSkeleton.Chest,
                    Vector3.Lerp(hipCentre, shoulderCentre, 0.75f),
                    bodyRotation,
                    InferredBodyConfidence,
                    bodyBasisSource);
                SetPositionAndRotation(
                    poses,
                    UnityHumanoidControlSkeleton.UpperChest,
                    shoulderCentre,
                    bodyRotation,
                    InferredBodyConfidence,
                    bodyBasisSource);
                SetPositionAndRotation(
                    poses,
                    UnityHumanoidControlSkeleton.Neck,
                    shoulderCentre,
                    bodyRotation,
                    InferredBodyConfidence,
                    bodyBasisSource);
            }

            if (source.TryGetHeadPose(out var headPose))
            {
                hasAnySource = true;
                SetPose(
                    poses,
                    UnityHumanoidControlSkeleton.Head,
                    SkeletonJointPose.FromPositionAndRotation(
                        headPose.Position,
                        Quaternion.LookRotation(headPose.Forward, headPose.Up),
                        InferredBodyConfidence,
                        SkeletonDataProvenance.Inferred,
                        "head-pose-provider"));
            }
            else if (TryCopyPosition(source, HumanPoseSkeleton33.Nose, poses, UnityHumanoidControlSkeleton.Head))
            {
                SetRotation(poses, UnityHumanoidControlSkeleton.Head, bodyRotation, InferredBodyConfidence, "body-basis");
                hasAnySource = true;
            }

            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.LeftShoulder,
                HumanPoseSkeleton33.LeftElbow,
                UnityHumanoidControlSkeleton.LeftUpperArm,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.RightShoulder,
                HumanPoseSkeleton33.RightElbow,
                UnityHumanoidControlSkeleton.RightUpperArm,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.LeftElbow,
                HumanPoseSkeleton33.LeftWrist,
                UnityHumanoidControlSkeleton.LeftLowerArm,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.RightElbow,
                HumanPoseSkeleton33.RightWrist,
                UnityHumanoidControlSkeleton.RightLowerArm,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.LeftHip,
                HumanPoseSkeleton33.LeftKnee,
                UnityHumanoidControlSkeleton.LeftUpperLeg,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.RightHip,
                HumanPoseSkeleton33.RightKnee,
                UnityHumanoidControlSkeleton.RightUpperLeg,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.LeftKnee,
                HumanPoseSkeleton33.LeftAnkle,
                UnityHumanoidControlSkeleton.LeftLowerLeg,
                bodyForward,
                poses);
            hasAnySource |= RetargetLimb(
                source,
                HumanPoseSkeleton33.RightKnee,
                HumanPoseSkeleton33.RightAnkle,
                UnityHumanoidControlSkeleton.RightLowerLeg,
                bodyForward,
                poses);

            hasAnySource |= RetargetFoot(
                source,
                HumanPoseSkeleton33.LeftAnkle,
                HumanPoseSkeleton33.LeftFootIndex,
                UnityHumanoidControlSkeleton.LeftFoot,
                UnityHumanoidControlSkeleton.LeftToes,
                bodyForward,
                poses);
            hasAnySource |= RetargetFoot(
                source,
                HumanPoseSkeleton33.RightAnkle,
                HumanPoseSkeleton33.RightFootIndex,
                UnityHumanoidControlSkeleton.RightFoot,
                UnityHumanoidControlSkeleton.RightToes,
                bodyForward,
                poses);

            hasAnySource |= TryCopyPosition(source, HumanPoseSkeleton33.LeftShoulder, poses, UnityHumanoidControlSkeleton.LeftShoulder);
            hasAnySource |= TryCopyPosition(source, HumanPoseSkeleton33.RightShoulder, poses, UnityHumanoidControlSkeleton.RightShoulder);
            hasAnySource |= RetargetHand(
                source,
                HumanPoseSkeleton33.LeftWrist,
                HumanPoseSkeleton33.LeftIndex,
                HumanPoseSkeleton33.LeftPinky,
                HumanPoseSkeleton33.LeftThumb,
                UnityHumanoidControlSkeleton.LeftHand,
                bodyForward,
                poses);
            hasAnySource |= RetargetHand(
                source,
                HumanPoseSkeleton33.RightWrist,
                HumanPoseSkeleton33.RightIndex,
                HumanPoseSkeleton33.RightPinky,
                HumanPoseSkeleton33.RightThumb,
                UnityHumanoidControlSkeleton.RightHand,
                bodyForward,
                poses);
            hasAnySource |= TryCopyPositionIfMissing(source, HumanPoseSkeleton33.LeftWrist, poses, UnityHumanoidControlSkeleton.LeftHand);
            hasAnySource |= TryCopyPositionIfMissing(source, HumanPoseSkeleton33.RightWrist, poses, UnityHumanoidControlSkeleton.RightHand);

            if (!hasAnySource)
            {
                frame = default;
                return false;
            }

            frame = new SkeletonFrame(
                new SkeletonPose(
                    UnityHumanoidControlSkeleton.Definition,
                    poses,
                    SkeletonCoordinateSpace.RootRelative),
                source.Metadata);
            return true;
        }

        private static SkeletonJointPose[] CreateRestPoseBuffer()
        {
            var poses = new SkeletonJointPose[UnityHumanoidControlSkeleton.Definition.JointCount];
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = SkeletonJointPose.Unavailable;
            }

            return poses;
        }

        private static bool TryGetBodyBasis(
            SkeletonFrame source,
            out Vector3 hipCentre,
            out Vector3 shoulderCentre,
            out Quaternion rotation,
            out Vector3 forward)
        {
            rotation = Quaternion.identity;
            forward = Vector3.forward;

            if (!TryGetMidpoint(source, HumanPoseSkeleton33.LeftHip, HumanPoseSkeleton33.RightHip, out hipCentre) ||
                !TryGetMidpoint(source, HumanPoseSkeleton33.LeftShoulder, HumanPoseSkeleton33.RightShoulder, out shoulderCentre) ||
                !source.TryGetJoint(HumanPoseSkeleton33.LeftHip, out var leftHip) ||
                !source.TryGetJoint(HumanPoseSkeleton33.RightHip, out var rightHip))
            {
                hipCentre = default;
                shoulderCentre = default;
                return false;
            }

            var right = rightHip - leftHip;
            var up = shoulderCentre - hipCentre;
            if (!TryCreateBasis(right, up, out rotation, out forward))
            {
                return false;
            }

            return true;
        }

        private static bool TryGetMidpoint(
            SkeletonFrame source,
            SkeletonJointId a,
            SkeletonJointId b,
            out Vector3 midpoint)
        {
            if (!source.TryGetJoint(a, out var first) ||
                !source.TryGetJoint(b, out var second))
            {
                midpoint = default;
                return false;
            }

            midpoint = (first + second) / 2f;
            return true;
        }

        private static bool RetargetLimb(
            SkeletonFrame source,
            SkeletonJointId sourceStart,
            SkeletonJointId sourceEnd,
            SkeletonJointId target,
            Vector3 referenceForward,
            SkeletonJointPose[] poses)
        {
            if (!source.TryGetJoint(sourceStart, out var start) ||
                !source.TryGetJoint(sourceEnd, out var end))
            {
                return false;
            }

            var rotation = CreateSegmentRotation(end - start, referenceForward);
            SetPositionAndRotation(
                poses,
                target,
                start,
                rotation,
                InferredRotationConfidence,
                FormatSegmentSource(sourceStart, sourceEnd));
            return true;
        }

        private static bool RetargetFoot(
            SkeletonFrame source,
            SkeletonJointId sourceAnkle,
            SkeletonJointId sourceToe,
            SkeletonJointId targetFoot,
            SkeletonJointId targetToe,
            Vector3 referenceForward,
            SkeletonJointPose[] poses)
        {
            if (!source.TryGetJoint(sourceAnkle, out var ankle) ||
                !source.TryGetJoint(sourceToe, out var toe))
            {
                return false;
            }

            var rotation = CreateSegmentRotation(toe - ankle, referenceForward);
            var sourceLabel = FormatSegmentSource(sourceAnkle, sourceToe);
            SetPositionAndRotation(poses, targetFoot, ankle, rotation, InferredRotationConfidence, sourceLabel);
            SetPositionAndRotation(poses, targetToe, toe, rotation, InferredRotationConfidence, sourceLabel);
            return true;
        }

        private static bool RetargetHand(
            SkeletonFrame source,
            SkeletonJointId sourceWrist,
            SkeletonJointId sourceIndex,
            SkeletonJointId sourcePinky,
            SkeletonJointId sourceThumb,
            SkeletonJointId targetHand,
            Vector3 referenceForward,
            SkeletonJointPose[] poses)
        {
            if (!source.TryGetJoint(sourceWrist, out var wrist) ||
                !source.TryGetJoint(sourceIndex, out var index) ||
                !source.TryGetJoint(sourcePinky, out var pinky))
            {
                return false;
            }

            var fingerDirection = ((index + pinky) / 2f) - wrist;
            var acrossPalm = index - pinky;
            if (!TryCreateHandRotation(
                    fingerDirection,
                    acrossPalm,
                    source.TryGetJoint(sourceThumb, out var thumb) ? thumb - wrist : Vector3.zero,
                    referenceForward,
                    out var rotation))
            {
                return false;
            }

            var sourceLabel = source.TryGetJoint(sourceThumb, out _)
                ? $"{sourceWrist.Name}+{sourceIndex.Name}+{sourcePinky.Name}+{sourceThumb.Name}"
                : $"{sourceWrist.Name}+{sourceIndex.Name}+{sourcePinky.Name}";
            SetPositionAndRotation(
                poses,
                targetHand,
                wrist,
                rotation,
                Mathf.Min(InferredHandConfidence, GetMinimumConfidence(source, sourceWrist, sourceIndex, sourcePinky, sourceThumb)),
                sourceLabel);
            return true;
        }

        private static bool TryCopyPositionIfMissing(
            SkeletonFrame source,
            SkeletonJointId sourceJoint,
            SkeletonJointPose[] poses,
            SkeletonJointId targetJoint)
        {
            var targetIndex = UnityHumanoidControlSkeleton.Definition.IndexOf(targetJoint);
            if (UnityHumanoidControlSkeleton.Definition.IsValidIndex(targetIndex) &&
                poses[targetIndex].HasPosition)
            {
                return false;
            }

            return TryCopyPosition(source, sourceJoint, poses, targetJoint);
        }

        private static bool TryCopyPosition(
            SkeletonFrame source,
            SkeletonJointId sourceJoint,
            SkeletonJointPose[] poses,
            SkeletonJointId targetJoint)
        {
            if (!source.TryGetJoint(sourceJoint, out var position))
            {
                return false;
            }

            var targetIndex = UnityHumanoidControlSkeleton.Definition.IndexOf(targetJoint);
            var existing = UnityHumanoidControlSkeleton.Definition.IsValidIndex(targetIndex)
                ? poses[targetIndex]
                : SkeletonJointPose.Unavailable;
            var channels = SkeletonJointChannels.Position | SkeletonJointChannels.Confidence;
            if (existing.HasRotation)
            {
                channels |= SkeletonJointChannels.Rotation;
            }

            var confidence = existing.HasConfidence ? existing.Confidence : GetConfidence(source, sourceJoint);
            var provenance = existing.HasRotation ? existing.Provenance : SkeletonDataProvenance.Direct;
            var sourceLabel = existing.HasRotation && !string.IsNullOrEmpty(existing.Source)
                ? existing.Source
                : GetSourceLabel(source, sourceJoint);
            SetPose(
                poses,
                targetJoint,
                new SkeletonJointPose(
                    channels,
                    position,
                    existing.HasRotation ? existing.Rotation : Quaternion.identity,
                    confidence,
                    provenance,
                    sourceLabel));
            return true;
        }

        private static bool TryCreateBasis(
            Vector3 right,
            Vector3 up,
            out Quaternion rotation,
            out Vector3 forward)
        {
            rotation = Quaternion.identity;
            forward = Vector3.forward;

            if (right.sqrMagnitude <= 0.0001f || up.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            right.Normalize();
            up.Normalize();
            forward = Vector3.Cross(right, up).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            rotation = Quaternion.LookRotation(forward, up);
            return true;
        }

        private static Quaternion CreateSegmentRotation(Vector3 direction, Vector3 referenceForward)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            var up = direction.normalized;
            var forward = Vector3.ProjectOnPlane(referenceForward, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        private static bool TryCreateHandRotation(
            Vector3 fingerDirection,
            Vector3 acrossPalm,
            Vector3 thumbDirection,
            Vector3 referenceForward,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (fingerDirection.sqrMagnitude <= 0.0001f || acrossPalm.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var up = fingerDirection.normalized;
            var forward = Vector3.Cross(acrossPalm.normalized, up).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(referenceForward, up).normalized;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (thumbDirection.sqrMagnitude > 0.0001f && Vector3.Dot(forward, thumbDirection) < 0f)
            {
                forward = -forward;
            }

            rotation = Quaternion.LookRotation(forward, up);
            return true;
        }

        private static void SetPositionAndRotation(
            SkeletonJointPose[] poses,
            SkeletonJointId joint,
            Vector3 position,
            Quaternion rotation,
            float confidence,
            string source)
        {
            SetPose(
                poses,
                joint,
                SkeletonJointPose.FromPositionAndRotation(
                    position,
                    rotation,
                    confidence,
                    SkeletonDataProvenance.Inferred,
                    source));
        }

        private static void SetRotation(
            SkeletonJointPose[] poses,
            SkeletonJointId joint,
            Quaternion rotation,
            float confidence,
            string source)
        {
            SetPose(
                poses,
                joint,
                SkeletonJointPose.FromRotation(
                    rotation,
                    confidence,
                    SkeletonDataProvenance.Inferred,
                    source));
        }

        private static void SetPose(
            SkeletonJointPose[] poses,
            SkeletonJointId joint,
            SkeletonJointPose pose)
        {
            var index = UnityHumanoidControlSkeleton.Definition.IndexOf(joint);
            if (UnityHumanoidControlSkeleton.Definition.IsValidIndex(index))
            {
                poses[index] = pose;
            }
        }

        private static string GetSourceLabel(SkeletonFrame source, SkeletonJointId joint)
        {
            return source.TryGetJointPose(joint, out var jointPose) &&
                   !string.IsNullOrEmpty(jointPose.Source)
                ? jointPose.Source
                : joint.Name;
        }

        private static float GetConfidence(SkeletonFrame source, SkeletonJointId joint)
        {
            return source.TryGetJointPose(joint, out var jointPose) && jointPose.HasConfidence
                ? jointPose.Confidence
                : 1f;
        }

        private static float GetMinimumConfidence(SkeletonFrame source, params SkeletonJointId[] joints)
        {
            var confidence = 1f;
            foreach (var joint in joints)
            {
                if (source.TryGetJointPose(joint, out var jointPose) && jointPose.HasConfidence)
                {
                    confidence = Mathf.Min(confidence, jointPose.Confidence);
                }
            }

            return confidence;
        }

        private static string FormatSegmentSource(SkeletonJointId start, SkeletonJointId end)
        {
            return $"{start.Name}->{end.Name}";
        }
    }
}
