using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
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
