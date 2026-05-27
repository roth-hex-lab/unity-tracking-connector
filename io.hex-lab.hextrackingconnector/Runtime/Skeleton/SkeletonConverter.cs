using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum OutputSkeletonSelection
    {
        Source,
        HumanPoseSkeleton33,
        CocoPose17,
    }

#pragma warning disable 0649
    public class SkeletonConverter : MonoBehaviour, ISkeletonProvider
    {
        [SerializeField] private MonoBehaviour sourceProvider;
        [SerializeField] private OutputSkeletonSelection outputSkeleton = OutputSkeletonSelection.Source;
        [SerializeField] private bool logUnsupportedConversions = true;

        private ISkeletonProvider activeSourceProvider;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public event Action<SkeletonFrame> PoseReceived;

        public OutputSkeletonSelection OutputSkeleton
        {
            get => outputSkeleton;
            set => outputSkeleton = value;
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool TryConvert(SkeletonFrame sourceFrame, out SkeletonFrame convertedFrame)
        {
            var targetDefinition = GetTargetDefinition(outputSkeleton);
            if (targetDefinition == null)
            {
                convertedFrame = sourceFrame;
                return true;
            }

            return sourceFrame.TryConvertTo(targetDefinition, out convertedFrame);
        }

        private void OnEnable()
        {
            ResolveSourceProvider();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (sourceProvider != null && !(sourceProvider is ISkeletonProvider))
            {
                sourceProvider = null;
            }
        }

        private void ResolveSourceProvider()
        {
            activeSourceProvider = sourceProvider as ISkeletonProvider;
        }

        private void Subscribe()
        {
            if (activeSourceProvider != null)
            {
                activeSourceProvider.PoseReceived += OnSourcePoseReceived;
            }
        }

        private void Unsubscribe()
        {
            if (activeSourceProvider != null)
            {
                activeSourceProvider.PoseReceived -= OnSourcePoseReceived;
            }

            activeSourceProvider = null;
        }

        private void OnSourcePoseReceived(SkeletonFrame frame)
        {
            if (!TryConvert(frame, out var convertedFrame))
            {
                if (logUnsupportedConversions)
                {
                    Debug.LogWarning(
                        $"SkeletonConversionProvider cannot convert '{frame.Definition.Name}' to '{outputSkeleton}'.",
                        this);
                }

                return;
            }

            latestPose = convertedFrame;
            hasLatestPose = true;
            RaisePoseReceived(convertedFrame);
        }

        private void RaisePoseReceived(SkeletonFrame pose)
        {
            var handlers = PoseReceived;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SkeletonFrame> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(pose);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private static SkeletonDefinition GetTargetDefinition(OutputSkeletonSelection selection)
        {
            switch (selection)
            {
                case OutputSkeletonSelection.HumanPoseSkeleton33:
                    return HumanPoseSkeleton33.Definition;
                case OutputSkeletonSelection.CocoPose17:
                    return CocoPoseSkeleton17.Definition;
                case OutputSkeletonSelection.Source:
                default:
                    return null;
            }
        }
    }
#pragma warning restore 0649
}
