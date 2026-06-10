using System;

namespace HEXLab.Hextrackingconnector
{
    internal static class SkeletonRecordingDefinitionUtility
    {
        public static SkeletonDefinition ResolveDefinition(
            string id,
            string name,
            string[] jointNames)
        {
            if (TryGetBuiltInDefinition(id, out var builtIn))
            {
                return builtIn;
            }

            if (jointNames == null || jointNames.Length == 0)
            {
                return SkeletonDefinition.Empty;
            }

            var joints = new SkeletonJointId[jointNames.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = new SkeletonJointId(
                    string.IsNullOrWhiteSpace(jointNames[i])
                        ? $"Joint{i}"
                        : jointNames[i]);
            }

            return new SkeletonDefinition(
                string.IsNullOrWhiteSpace(id) ? "recording.dynamic" : id,
                string.IsNullOrWhiteSpace(name) ? "Recorded Skeleton" : name,
                joints);
        }

        public static bool TryGetBuiltInDefinition(string id, out SkeletonDefinition definition)
        {
            if (string.Equals(id, HumanPoseSkeleton33.Definition.Id, StringComparison.Ordinal))
            {
                definition = HumanPoseSkeleton33.Definition;
                return true;
            }

            if (string.Equals(id, CocoPoseSkeleton17.Definition.Id, StringComparison.Ordinal))
            {
                definition = CocoPoseSkeleton17.Definition;
                return true;
            }

            if (string.Equals(id, UnityHumanoidControlSkeleton.Definition.Id, StringComparison.Ordinal))
            {
                definition = UnityHumanoidControlSkeleton.Definition;
                return true;
            }

            definition = null;
            return false;
        }
    }
}
