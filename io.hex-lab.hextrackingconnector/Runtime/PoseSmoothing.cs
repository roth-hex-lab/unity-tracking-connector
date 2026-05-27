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
            var tracked = new bool[jointCount];
            var counts = new int[jointCount];

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
                    counts[i]++;
                }
            }

            for (int i = 0; i < jointCount; i++)
            {
                if (counts[i] == 0)
                {
                    continue;
                }

                positions[i] /= counts[i];
                tracked[i] = true;
            }

            return new SkeletonFrame(
                frame.Definition,
                positions,
                tracked,
                frame.SequenceNumber,
                frame.ReceivedTime);
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
