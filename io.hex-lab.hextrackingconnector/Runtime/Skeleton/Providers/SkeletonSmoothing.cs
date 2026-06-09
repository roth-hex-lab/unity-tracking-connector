using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
#pragma warning disable 0649
    [SkeletonPipelineNode("SkeletonSmoothing")]
    public class SkeletonSmoothing : MonoBehaviour, ISkeletonProvider
    {
        [SerializeField, SkeletonProvider(allowSelf: false)] private MonoBehaviour sourceProvider;
        [SerializeField] private PoseSmoothingMode smoothingMode = PoseSmoothingMode.MovingAverage;
        [SerializeField, Min(1)] private int movingAverageWindowSize = 5;

        private ISkeletonProvider activeSourceProvider;
        private IPoseSmoother poseSmoother;
        private PoseSmoothingMode activeSmoothingMode;
        private int activeMovingAverageWindowSize;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public event Action<SkeletonFrame> PoseReceived;

        public PoseSmoothingMode SmoothingMode
        {
            get => smoothingMode;
            set
            {
                if (smoothingMode == value)
                {
                    return;
                }

                smoothingMode = value;
                ResetSmoother();
            }
        }

        public int MovingAverageWindowSize
        {
            get => movingAverageWindowSize;
            set
            {
                movingAverageWindowSize = Mathf.Max(1, value);
                ResetSmoother();
            }
        }

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public void ResetSmoother()
        {
            EnsureSmoother(forceReset: true);
        }

        private void OnEnable()
        {
            EnsureSmoother(forceReset: true);
            ResolveSourceProvider();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            movingAverageWindowSize = Mathf.Max(1, movingAverageWindowSize);
        }

        private void ResolveSourceProvider()
        {
            activeSourceProvider = null;
            if (sourceProvider != null)
            {
                SkeletonProviderUtility.TryResolveProvider(
                    sourceProvider,
                    this,
                    "Source Provider",
                    allowSelf: false,
                    out activeSourceProvider);
            }
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
            EnsureSmoother(forceReset: false);
            var smoothedFrame = poseSmoother.Smooth(frame);
            latestPose = smoothedFrame;
            hasLatestPose = true;
            RaisePoseReceived(smoothedFrame);
        }

        private void EnsureSmoother(bool forceReset)
        {
            if (!forceReset &&
                poseSmoother != null &&
                activeSmoothingMode == smoothingMode &&
                activeMovingAverageWindowSize == movingAverageWindowSize)
            {
                return;
            }

            poseSmoother = CreateSmoother(smoothingMode, movingAverageWindowSize);
            activeSmoothingMode = smoothingMode;
            activeMovingAverageWindowSize = movingAverageWindowSize;
        }

        private static IPoseSmoother CreateSmoother(PoseSmoothingMode mode, int movingAverageWindowSize)
        {
            switch (mode)
            {
                case PoseSmoothingMode.MovingAverage:
                    return new MovingAveragePoseSmoother(movingAverageWindowSize);
                case PoseSmoothingMode.None:
                default:
                    return new PassthroughPoseSmoother();
            }
        }

        private void RaisePoseReceived(SkeletonFrame pose)
        {
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, pose, this);
        }
    }
#pragma warning restore 0649
}
