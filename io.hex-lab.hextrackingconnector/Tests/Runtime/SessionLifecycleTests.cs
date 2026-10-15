using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class SessionLifecycleTests
    {
        [Test]
        public void CalibrationSetupReplacesSubscriptionAndDisableDetachesIt()
        {
            var sourceObject = new GameObject("Sources");
            var targetObject = new GameObject("Calibration");
            targetObject.SetActive(false);
            var first = sourceObject.AddComponent<LifecycleTestProvider>();
            var second = sourceObject.AddComponent<LifecycleTestProvider>();
            var calibration = targetObject.AddComponent<BodyCalibration>();
            calibration.SetSkeletonProvider(first);
            var received = 0;
            calibration.PoseReceived += _ => received++;

            try
            {
                targetObject.SetActive(true);
                calibration.SetupProviderCapture();
                calibration.SetupProviderCapture();
                first.Publish(CreateFrame(1));
                Assert.AreEqual(1, received, "Repeated setup must not duplicate delivery.");

                calibration.SetSkeletonProvider(second);
                calibration.SetupProviderCapture();
                first.Publish(CreateFrame(2));
                Assert.AreEqual(1, received, "The previous source must be detached.");
                second.Publish(CreateFrame(3));
                Assert.AreEqual(2, received);

                targetObject.SetActive(false);
                first.Publish(CreateFrame(4));
                second.Publish(CreateFrame(5));
                Assert.AreEqual(2, received, "A disabled calibration must not receive frames.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void CaptureCompletesBeforeTheSameFrameCanBePublished()
        {
            var gameObject = new GameObject("CaptureOrdering");
            gameObject.SetActive(false);
            var server = gameObject.AddComponent<CommServer>();
            var published = 0;
            var publishedDuringCapture = 0;
            server.PoseReceived += _ => published++;
            server.FrameCaptured += _ =>
            {
                Invoke(server, "PublishPendingFrames");
                publishedDuringCapture = published;
            };
            try
            {
                Invoke(server, "ParseAndEnqueue", "{\"free\":[{\"index\":0,\"x\":1,\"y\":2,\"z\":3}]}");
                Assert.AreEqual(0, publishedDuringCapture);
                Invoke(server, "PublishPendingFrames");
                Assert.AreEqual(1, published);
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void TransportCapturesEveryFrameButPublishesOnlyLatestPendingFrame()
        {
            var gameObject = new GameObject("LatestFrame");
            gameObject.SetActive(false);
            var server = gameObject.AddComponent<CommServer>();
            var captured = 0;
            var published = 0;
            var latest = default(SkeletonFrame);
            server.FrameCaptured += _ => captured++;
            server.PoseReceived += frame => { published++; latest = frame; };

            try
            {
                for (var i = 0; i < 100; i++)
                {
                    Invoke(server, "ParseAndEnqueue", "{\"free\":[{\"index\":0,\"x\":" + i + ",\"y\":1,\"z\":2}]}");
                }

                Assert.AreEqual(100, captured);
                Assert.AreEqual(1, server.PendingFrameCount, "Pausing publication must keep memory bounded.");
                Invoke(server, "PublishPendingFrames");
                Invoke(server, "PublishPendingFrames");
                Assert.AreEqual(1, published);
                Assert.AreEqual(0, server.PendingFrameCount);
                Assert.IsTrue(latest.TryGetJoint(BodyJoints.Nose, out var nose));
                Assert.AreEqual(99f, nose.x);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        internal static SkeletonFrame CreateFrame(int sequence)
        {
            var poses = new SkeletonJointPose[HumanPoseSkeleton33.JointCount];
            poses[HumanPoseSkeleton33.Definition.IndexOf(BodyJoints.Nose)] =
                SkeletonJointPose.FromPosition(new Vector3(sequence, 1, 2));
            return new SkeletonFrame(new SkeletonPose(HumanPoseSkeleton33.Definition, poses),
                new SkeletonFrameMetadata(sequence, sequence));
        }

        internal static void Invoke(object target, string name, params object[] args)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }
    }

    public class LifecycleTestProvider : MonoBehaviour, ISkeletonProvider
    {
        public event Action<SkeletonFrame> PoseReceived;
        public bool TryGetLatestPose(out SkeletonFrame pose) { pose = default; return false; }
        public void Publish(SkeletonFrame frame) { PoseReceived?.Invoke(frame); }
        public Action<SkeletonFrame> SnapshotHandlers() => PoseReceived;
    }
}
