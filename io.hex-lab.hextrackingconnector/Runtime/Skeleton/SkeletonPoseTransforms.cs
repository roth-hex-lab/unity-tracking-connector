using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class SkeletonPoseTransforms
    {
        public static SkeletonPose MirrorLeftRight(SkeletonPose pose)
        {
            var mirrored = pose.CopyJointPoses();
            for (int i = 0; i < mirrored.Length; i++)
            {
                mirrored[i] = MirrorJointPose(mirrored[i]);
            }

            foreach (var pair in pose.Definition.MirrorPairs)
            {
                var firstIndex = pose.Definition.IndexOf(pair.First);
                var secondIndex = pose.Definition.IndexOf(pair.Second);
                if (!pose.Definition.IsValidIndex(firstIndex) ||
                    !pose.Definition.IsValidIndex(secondIndex))
                {
                    continue;
                }

                var first = mirrored[firstIndex];
                mirrored[firstIndex] = mirrored[secondIndex];
                mirrored[secondIndex] = first;
            }

            return new SkeletonPose(
                pose.Definition,
                mirrored,
                pose.CoordinateSpace);
        }

        public static SkeletonFrame MirrorLeftRight(SkeletonFrame frame)
        {
            return new SkeletonFrame(
                MirrorLeftRight(frame.Pose),
                frame.Metadata);
        }

        private static SkeletonJointPose MirrorJointPose(SkeletonJointPose pose)
        {
            if (!pose.HasPosition && !pose.HasRotation)
            {
                return pose;
            }

            return new SkeletonJointPose(
                pose.Channels,
                pose.HasPosition ? MirrorPosition(pose.Position) : pose.Position,
                pose.HasRotation ? MirrorRotation(pose.Rotation) : pose.Rotation,
                pose.Confidence,
                pose.Provenance,
                pose.Source);
        }

        private static Vector3 MirrorPosition(Vector3 position)
        {
            return new Vector3(-position.x, position.y, position.z);
        }

        private static Quaternion MirrorRotation(Quaternion rotation)
        {
            return Normalize(new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w));
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            var magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);
            if (magnitude <= 0.00001f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }
    }
}
