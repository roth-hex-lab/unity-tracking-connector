using System;
using System.Collections.Generic;

namespace HEXLab.Hextrackingconnector
{
    public static class InputSkeletonRegistry
    {
        private const string MediaPipePose33Id = "mediapipe.pose.33";

        private static readonly MediaPipePose33WireMapper MediaPipePose33Mapper =
            new MediaPipePose33WireMapper();
        private static readonly Dictionary<string, IWireSkeletonMapper> MappersById =
            new Dictionary<string, IWireSkeletonMapper>(StringComparer.OrdinalIgnoreCase);
        private static readonly object RegistryLock = new object();

        static InputSkeletonRegistry()
        {
            Register(MediaPipePose33Id, MediaPipePose33Mapper);
            Register("mediapipe_pose_33", MediaPipePose33Mapper);
            Register("HumanPoseSkeleton33", MediaPipePose33Mapper);
        }

        public static void Register(string skeletonId, IWireSkeletonMapper mapper)
        {
            if (string.IsNullOrWhiteSpace(skeletonId))
            {
                throw new ArgumentException("A wire skeleton registration needs a non-empty skeleton id.", nameof(skeletonId));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            lock (RegistryLock)
            {
                MappersById[skeletonId.Trim()] = mapper;
            }
        }

        public static bool TryResolve(string skeletonId, out SkeletonDefinition definition)
        {
            if (TryGetRegisteredMapper(skeletonId, out var mapper))
            {
                definition = mapper.Definition;
                return true;
            }

            definition = null;
            return false;
        }

        public static bool TryGetMapper(
            InputSkeletonSelection selection,
            string skeletonId,
            out IWireSkeletonMapper mapper)
        {
            switch (selection)
            {
                case InputSkeletonSelection.MediaPipePose33:
                    mapper = MediaPipePose33Mapper;
                    return true;
                case InputSkeletonSelection.Auto:
                default:
                    if (string.IsNullOrWhiteSpace(skeletonId))
                    {
                        mapper = MediaPipePose33Mapper;
                        return true;
                    }

                    return TryGetRegisteredMapper(skeletonId, out mapper);
            }
        }

        private static bool TryGetRegisteredMapper(string skeletonId, out IWireSkeletonMapper mapper)
        {
            mapper = null;
            if (string.IsNullOrWhiteSpace(skeletonId))
            {
                return false;
            }

            lock (RegistryLock)
            {
                return MappersById.TryGetValue(skeletonId.Trim(), out mapper);
            }
        }
    }
}
