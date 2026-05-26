using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Tests
{
    class RuntimeExampleTest
    {
        [Test]
        public void HumanPoseSkeleton33DefinitionExposesNamedJoints()
        {
            Assert.AreEqual("HumanPoseSkeleton33", HumanPoseSkeleton33.Definition.Name);
            Assert.AreEqual(33, HumanPoseSkeleton33.JointCount);
            Assert.AreEqual(HumanPoseSkeleton33.JointCount, HumanPoseSkeleton33.Definition.JointCount);
            Assert.AreEqual(0, HumanPoseSkeleton33.Definition.IndexOf(SkeletonJoint.Nose));
            Assert.AreEqual(32, HumanPoseSkeleton33.Definition.IndexOf(SkeletonJoint.RightFootIndex));
            Assert.IsTrue(HumanPoseSkeleton33.Definition.Contains(SkeletonJoint.LeftShoulder));
        }

        [Test]
        public void HumanPoseSkeleton33DoesNotExposeDebugConnectionTopology()
        {
            var skeletonMembers = typeof(HumanPoseSkeleton33)
                .GetMembers(BindingFlags.Static | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();
            var definitionMembers = typeof(SkeletonDefinition)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();
            var publicTypeNames = typeof(SkeletonFrame)
                .Assembly
                .GetExportedTypes()
                .Select(type => type.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(skeletonMembers, "Connections");
            CollectionAssert.DoesNotContain(definitionMembers, "Connections");
            CollectionAssert.DoesNotContain(publicTypeNames, "SkeletonConnection");
        }

        [Test]
        public void DebugVisualizationTypeIsExplicitlyNamed()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(BodyDebugVis)));
            Assert.IsNull(typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.Body"));
        }

        [Test]
        public void BodyDebugVisUsesColorSettingForGeneratedJointMaterial()
        {
            var fields = typeof(BodyDebugVis)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.Contains(fields, "jointColor");
            CollectionAssert.DoesNotContain(fields, "jointMaterial");
            CollectionAssert.DoesNotContain(fields, "jointMaterialOverride");
        }

        [Test]
        public void BodyDebugMaterialsSelectsPipelineSpecificLitShaders()
        {
            var materials = typeof(BodyDebugVis).Assembly.GetType("HEXLab.Hextrackingconnector.BodyDebugMaterials");
            Assert.IsNotNull(materials);

            var method = materials.GetMethod(
                "GetCandidateShaderNames",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var builtIn = (string[])method.Invoke(null, new object[] { null });
            var urp = (string[])method.Invoke(null, new object[] { "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset" });
            var hdrp = (string[])method.Invoke(null, new object[] { "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset" });

            Assert.AreEqual("Standard", builtIn[0]);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit", urp[0]);
            Assert.AreEqual("HDRP/Lit", hdrp[0]);
        }

        [Test]
        public void SkeletonFrameStoresAndRetrievesTrackedJoints()
        {
            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];

            positions[(int)SkeletonJoint.LeftWrist] = new Vector3(1f, 2f, 3f);
            tracked[(int)SkeletonJoint.LeftWrist] = true;

            var frame = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 12,
                receivedTime: 34.5);

            Assert.AreSame(HumanPoseSkeleton33.Definition, frame.Definition);
            Assert.IsTrue(frame.LeftWrist.IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), frame.LeftWrist.Position);
            Assert.IsFalse(frame.RightWrist.IsTracked);
            Assert.IsTrue(frame.TryGetJoint(SkeletonJoint.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), wrist);
            Assert.AreEqual(12, frame.SequenceNumber);
            Assert.AreEqual(34.5, frame.ReceivedTime);
        }

        [Test]
        public void SkeletonFrameDoesNotExposeWireSettings()
        {
            var publicMembers = typeof(SkeletonFrame)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(publicMembers, "CoordinateSource");
            CollectionAssert.DoesNotContain(publicMembers, "MirrorMode");
            Assert.IsFalse(publicMembers.Any(name => name.Contains("Landmark")));
        }

        [Test]
        public void WireLandmarkTypesAreNotPublicApi()
        {
            var publicTypeNames = typeof(SkeletonFrame)
                .Assembly
                .GetExportedTypes()
                .Select(type => type.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(publicTypeNames, "Landmark");
            CollectionAssert.DoesNotContain(publicTypeNames, "LandmarkDefinition");
            CollectionAssert.DoesNotContain(publicTypeNames, "LandmarkMirrorMode");
            CollectionAssert.DoesNotContain(publicTypeNames, "LandmarkCoordinateSource");
        }

        [Test]
        public void MovingAverageSmootherAveragesTrackedJointsAcrossWindow()
        {
            var smoother = new MovingAveragePoseSmoother(windowSize: 2);
            var first = CreateFrame(SkeletonJoint.LeftWrist, new Vector3(0f, 0f, 0f), 1);
            var second = CreateFrame(SkeletonJoint.LeftWrist, new Vector3(2f, 4f, 6f), 2);

            smoother.Smooth(first);
            var smoothed = smoother.Smooth(second);

            Assert.IsTrue(smoothed.TryGetJoint(SkeletonJoint.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), wrist);
            Assert.AreEqual(2, smoothed.SequenceNumber);
        }

        [Test]
        public void MovingAverageSmootherUsesNewestMetadata()
        {
            var smoother = new MovingAveragePoseSmoother(windowSize: 3);

            smoother.Smooth(CreateFrame(SkeletonJoint.LeftAnkle, Vector3.zero, 1));
            var smoothed = smoother.Smooth(CreateFrame(SkeletonJoint.LeftAnkle, Vector3.one, 2));

            Assert.AreSame(HumanPoseSkeleton33.Definition, smoothed.Definition);
            Assert.AreEqual(2, smoothed.SequenceNumber);
            Assert.AreEqual(2.0, smoothed.ReceivedTime);
        }

        [Test]
        public void BodyCalibrationIsAComponentWithCalibrateCommand()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(BodyCalibration)));
            Assert.IsNotNull(typeof(BodyCalibration).GetMethod(nameof(BodyCalibration.Calibrate), System.Type.EmptyTypes));
        }

        [Test]
        public void BodyCalibrationAppliesCalibrationToPoseArrays()
        {
            var calibration = (BodyCalibration)FormatterServices.GetUninitializedObject(typeof(BodyCalibration));
            SetPrivateField(calibration, "autoCalibrate", true);
            SetPrivateField(calibration, "calibrationMode", BodyCalibrationMode.CenterHips);

            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            var calibrated = new Vector3[SkeletonFrame.JointCount];
            positions[(int)SkeletonJoint.LeftHip] = new Vector3(-1f, 2f, 3f);
            positions[(int)SkeletonJoint.RightHip] = new Vector3(3f, 4f, 5f);
            tracked[(int)SkeletonJoint.LeftHip] = true;
            tracked[(int)SkeletonJoint.RightHip] = true;

            calibration.Apply(positions, tracked, calibrated);

            Assert.AreEqual(Vector3.zero, calibrated[(int)SkeletonJoint.LeftHip] + calibrated[(int)SkeletonJoint.RightHip]);
            Assert.IsTrue(calibration.HasCalibration);
        }

        [Test]
        public void CalibrationCanCenterHipMidpointAtOrigin()
        {
            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            positions[(int)SkeletonJoint.LeftHip] = new Vector3(-1f, 2f, 3f);
            positions[(int)SkeletonJoint.RightHip] = new Vector3(3f, 4f, 5f);
            tracked[(int)SkeletonJoint.LeftHip] = true;
            tracked[(int)SkeletonJoint.RightHip] = true;

            var offset = BodyCalibration.CalculateOffset(
                positions,
                tracked,
                BodyCalibrationMode.CenterHips,
                groundHeight: 0f);

            Assert.AreEqual(new Vector3(-1f, -3f, -4f), offset);
        }

        [Test]
        public void CalibrationCanGroundLowestTrackedFoot()
        {
            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            positions[(int)SkeletonJoint.LeftHip] = new Vector3(-1f, 2f, 2f);
            positions[(int)SkeletonJoint.RightHip] = new Vector3(3f, 2f, 4f);
            positions[(int)SkeletonJoint.LeftAnkle] = new Vector3(-1f, -0.25f, 2f);
            positions[(int)SkeletonJoint.RightFootIndex] = new Vector3(3f, -0.75f, 4f);
            tracked[(int)SkeletonJoint.LeftHip] = true;
            tracked[(int)SkeletonJoint.RightHip] = true;
            tracked[(int)SkeletonJoint.LeftAnkle] = true;
            tracked[(int)SkeletonJoint.RightFootIndex] = true;

            var offset = BodyCalibration.CalculateOffset(
                positions,
                tracked,
                BodyCalibrationMode.CenterHipsGroundFeet,
                groundHeight: 0f);

            Assert.AreEqual(new Vector3(-1f, 0.75f, -3f), offset);
        }

        private static SkeletonFrame CreateFrame(SkeletonJoint joint, Vector3 position, int sequenceNumber)
        {
            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            positions[(int)joint] = position;
            tracked[(int)joint] = true;
            return new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber,
                receivedTime: sequenceNumber);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
