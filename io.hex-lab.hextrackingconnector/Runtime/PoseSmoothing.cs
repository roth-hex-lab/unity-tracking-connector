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

        public MovingAveragePoseSmoother(int windowSize)
        {
            WindowSize = Math.Max(1, windowSize);
        }

        public int WindowSize { get; }

        public SkeletonFrame Smooth(SkeletonFrame frame)
        {
            frames.Enqueue(frame);

            while (frames.Count > WindowSize)
            {
                frames.Dequeue();
            }

            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            var counts = new int[SkeletonFrame.JointCount];

            foreach (var sample in frames)
            {
                for (int i = 0; i < SkeletonFrame.JointCount; i++)
                {
                    if (!sample.TryGetJoint(i, out var position))
                    {
                        continue;
                    }

                    positions[i] += position;
                    counts[i]++;
                }
            }

            for (int i = 0; i < SkeletonFrame.JointCount; i++)
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
