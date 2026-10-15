using NUnit.Framework;
using UnityEngine;
using static HEXLab.Hextrackingconnector.Tests.BodyCalibrationLifecycleTests;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class BodyCalibrationModeTests
    {
        [TestCase(BodyCalibrationMode.None, 0f, 0f, 0f)]
        [TestCase(BodyCalibrationMode.CenterHips, -2f, -2f, -3f)]
        [TestCase(BodyCalibrationMode.CenterHipsGroundFeet, -2f, -0.2f, -3f)]
        [TestCase(BodyCalibrationMode.GroundFeet, 0f, -0.2f, 0f)]
        public void InitialAndExplicitCalibrationUseTheSameMode(BodyCalibrationMode mode, float x, float y, float z)
        {
            var frame = Frame(1, 1d, 2f, 0.4f);
            var expected = new Vector3(x, y, z);
            using (var initial = new CalibrationFixture())
            using (var manual = new CalibrationFixture())
            {
                initial.Set("initialCalibrationMode", mode);
                initial.Set("continuousCalibration", false);
                initial.Set("groundHeight", 0.2f);
                initial.Publish(frame);
                AssertOffset(initial.Calibration, expected);

                manual.Set("groundHeight", 0.2f);
                Assert.IsTrue(manual.Calibration.Calibrate(frame, mode));
                AssertOffset(manual.Calibration, expected);
                manual.Calibration.ResetCalibration();
                Assert.IsTrue(manual.Calibration.Calibrate(frame.Definition, frame.CopyPositions(), frame.CopyTracked(), mode));
                AssertOffset(manual.Calibration, expected);
            }
        }

        [TestCase(BodyCalibrationMode.None, 0f, 0f, 0f)]
        [TestCase(BodyCalibrationMode.CenterHips, -6f, -2f, -3f)]
        [TestCase(BodyCalibrationMode.CenterHipsGroundFeet, -6f, -1f, -3f)]
        [TestCase(BodyCalibrationMode.GroundFeet, 0f, -1f, 0f)]
        public void ContinuousCalibrationRepeatsItsSelectedMode(BodyCalibrationMode mode, float x, float y, float z)
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("calibrateOnFirstPose", false);
                fixture.Set("continuousCalibrationMode", mode);
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                fixture.Publish(Frame(2, 2d, 6f, 1f));
                AssertOffset(fixture.Calibration, new Vector3(x, y, z));
            }
        }

        [Test]
        public void ContinuousGroundingPreservesInitialCenteringAndHorizontalMovement()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("initialCalibrationMode", BodyCalibrationMode.CenterHipsGroundFeet);
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                AssertOffset(fixture.Calibration, new Vector3(-2f, -0.4f, -3f));
                fixture.Publish(Frame(2, 2d, 6f, 0.8f));
                AssertOffset(fixture.Calibration, new Vector3(-2f, -0.8f, -3f));
                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var pose));
                Assert.That(pose.GetJoint(BodyJoints.LeftAnkle).y, Is.EqualTo(0f).Within(0.00001f));
                var centre = (pose.GetJoint(BodyJoints.LeftHip) + pose.GetJoint(BodyJoints.RightHip)) / 2f;
                Assert.That(centre.x, Is.EqualTo(4f).Within(0.00001f));
            }
        }

        [Test]
        public void GroundFeetUsesFootHeightWithoutRequiringHipCentering()
        {
            using (var fixture = new CalibrationFixture())
            {
                var definition = HumanPoseSkeleton33.Definition;
                var positions = new Vector3[definition.JointCount];
                var tracked = new bool[definition.JointCount];
                positions[definition.IndexOf(BodyJoints.LeftAnkle)] = new Vector3(5f, 1f, 4f);
                positions[definition.IndexOf(BodyJoints.Nose)] = new Vector3(5f, 3f, 4f);
                tracked[definition.IndexOf(BodyJoints.LeftAnkle)] = true;
                tracked[definition.IndexOf(BodyJoints.Nose)] = true;
                Assert.IsTrue(fixture.Calibration.Calibrate(positions, tracked, BodyCalibrationMode.GroundFeet));
                AssertOffset(fixture.Calibration, new Vector3(0f, -1f, 0f));
            }
        }

        [Test]
        public void ManualCalibrationIsImmediateAndSatisfiesTheInitialCalibration()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("smoothContinuousCalibration", true);
                fixture.Set("continuousSmoothingTime", 10f);
                Assert.IsTrue(fixture.Calibration.Calibrate(Frame(1, 1d, 2f, 0.4f), BodyCalibrationMode.CenterHips));
                AssertOffset(fixture.Calibration, new Vector3(-2f, -2f, -3f));
                fixture.Publish(Frame(2, 2d, 6f, 0.8f));
                Assert.That(fixture.Calibration.CalibrationOffset.x, Is.EqualTo(-2f));
                // The command using the cached source also applies its explicitly requested mode immediately.
                Assert.IsTrue(fixture.Calibration.Calibrate(BodyCalibrationMode.CenterHipsGroundFeet));
                AssertOffset(fixture.Calibration, new Vector3(-6f, -0.8f, -3f));
            }
        }

        [Test]
        public void SmoothingUsesPoseTimeAndIsIndependentOfDeliveryRate()
        {
            using (var singleStep = new CalibrationFixture())
            using (var splitSteps = new CalibrationFixture())
            {
                foreach (var fixture in new[] { singleStep, splitSteps })
                {
                    fixture.Set("smoothContinuousCalibration", true);
                    fixture.Set("continuousSmoothingTime", 0.5f);
                    fixture.Publish(Frame(1, 1d, 2f, 0f));
                }

                singleStep.Publish(Frame(3, 2d, 2f, 1f));
                splitSteps.Publish(Frame(2, 1.5d, 2f, 1f));
                var last = Frame(3, 2d, 2f, 1f);
                splitSteps.Publish(last);
                Assert.That(singleStep.Calibration.CalibrationOffset.y, Is.EqualTo(-0.8646647f).Within(0.00001f));
                AssertOffset(splitSteps.Calibration, singleStep.Calibration.CalibrationOffset);
                splitSteps.Source.Publish(last);
                AssertOffset(splitSteps.Calibration, singleStep.Calibration.CalibrationOffset);

                // Playback can restart its timeline; it must not freeze or interpolate with negative time.
                splitSteps.Publish(Frame(1, 1d, 2f, 0.25f));
                Assert.That(splitSteps.Calibration.CalibrationOffset.y, Is.EqualTo(-0.25f).Within(0.00001f));
            }
        }

        [Test]
        public void SmoothingPrefersSourceTimeOverDeliveryTime()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("smoothContinuousCalibration", true);
                fixture.Set("continuousSmoothingTime", 0.5f);
                var first = Frame(1, 1000d, 2f, 0f);
                var second = Frame(2, 1000d, 2f, 1f);
                fixture.Publish(new SkeletonFrame(first.Pose, new SkeletonFrameMetadata(1, 1000d, 10d)));
                fixture.Publish(new SkeletonFrame(second.Pose, new SkeletonFrameMetadata(2, 1000d, 10.5d)));
                Assert.That(fixture.Calibration.CalibrationOffset.y, Is.EqualTo(-0.6321206f).Within(0.00001f));
            }
        }

        [Test]
        public void DistinctPosesAtTheSameTimestampDoNotAdvanceSmoothing()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Set("smoothContinuousCalibration", true);
                fixture.Set("continuousSmoothingTime", 0.5f);
                fixture.Publish(Frame(1, 1d, 2f, 0f));
                fixture.Publish(Frame(2, 2d, 2f, 1f));
                var offset = fixture.Calibration.CalibrationOffset;

                fixture.Publish(Frame(3, 2d, 2f, 2f));
                Assert.AreEqual(offset, fixture.Calibration.CalibrationOffset);
                Assert.IsTrue(fixture.Calibration.TryGetLatestPose(out var latest));
                Assert.AreEqual(3, latest.SequenceNumber, "A distinct pose is still delivered even when no filter time elapses.");
            }
        }

        [Test]
        public void TrackingLossKeepsTheOffsetAndResetRearmsInitialCalibration()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                fixture.Publish(new SkeletonFrame(new SkeletonPose(HumanPoseSkeleton33.Definition,
                    new SkeletonJointPose[HumanPoseSkeleton33.JointCount]), new SkeletonFrameMetadata(2, 2d)));
                AssertOffset(fixture.Calibration, new Vector3(-2f, -0.4f, -3f));
                fixture.Calibration.ResetCalibration();
                fixture.Publish(Frame(3, 3d, 6f, 0.8f));
                AssertOffset(fixture.Calibration, new Vector3(-6f, -0.8f, -3f));
            }
        }

        [Test]
        public void DisableIgnoresQueuedPosesAndReenableKeepsInitialCentering()
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                var queued = fixture.Source.SnapshotHandlers();
                fixture.Disable();
                queued(Frame(2, 2d, 20f, 8f));
                AssertOffset(fixture.Calibration, new Vector3(-2f, -0.4f, -3f));
                fixture.Source.Publish(Frame(2, 2d, 6f, 0.8f));
                fixture.Enable();
                AssertOffset(fixture.Calibration, new Vector3(-2f, -0.8f, -3f));
            }
        }

        [TestCase("definition")]
        [TestCase("coordinates")]
        [TestCase("source")]
        public void ChangingPoseContextReinitializesCalibration(string context)
        {
            using (var fixture = new CalibrationFixture())
            {
                fixture.Publish(Frame(1, 1d, 2f, 0.4f));
                var changed = Frame(2, 2d, 10f, 0.8f,
                    context == "definition" ? CocoPoseSkeleton17.Definition : HumanPoseSkeleton33.Definition);
                if (context == "coordinates")
                    changed = new SkeletonFrame(new SkeletonPose(changed.Definition, changed.CopyJointPoses(),
                        SkeletonCoordinateSpace.RootRelative), changed.Metadata);
                if (context == "source")
                    changed = new SkeletonFrame(changed.Pose, new SkeletonFrameMetadata(2, 2d, sourceId: "new source"));
                fixture.Publish(changed);
                AssertOffset(fixture.Calibration, new Vector3(-10f, -0.8f, -3f));
            }
        }

        private static void AssertOffset(BodyCalibration calibration, Vector3 expected)
        {
            Assert.That(Vector3.Distance(expected, calibration.CalibrationOffset), Is.LessThan(0.00001f));
        }
    }
}
