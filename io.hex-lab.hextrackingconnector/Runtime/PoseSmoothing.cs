using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum PoseSmoothingMode
    {
        None,
        MovingAverage,
    }

    public interface IPoseSmoother
    {
        SkeletonFrame Smooth(SkeletonFrame frame);
        void Reset();
    }

    public sealed class PassthroughPoseSmoother : IPoseSmoother
    {
        public SkeletonFrame Smooth(SkeletonFrame frame)
        {
            return frame;
        }

        public void Reset()
        {
        }
    }

    public sealed class MovingAveragePoseSmoother : IPoseSmoother
    {
        private readonly Queue<SkeletonFrame> frames = new Queue<SkeletonFrame>();
        private string activeDefinitionId;

        public MovingAveragePoseSmoother(int windowSize)
        {
            WindowSize = Math.Max(1, windowSize);
        }

        public int WindowSize { get; }

        public SkeletonFrame Smooth(SkeletonFrame frame)
        {
            if (activeDefinitionId != null &&
                !string.Equals(activeDefinitionId, frame.Definition.Id, StringComparison.Ordinal))
            {
                frames.Clear();
            }

            activeDefinitionId = frame.Definition.Id;
            frames.Enqueue(frame);

            while (frames.Count > WindowSize)
            {
                frames.Dequeue();
            }

            var jointCount = frame.Definition.JointCount;
            var positions = new Vector3[jointCount];
            var confidenceTotals = new float[jointCount];
            var counts = new int[jointCount];
            var latestJointPoses = frame.CopyJointPoses();

            foreach (var sample in frames)
            {
                if (!string.Equals(sample.Definition.Id, frame.Definition.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                for (int i = 0; i < jointCount; i++)
                {
                    if (!sample.TryGetJoint(i, out var position))
                    {
                        continue;
                    }

                    positions[i] += position;
                    if (sample.TryGetJointPose(i, out var jointPose) && jointPose.HasConfidence)
                    {
                        confidenceTotals[i] += jointPose.Confidence;
                    }
                    else
                    {
                        confidenceTotals[i] += 1f;
                    }

                    counts[i]++;
                }
            }

            var poses = new SkeletonJointPose[jointCount];
            for (int i = 0; i < jointCount; i++)
            {
                var latestPose = latestJointPoses[i];
                if (counts[i] == 0)
                {
                    poses[i] = latestPose.HasRotation
                        ? latestPose
                        : SkeletonJointPose.Unavailable;
                    continue;
                }

                positions[i] /= counts[i];
                var confidence = confidenceTotals[i] / counts[i];
                var channels = SkeletonJointChannels.Position | SkeletonJointChannels.Confidence;
                if (latestPose.HasRotation)
                {
                    channels |= SkeletonJointChannels.Rotation;
                }

                poses[i] = new SkeletonJointPose(
                    channels,
                    positions[i],
                    latestPose.HasRotation ? latestPose.Rotation : Quaternion.identity,
                    confidence,
                    latestPose.Provenance == SkeletonDataProvenance.Unknown
                        ? SkeletonDataProvenance.Direct
                        : latestPose.Provenance,
                    latestPose.Source);
            }

            return new SkeletonFrame(
                new SkeletonPose(frame.Definition, poses, frame.CoordinateSpace),
                frame.Metadata);
        }

        public void Reset()
        {
            frames.Clear();
            activeDefinitionId = null;
        }
    }

    public static class PoseSmootherFactory
    {
        public static IPoseSmoother Create(PoseSmoothingMode mode, int movingAverageWindowSize)
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
    }
}
