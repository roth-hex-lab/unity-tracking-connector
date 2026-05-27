using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Tests
{
    class RuntimeExampleTest
    {
        private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

        static RuntimeExampleTest()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (!(field.GetValue(null) is OpCode opCode))
                {
                    continue;
                }

                var value = (ushort)opCode.Value;
                if (value < 0x100)
                {
                    OneByteOpCodes[value] = opCode;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByteOpCodes[value & 0xff] = opCode;
                }
            }
        }

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
        public void SkeletonJointIdIsPublicNameBasedValueType()
        {
            var type = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.SkeletonJointId");

            Assert.IsNotNull(type);
            Assert.IsTrue(type.IsValueType);

            var leftWrist = Activator.CreateInstance(type, "LeftWrist");
            var sameLeftWrist = Activator.CreateInstance(type, "LeftWrist");
            var rightWrist = Activator.CreateInstance(type, "RightWrist");

            Assert.AreEqual(leftWrist, sameLeftWrist);
            Assert.AreNotEqual(leftWrist, rightWrist);
            Assert.AreEqual("LeftWrist", leftWrist.ToString());
        }

        [Test]
        public void SkeletonFrameDoesNotExposeStaticJointCount()
        {
            Assert.IsNull(typeof(SkeletonFrame).GetField("JointCount"));
        }

        [Test]
        public void SkeletonDefinitionsExposeDebugLineTopology()
        {
            var skeletonMembers = typeof(HumanPoseSkeleton33)
                .GetMembers(BindingFlags.Static | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();
            var publicTypeNames = typeof(SkeletonFrame)
                .Assembly
                .GetExportedTypes()
                .Select(type => type.Name)
                .ToArray();
            var debugLineStripsProperty = typeof(SkeletonDefinition).GetProperty("DebugLineStrips");
            var bodyLineStrips = typeof(BodyDebugVis).GetField(
                "LineStrips",
                BindingFlags.Static | BindingFlags.NonPublic);

            CollectionAssert.DoesNotContain(skeletonMembers, "Connections");
            CollectionAssert.DoesNotContain(publicTypeNames, "SkeletonConnection");
            Assert.IsNotNull(debugLineStripsProperty);
            Assert.IsNull(bodyLineStrips);
            Assert.Greater(((System.Collections.ICollection)debugLineStripsProperty.GetValue(HumanPoseSkeleton33.Definition)).Count, 0);
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
        public void CommServerImplementsSkeletonProvider()
        {
            var providerType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.ISkeletonProvider");

            Assert.IsNotNull(providerType);
            Assert.IsTrue(providerType.IsInterface);
            Assert.IsTrue(providerType.IsAssignableFrom(typeof(CommServer)));
            Assert.IsNotNull(providerType.GetEvent("PoseReceived"));
            Assert.IsNotNull(providerType.GetMethod("TryGetLatestPose"));
        }

        [Test]
        public void BodyDebugVisDependsOnSkeletonProviderInsteadOfCommServer()
        {
            var providerFields = typeof(BodyDebugVis)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.Name.Contains("Provider"))
                .ToArray();
            var commServerFields = typeof(BodyDebugVis)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(CommServer))
                .ToArray();

            Assert.AreEqual(0, commServerFields.Length);
            Assert.IsTrue(providerFields.Any(field => field.FieldType == typeof(MonoBehaviour)));
        }

        [Test]
        public void SkeletonConverterIsInspectableProviderAdapter()
        {
            var providerType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.ISkeletonProvider");
            var converterType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.SkeletonConverter");
            var outputType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.OutputSkeletonSelection");

            Assert.IsNotNull(converterType);
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(converterType));
            Assert.IsTrue(providerType.IsAssignableFrom(converterType));
            Assert.IsNotNull(outputType);
            CollectionAssert.Contains(Enum.GetNames(outputType), "Source");
            CollectionAssert.Contains(Enum.GetNames(outputType), "CocoPose17");
            Assert.IsTrue(converterType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.Name.Contains("Provider") && field.FieldType == typeof(MonoBehaviour)));
        }

        [Test]
        public void SkeletonDefinitionProvidesHeadPose()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[(int)SkeletonJoint.Nose] = new Vector3(0f, 0f, 1f);
            positions[(int)SkeletonJoint.RightEar] = new Vector3(1f, 0f, 0f);
            positions[(int)SkeletonJoint.LeftEar] = new Vector3(-1f, 0f, 0f);
            tracked[(int)SkeletonJoint.Nose] = true;
            tracked[(int)SkeletonJoint.RightEar] = true;
            tracked[(int)SkeletonJoint.LeftEar] = true;

            Assert.IsTrue(HumanPoseSkeleton33.Definition.TryGetHeadPose(positions, tracked, out var pose));
            Assert.AreEqual(Vector3.zero, pose.Position);
            Assert.Greater(Vector3.Dot(pose.Forward, Vector3.forward), 0.999f);
            Assert.Greater(Vector3.Dot(pose.Up, Vector3.up), 0.999f);
        }

        [Test]
        public void SkeletonFrameExposesDefinitionHeadPose()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[(int)SkeletonJoint.Nose] = new Vector3(0f, 0f, 1f);
            positions[(int)SkeletonJoint.RightEar] = new Vector3(1f, 0f, 0f);
            positions[(int)SkeletonJoint.LeftEar] = new Vector3(-1f, 0f, 0f);
            tracked[(int)SkeletonJoint.Nose] = true;
            tracked[(int)SkeletonJoint.RightEar] = true;
            tracked[(int)SkeletonJoint.LeftEar] = true;
            var frame = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 1,
                receivedTime: 1.0);

            Assert.IsTrue(frame.TryGetHeadPose(out var pose));
            Assert.Greater(Vector3.Dot(pose.Forward, Vector3.forward), 0.999f);
        }

        [Test]
        public void BodyDebugVisAppliesHeadPoseInLocalSpace()
        {
            var calls = GetCalledMethods(typeof(BodyDebugVis).GetMethod(
                "UpdateHead",
                BindingFlags.Instance | BindingFlags.NonPublic));

            Assert.IsTrue(calls.Any(method => IsTransformMethod(method, "set_localPosition")));
            Assert.IsTrue(calls.Any(method => IsTransformMethod(method, "set_localRotation")));
            Assert.IsFalse(calls.Any(method => IsTransformMethod(method, "set_position")));
            Assert.IsFalse(calls.Any(method => IsTransformMethod(method, "set_rotation")));
        }

        [Test]
        public void BodyDebugVisKeepsDebugLinesInLocalPoseSpace()
        {
            var bodyMethods = typeof(BodyDebugVis).GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var calls = bodyMethods.SelectMany(GetCalledMethods).ToArray();

            Assert.IsTrue(calls.Any(method => IsLineRendererMethod(method, "set_useWorldSpace")));
            Assert.IsFalse(calls.Any(method => IsTransformMethod(method, "get_position")));
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
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];

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
        public void CocoPoseSkeleton17CanBeCreatedFromHumanPoseSkeleton33()
        {
            var type = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.CocoPoseSkeleton17");
            Assert.IsNotNull(type);

            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[(int)SkeletonJoint.Nose] = new Vector3(1f, 2f, 3f);
            tracked[(int)SkeletonJoint.Nose] = true;
            positions[(int)SkeletonJoint.LeftWrist] = new Vector3(4f, 5f, 6f);
            tracked[(int)SkeletonJoint.LeftWrist] = true;

            var source = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 7,
                receivedTime: 8.5);
            var method = type.GetMethod("TryCreateFrom", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(method);

            var args = new object[] { source, null };
            Assert.IsTrue((bool)method.Invoke(null, args));

            var cocoFrame = (SkeletonFrame)args[1];
            var definition = (SkeletonDefinition)type.GetField("Definition", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            Assert.AreSame(definition, cocoFrame.Definition);
            Assert.AreEqual(17, cocoFrame.Positions.Count);
            Assert.IsTrue(cocoFrame.Nose.IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), cocoFrame.Nose.Position);
            Assert.IsTrue(cocoFrame.TryGetJoint(SkeletonJoint.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(4f, 5f, 6f), wrist);
            Assert.IsFalse(cocoFrame.LeftPinky.IsTracked);
            Assert.AreEqual(7, cocoFrame.SequenceNumber);
            Assert.AreEqual(8.5, cocoFrame.ReceivedTime);
        }

        [Test]
        public void SkeletonFrameCanConvertToTargetDefinition()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[(int)SkeletonJoint.Nose] = new Vector3(1f, 2f, 3f);
            tracked[(int)SkeletonJoint.Nose] = true;
            var source = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 2,
                receivedTime: 4.0);
            var method = typeof(SkeletonFrame).GetMethod(
                "TryConvertTo",
                new[] { typeof(SkeletonDefinition), typeof(SkeletonFrame).MakeByRefType() });

            Assert.IsNotNull(method);

            var args = new object[] { CocoPoseSkeleton17.Definition, null };
            Assert.IsTrue((bool)method.Invoke(source, args));

            var converted = (SkeletonFrame)args[1];
            Assert.AreSame(CocoPoseSkeleton17.Definition, converted.Definition);
            Assert.IsTrue(converted.Nose.IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), converted.Nose.Position);
        }

        [Test]
        public void CommServerExposesExtensibleInputSkeletonSelection()
        {
            var selectionType = typeof(CommServer).Assembly.GetType("HEXLab.Hextrackingconnector.InputSkeletonSelection");
            Assert.IsNotNull(selectionType);
            CollectionAssert.Contains(Enum.GetNames(selectionType), "Auto");
            CollectionAssert.Contains(Enum.GetNames(selectionType), "MediaPipePose33");
            Assert.IsNotNull(typeof(CommServer).GetProperty("InputSkeleton"));
        }

        [Test]
        public void PoseFrameAcceptsOptionalSkeletonIdField()
        {
            var poseFrameType = typeof(CommServer).Assembly.GetType("HEXLab.Hextrackingconnector.PoseFrame");
            var field = poseFrameType.GetField(
                "skeleton_id",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(string), field.FieldType);
        }

        [Test]
        public void InputSkeletonRegistryResolvesKnownWireSkeletonIds()
        {
            var registry = typeof(CommServer).Assembly.GetType("HEXLab.Hextrackingconnector.InputSkeletonRegistry");
            Assert.IsNotNull(registry);

            var method = registry.GetMethod("TryResolve", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var knownArgs = new object[] { "mediapipe.pose.33", null };
            var unknownArgs = new object[] { "unknown.skeleton", null };

            Assert.IsTrue((bool)method.Invoke(null, knownArgs));
            Assert.AreSame(HumanPoseSkeleton33.Definition, ((SkeletonDefinition)knownArgs[1]));
            Assert.IsFalse((bool)method.Invoke(null, unknownArgs));
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
        public void MovingAverageSmootherClearsWindowWhenSkeletonDefinitionChanges()
        {
            var cocoType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.CocoPoseSkeleton17");
            Assert.IsNotNull(cocoType);

            var smoother = new MovingAveragePoseSmoother(windowSize: 2);
            smoother.Smooth(CreateFrame(SkeletonJoint.Nose, Vector3.zero, 1));

            var definition = (SkeletonDefinition)cocoType.GetField("Definition", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var positions = new Vector3[definition.JointCount];
            var tracked = new bool[definition.JointCount];
            positions[definition.IndexOf(SkeletonJoint.Nose)] = Vector3.one * 10f;
            tracked[definition.IndexOf(SkeletonJoint.Nose)] = true;

            var smoothed = smoother.Smooth(new SkeletonFrame(
                definition,
                positions,
                tracked,
                sequenceNumber: 2,
                receivedTime: 2.0));

            Assert.IsTrue(smoothed.TryGetJoint(SkeletonJoint.Nose, out var nose));
            Assert.AreEqual(Vector3.one * 10f, nose);
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

            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            var calibrated = new Vector3[HumanPoseSkeleton33.JointCount];
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
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
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
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
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
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
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

        private static IEnumerable<MethodBase> GetCalledMethods(MethodInfo method)
        {
            var methodBody = method?.GetMethodBody();
            var il = methodBody?.GetILAsByteArray();
            if (il == null)
            {
                yield break;
            }

            for (int offset = 0; offset < il.Length;)
            {
                var opCode = ReadOpCode(il, ref offset);
                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    MethodBase calledMethod;
                    try
                    {
                        calledMethod = method.Module.ResolveMethod(
                            token,
                            method.DeclaringType?.GetGenericArguments(),
                            method.GetGenericArguments());
                    }
                    catch (BadImageFormatException)
                    {
                        continue;
                    }

                    yield return calledMethod;
                    continue;
                }

                SkipOperand(il, ref offset, opCode.OperandType);
            }
        }

        private static OpCode ReadOpCode(byte[] il, ref int offset)
        {
            var firstByte = il[offset++];
            return firstByte == 0xfe
                ? TwoByteOpCodes[il[offset++]]
                : OneByteOpCodes[firstByte];
        }

        private static void SkipOperand(byte[] il, ref int offset, OperandType operandType)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    offset += 1;
                    break;
                case OperandType.InlineVar:
                    offset += 2;
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    offset += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    offset += 8;
                    break;
                case OperandType.InlineSwitch:
                    var switchCount = BitConverter.ToInt32(il, offset);
                    offset += 4 + switchCount * 4;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported IL operand type {operandType}.");
            }
        }

        private static bool IsTransformMethod(MethodBase method, string methodName)
        {
            return method != null &&
                   method.Name == methodName &&
                   method.DeclaringType == typeof(Transform);
        }

        private static bool IsLineRendererMethod(MethodBase method, string methodName)
        {
            return method != null &&
                   method.Name == methodName &&
                   method.DeclaringType == typeof(LineRenderer);
        }
    }
}
