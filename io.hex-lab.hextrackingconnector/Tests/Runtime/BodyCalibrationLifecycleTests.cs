using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class BodyCalibrationLifecycleTests
    {
        [Test]
        public void ApplyingAndResettingCachedPosesDoNotAdvanceCalibration()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("initialCalibrationMode", BodyCalibrationMode.CenterHipsGroundFeet);
                fixture.Publish(Frame(1, 1d, 2f, 0f));
                var moved = Frame(2, 2d, 6f, 1f);
                fixture.Publish(moved);
                var offset = fixture.Calibration.CalibrationOffset;
                fixture.Calibration.TryApply(moved, out var first);
                fixture.Calibration.TryApply(moved, out var second);
                Assert.AreEqual(offset, fixture.Calibration.CalibrationOffset,
                    "Applying the same pose must not change calibration state.");
                Assert.AreEqual(first.GetJoint(BodyJoints.LeftAnkle), second.GetJoint(BodyJoints.LeftAnkle));

                fixture.Calibration.ResetCalibration();
                Assert.AreEqual(Vector3.zero, fixture.Calibration.CalibrationOffset);
                Assert.IsFalse(fixture.Calibration.HasCalibration);
                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var reset));
                Assert.AreEqual(moved.GetJoint(BodyJoints.LeftAnkle), reset.GetJoint(BodyJoints.LeftAnkle));
            }
        }

        [Test]
        public void SetupProcessesAnAlreadyAvailablePoseOnlyOnce()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("initialCalibrationMode", BodyCalibrationMode.CenterHipsGroundFeet);
                fixture.Source.Publish(Frame(1, 1d, 2f, 0.4f));
                var published = 0;
                fixture.Calibration.PoseReceived += _ => published++;
                fixture.Enable();
                Assert.IsTrue(fixture.Calibration.HasCalibration);
                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var pose));
                Assert.That(pose.GetJoint(BodyJoints.LeftAnkle).y, Is.EqualTo(0f).Within(0.00001f));
                Assert.AreEqual(1, published);

                fixture.Calibration.SetupProviderCapture();
                fixture.Calibration.SetupProviderCapture();
                Assert.AreEqual(1, published, "Repeated setup must not reprocess the cached pose.");
            }
        }

        [Test]
        public void SwitchingProvidersDropsOldCalibrationAndQueuedDeliveries()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("initialCalibrationMode", BodyCalibrationMode.CenterHipsGroundFeet);
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                var queued = fixture.Source.SnapshotHandlers();
                var next = fixture.Root.AddComponent<CalibrationTestProvider>();
                fixture.Calibration.SetSkeletonProvider(next);
                Assert.IsFalse(fixture.Calibration.HasCalibration);
                Assert.IsFalse(fixture.Calibration.TryGetLatestPose(out _));
                Assert.AreEqual(Vector3.zero, fixture.Calibration.CalibrationOffset);
                queued(Frame(2, 2d, 20f, 8f));
                Assert.IsFalse(fixture.Calibration.TryGetLatestPose(out _));

                next.Publish(Frame(1, 1d, 10f, 0.8f));
                Assert.AreEqual(new Vector3(-10f, -0.8f, -3f), fixture.Calibration.CalibrationOffset);
            }
        }

        [Test]
        public void SwitchingProvidersInsideCalibrationChangedCannotPublishTheOldFrame()
        {
            using (var fixture = new CalibrationFixture())
            {
                var next = fixture.Root.AddComponent<CalibrationTestProvider>();
                next.Publish(Frame(9, 9d, 10f, 0.8f));
                Action switchProvider = null;
                switchProvider = () =>
                {
                    fixture.Calibration.CalibrationChanged -= switchProvider;
                    fixture.Calibration.SetSkeletonProvider(next);
                };
                fixture.Calibration.CalibrationChanged += switchProvider;
                var published = 0;
                fixture.Calibration.PoseReceived += _ => published++;

                fixture.Publish(Frame(1, 1d, 2f, 0.4f));

                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var latest));
                Assert.AreEqual(9, latest.SequenceNumber);
                Assert.AreEqual(1, published);
                Assert.AreEqual(new Vector3(-10f, -0.8f, -3f), fixture.Calibration.CalibrationOffset);
            }
        }

        [Test]
        public void ResetInsideCalibrationChangedLeavesInitialCalibrationArmed()
        {
            using (var fixture = new CalibrationFixture())
            {
                Action reset = null;
                reset = () =>
                {
                    fixture.Calibration.CalibrationChanged -= reset;
                    fixture.Calibration.ResetCalibration();
                };
                fixture.Calibration.CalibrationChanged += reset;
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                Assert.IsFalse(fixture.Calibration.HasCalibration);

                fixture.Publish(Frame(2, 2d, 6f, 0.8f));
                Assert.AreEqual(new Vector3(-6f, -0.8f, -3f), fixture.Calibration.CalibrationOffset);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResetDuringAProviderChangeDoesNotPreventReconnection(bool serializedChange)
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                var next = fixture.Root.AddComponent<CalibrationTestProvider>();
                Action reset = null;
                reset = () =>
                {
                    fixture.Calibration.CalibrationChanged -= reset;
                    fixture.Calibration.ResetCalibration();
                };
                fixture.Calibration.CalibrationChanged += reset;

                if (serializedChange)
                {
                    fixture.Set("skeletonProvider", next);
                    fixture.Calibration.SetupProviderCapture();
                }
                else
                {
                    fixture.Calibration.SetSkeletonProvider(next);
                }

                next.Publish(Frame(9, 9d, 10f, 0.8f));
                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var latest));
                Assert.AreEqual(9, latest.SequenceNumber);
                Assert.AreEqual(new Vector3(-10f, -0.8f, -3f), fixture.Calibration.CalibrationOffset);
            }
        }

        [Test]
        public void ApplyingArraysOnlyAppliesTheCurrentOffset()
        {
            using (var fixture = new CalibrationFixture())
            {
                var frame = Frame(1, 1d, 2f, 0.4f);
                var destination = new Vector3[frame.JointCount];
                fixture.Calibration.Apply(frame.Definition, frame.CopyPositions(), frame.CopyTracked(), destination);
                Assert.IsFalse(fixture.Calibration.HasCalibration);
                Assert.AreEqual(frame.GetJoint(BodyJoints.LeftHip), destination[frame.Definition.IndexOf(BodyJoints.LeftHip)]);
            }
        }

        internal static SkeletonFrame Frame(int sequence, double time, float hipsX, float footY,
            SkeletonDefinition definition = null)
        {
            definition = definition ?? HumanPoseSkeleton33.Definition;
            var positions = new Vector3[definition.JointCount];
            var tracked = new bool[definition.JointCount];
            SetJoint(BodyJoints.LeftHip, new Vector3(hipsX - 1f, 2f, 3f));
            SetJoint(BodyJoints.RightHip, new Vector3(hipsX + 1f, 2f, 3f));
            SetJoint(BodyJoints.LeftAnkle, new Vector3(hipsX - 1f, footY, 3f));
            return new SkeletonFrame(definition, positions, tracked, sequence, time);

            void SetJoint(SkeletonJointId joint, Vector3 position)
            {
                var index = definition.IndexOf(joint);
                if (!definition.IsValidIndex(index)) return;
                positions[index] = position;
                tracked[index] = true;
            }
        }

        internal sealed class CalibrationFixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("Calibration fixture");
            internal readonly CalibrationTestProvider Source;
            internal readonly BodyCalibration Calibration;
            private readonly GameObject target;

            internal CalibrationFixture()
            {
                Source = Root.AddComponent<CalibrationTestProvider>();
                target = new GameObject("Calibrated provider");
                target.transform.SetParent(Root.transform);
                target.SetActive(false);
                Calibration = target.AddComponent<BodyCalibration>();
                Calibration.SetSkeletonProvider(Source);
            }

            internal void Set(string field, object value) => typeof(BodyCalibration)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Calibration, value);

            internal void Enable() => target.SetActive(true);
            internal void Disable() => target.SetActive(false);
            internal void Publish(SkeletonFrame frame) { Enable(); Source.Publish(frame); }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }
    }

    public sealed class CalibrationTestProvider : MonoBehaviour, ISkeletonProvider
    {
        private SkeletonFrame latest;
        private bool hasLatest;
        public event Action<SkeletonFrame> PoseReceived;
        public bool TryGetLatestPose(out SkeletonFrame pose) { pose = latest; return hasLatest; }
        public void Publish(SkeletonFrame frame) { latest = frame; hasLatest = true; PoseReceived?.Invoke(frame); }
        public Action<SkeletonFrame> SnapshotHandlers() => PoseReceived;
    }
}
