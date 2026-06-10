using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    [SkeletonPipelineNode("SkeletonProviderSwitcher")]
    public class SkeletonProviderSwitcher : MonoBehaviour, ISkeletonProvider
    {
        [SerializeField, SkeletonProvider(allowSelf: false)] private MonoBehaviour primaryProvider;
        [SerializeField, SkeletonProvider(allowSelf: false)] private MonoBehaviour secondaryProvider;
        [SerializeField] private SkeletonProviderSwitchSelection activeSource = SkeletonProviderSwitchSelection.Primary;
        [SerializeField] private bool publishLatestOnSwitch;

        private ISkeletonProvider activeProvider;
        private SkeletonProviderSwitchSelection subscribedSource;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public event Action<SkeletonFrame> PoseReceived;

        public SkeletonProviderSwitchSelection ActiveSource
        {
            get => activeSource;
            set
            {
                if (activeSource == value && activeProvider != null)
                {
                    return;
                }

                activeSource = value;
                Reconnect();
            }
        }

        public MonoBehaviour PrimaryProvider => primaryProvider;
        public MonoBehaviour SecondaryProvider => secondaryProvider;
        public MonoBehaviour ActiveProviderComponent => activeSource == SkeletonProviderSwitchSelection.Primary
            ? primaryProvider
            : secondaryProvider;

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool TryGetActiveProviderComponent(out MonoBehaviour provider)
        {
            provider = ActiveProviderComponent;
            return provider != null;
        }

        private void OnEnable()
        {
            Reconnect();
        }

        private void Update()
        {
            if (activeProvider == null || subscribedSource != activeSource)
            {
                Reconnect();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void UsePrimary()
        {
            ActiveSource = SkeletonProviderSwitchSelection.Primary;
        }

        public void UseSecondary()
        {
            ActiveSource = SkeletonProviderSwitchSelection.Secondary;
        }

        private void Reconnect()
        {
            Unsubscribe();

            var component = activeSource == SkeletonProviderSwitchSelection.Primary
                ? primaryProvider
                : secondaryProvider;

            if (component == null)
            {
                return;
            }

            if (!SkeletonProviderUtility.TryResolveProvider(
                    component,
                    this,
                    activeSource.ToString(),
                    allowSelf: false,
                    out activeProvider))
            {
                return;
            }

            subscribedSource = activeSource;
            activeProvider.PoseReceived += OnSourcePoseReceived;

            if (activeProvider.TryGetLatestPose(out var pose))
            {
                latestPose = pose;
                hasLatestPose = true;
                if (publishLatestOnSwitch)
                {
                    RaisePoseReceived(pose);
                }
            }
        }

        private void Unsubscribe()
        {
            if (activeProvider != null)
            {
                activeProvider.PoseReceived -= OnSourcePoseReceived;
                activeProvider = null;
            }
        }

        private void OnSourcePoseReceived(SkeletonFrame frame)
        {
            latestPose = frame;
            hasLatestPose = true;
            RaisePoseReceived(frame);
        }

        private void RaisePoseReceived(SkeletonFrame frame)
        {
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, frame, this);
        }
    }
}
