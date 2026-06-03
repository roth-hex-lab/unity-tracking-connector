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
    class RuntimeTests
    {
        private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

        static RuntimeTests()
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
            Assert.AreEqual(0, HumanPoseSkeleton33.Definition.IndexOf(BodyJoints.Nose));
            Assert.AreEqual(32, HumanPoseSkeleton33.Definition.IndexOf(BodyJoints.RightFootIndex));
            Assert.IsTrue(HumanPoseSkeleton33.Definition.Contains(BodyJoints.LeftShoulder));
            Assert.Greater(HumanPoseSkeleton33.Definition.MirrorPairs.Count, 0);
        }

        [Test]
        public void BodyJointsAreStandaloneAnatomicalJointIds()
        {
            Assert.AreEqual(new SkeletonJointId("LeftWrist"), BodyJoints.LeftWrist);
            Assert.IsNull(typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.SkeletonJoint"));
            Assert.IsNull(typeof(HumanPoseSkeleton33).GetField("LeftWrist", BindingFlags.Static | BindingFlags.Public));
            Assert.IsNull(typeof(CocoPoseSkeleton17).GetField("LeftWrist", BindingFlags.Static | BindingFlags.Public));
            Assert.IsTrue(HumanPoseSkeleton33.Definition.Contains(BodyJoints.LeftWrist));
            Assert.IsTrue(CocoPoseSkeleton17.Definition.Contains(BodyJoints.LeftWrist));
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
        public void SkeletonProviderFieldsAdvertiseInspectorValidation()
        {
            var attributeType = typeof(SkeletonFrame).Assembly.GetType(
                "HEXLab.Hextrackingconnector.SkeletonProviderAttribute");

            Assert.IsNotNull(attributeType);
            AssertProviderFieldHasAttribute(typeof(BodyDebugVis), "skeletonProvider", attributeType, expectedAllowSelf: true);
            AssertProviderFieldHasAttribute(typeof(BodyCalibration), "skeletonProvider", attributeType, expectedAllowSelf: false);
            AssertProviderFieldHasAttribute(typeof(SkeletonConverter), "sourceProvider", attributeType, expectedAllowSelf: false);
            AssertProviderFieldHasAttribute(typeof(DirectHumanoidBoneDriver), "skeletonProvider", attributeType, expectedAllowSelf: true);
        }

        [Test]
        public void SkeletonProviderUtilityExplainsInvalidAssignments()
        {
            var utilityType = typeof(SkeletonFrame).Assembly.GetType(
                "HEXLab.Hextrackingconnector.SkeletonProviderUtility");
            Assert.IsNotNull(utilityType);

            var method = utilityType.GetMethod(
                "GetValidationMessage",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(MonoBehaviour), typeof(MonoBehaviour), typeof(bool) },
                null);
            Assert.IsNotNull(method);

            var gameObject = new GameObject("ProviderValidationTest");
            try
            {
                var owner = gameObject.AddComponent<BodyCalibration>();
                var invalidProvider = gameObject.AddComponent<BodyDebugVis>();

                var invalidMessage = (string)method.Invoke(null, new object[] { invalidProvider, owner, true });
                Assert.IsNotNull(invalidMessage);
                StringAssert.Contains(nameof(ISkeletonProvider), invalidMessage);

                var selfMessage = (string)method.Invoke(null, new object[] { owner, owner, false });
                Assert.IsNotNull(selfMessage);
                StringAssert.Contains("itself", selfMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SkeletonDefinitionProvidesHeadPose()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[HumanPoseIndex(BodyJoints.Nose)] = new Vector3(0f, 0f, 1f);
            positions[HumanPoseIndex(BodyJoints.RightEar)] = new Vector3(1f, 0f, 0f);
            positions[HumanPoseIndex(BodyJoints.LeftEar)] = new Vector3(-1f, 0f, 0f);
            tracked[HumanPoseIndex(BodyJoints.Nose)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightEar)] = true;
            tracked[HumanPoseIndex(BodyJoints.LeftEar)] = true;

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
            positions[HumanPoseIndex(BodyJoints.Nose)] = new Vector3(0f, 0f, 1f);
            positions[HumanPoseIndex(BodyJoints.RightEar)] = new Vector3(1f, 0f, 0f);
            positions[HumanPoseIndex(BodyJoints.LeftEar)] = new Vector3(-1f, 0f, 0f);
            tracked[HumanPoseIndex(BodyJoints.Nose)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightEar)] = true;
            tracked[HumanPoseIndex(BodyJoints.LeftEar)] = true;
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

            positions[HumanPoseIndex(BodyJoints.LeftWrist)] = new Vector3(1f, 2f, 3f);
            tracked[HumanPoseIndex(BodyJoints.LeftWrist)] = true;

            var frame = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 12,
                receivedTime: 34.5);

            Assert.AreSame(HumanPoseSkeleton33.Definition, frame.Definition);
            Assert.IsTrue(frame[BodyJoints.LeftWrist].IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), frame[BodyJoints.LeftWrist].Position);
            Assert.IsFalse(frame[BodyJoints.RightWrist].IsTracked);
            Assert.IsTrue(frame.TryGetJoint(BodyJoints.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), wrist);
            Assert.AreEqual(12, frame.SequenceNumber);
            Assert.AreEqual(34.5, frame.ReceivedTime);
        }

        [Test]
        public void SkeletonPoseStoresJointChannelsWithoutFrameTiming()
        {
            var joint = new SkeletonJointId("TrackedBone");
            var definition = new SkeletonDefinition(
                "test.rich-pose",
                "Rich Test Pose",
                new[] { joint });
            var rotation = Quaternion.Euler(10f, 20f, 30f);
            var pose = new SkeletonPose(
                definition,
                new[]
                {
                    SkeletonJointPose.FromPositionAndRotation(
                        new Vector3(1f, 2f, 3f),
                        rotation,
                        0.75f,
                        SkeletonDataProvenance.Inferred,
                        "left-shoulder/right-shoulder midpoint"),
                },
                SkeletonCoordinateSpace.RootRelative);

            Assert.AreSame(definition, pose.Definition);
            Assert.AreEqual(SkeletonCoordinateSpace.RootRelative, pose.CoordinateSpace);
            Assert.IsTrue(pose.TryGetJointPose(joint, out var jointPose));
            Assert.IsTrue(jointPose.HasPosition);
            Assert.IsTrue(jointPose.HasRotation);
            Assert.IsTrue(jointPose.HasConfidence);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), jointPose.Position);
            Assert.AreEqual(rotation, jointPose.Rotation);
            Assert.AreEqual(0.75f, jointPose.Confidence);
            Assert.AreEqual(SkeletonDataProvenance.Inferred, jointPose.Provenance);
            Assert.AreEqual("left-shoulder/right-shoulder midpoint", jointPose.Source);
        }

        [Test]
        public void SkeletonFrameWrapsPoseWithMetadata()
        {
            var joint = new SkeletonJointId("RotationOnly");
            var definition = new SkeletonDefinition(
                "test.frame-metadata",
                "Frame Metadata Test",
                new[] { joint });
            var rotation = Quaternion.Euler(0f, 45f, 0f);
            var pose = new SkeletonPose(
                definition,
                new[]
                {
                    SkeletonJointPose.FromRotation(
                        rotation,
                        0.5f,
                        SkeletonDataProvenance.Direct),
                });
            var metadata = new SkeletonFrameMetadata(
                sequenceNumber: 42,
                receivedTime: 12.5,
                sourceTimestamp: 11.5,
                sourceId: "unit-test");
            var frame = new SkeletonFrame(pose, metadata);

            Assert.AreSame(definition, frame.Pose.Definition);
            Assert.AreEqual(42, frame.SequenceNumber);
            Assert.AreEqual(12.5, frame.ReceivedTime);
            Assert.AreEqual(11.5, frame.Metadata.SourceTimestamp);
            Assert.AreEqual("unit-test", frame.Metadata.SourceId);
            Assert.IsTrue(frame.TryGetRotation(joint, out var retrievedRotation));
            Assert.AreEqual(rotation, retrievedRotation);
            Assert.IsFalse(frame.TryGetJoint(joint, out _));
        }

        [Test]
        public void UnityHumanoidControlSkeletonExposesHumanBodyBoneMapping()
        {
            Assert.AreEqual("unity.humanoid.control", UnityHumanoidControlSkeleton.Definition.Id);
            Assert.IsTrue(UnityHumanoidControlSkeleton.Definition.Contains(UnityHumanoidControlSkeleton.Hips));
            Assert.IsTrue(UnityHumanoidControlSkeleton.Definition.Contains(UnityHumanoidControlSkeleton.LeftUpperArm));
            Assert.IsTrue(UnityHumanoidControlSkeleton.TryGetHumanBodyBone(
                UnityHumanoidControlSkeleton.LeftUpperArm,
                out var bone));
            Assert.AreEqual(HumanBodyBones.LeftUpperArm, bone);
        }

        [Test]
        public void HumanoidRetargeterCreatesBestEffortControlPoseFromHumanPose33()
        {
            var source = CreateStandingHumanPoseFrame();

            Assert.IsTrue(UnityHumanoidPoseRetargeter.TryCreateFrom(source, out var humanoid));
            Assert.AreSame(UnityHumanoidControlSkeleton.Definition, humanoid.Definition);
            Assert.AreEqual(source.SequenceNumber, humanoid.SequenceNumber);
            Assert.AreEqual(source.ReceivedTime, humanoid.ReceivedTime);
            Assert.IsTrue(humanoid.TryGetJoint(UnityHumanoidControlSkeleton.Hips, out var hips));
            Assert.AreEqual(Vector3.zero, hips);
            Assert.IsTrue(humanoid.TryGetRotation(UnityHumanoidControlSkeleton.Hips, out var hipsRotation));
            Assert.Greater(Vector3.Dot(hipsRotation * Vector3.up, Vector3.up), 0.99f);
            Assert.IsTrue(humanoid.TryGetRotation(UnityHumanoidControlSkeleton.LeftUpperArm, out var upperArmRotation));
            Assert.Greater(Vector3.Dot((upperArmRotation * Vector3.up).normalized, Vector3.left), 0.99f);
            Assert.AreEqual(
                SkeletonDataProvenance.Inferred,
                humanoid.GetPoint(UnityHumanoidControlSkeleton.LeftUpperArm).Provenance);
            Assert.AreEqual(
                "LeftShoulder->LeftElbow",
                humanoid.GetPoint(UnityHumanoidControlSkeleton.LeftUpperArm).Source);
        }

        [Test]
        public void CocoPoseSkeleton17CanBeCreatedFromHumanPoseSkeleton33()
        {
            var type = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.CocoPoseSkeleton17");
            Assert.IsNotNull(type);

            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[HumanPoseIndex(BodyJoints.Nose)] = new Vector3(1f, 2f, 3f);
            tracked[HumanPoseIndex(BodyJoints.Nose)] = true;
            positions[HumanPoseIndex(BodyJoints.LeftWrist)] = new Vector3(4f, 5f, 6f);
            tracked[HumanPoseIndex(BodyJoints.LeftWrist)] = true;

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
            Assert.IsTrue(cocoFrame[BodyJoints.Nose].IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), cocoFrame[BodyJoints.Nose].Position);
            Assert.IsTrue(cocoFrame.TryGetJoint(BodyJoints.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(4f, 5f, 6f), wrist);
            Assert.IsFalse(cocoFrame[BodyJoints.LeftPinky].IsTracked);
            Assert.AreEqual(7, cocoFrame.SequenceNumber);
            Assert.AreEqual(8.5, cocoFrame.ReceivedTime);
        }

        [Test]
        public void SkeletonFrameCanConvertToTargetDefinition()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[HumanPoseIndex(BodyJoints.Nose)] = new Vector3(1f, 2f, 3f);
            tracked[HumanPoseIndex(BodyJoints.Nose)] = true;
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
            Assert.IsTrue(converted[BodyJoints.Nose].IsTracked);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), converted[BodyJoints.Nose].Position);
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
        public void InputSkeletonRegistryAllowsStudentWireMappers()
        {
            var definition = new SkeletonDefinition(
                "student.test.skeleton",
                "Student Test Skeleton",
                new[] { new SkeletonJointId("StudentJoint") });
            var mapper = new TestWireSkeletonMapper(definition);

            InputSkeletonRegistry.Register("student.test.skeleton", mapper);

            Assert.IsTrue(InputSkeletonRegistry.TryResolve("student.test.skeleton", out var resolvedDefinition));
            Assert.AreSame(definition, resolvedDefinition);
            Assert.IsTrue(InputSkeletonRegistry.TryGetMapper(
                InputSkeletonSelection.Auto,
                "student.test.skeleton",
                out var resolvedMapper));
            Assert.AreSame(mapper, resolvedMapper);
            Assert.IsTrue(resolvedMapper.TryMapIndex(0, out var joint));
            Assert.AreEqual(new SkeletonJointId("StudentJoint"), joint);
        }

        [Test]
        public void WireMappersDoNotOwnMirrorMode()
        {
            var method = typeof(IWireSkeletonMapper).GetMethod(nameof(IWireSkeletonMapper.TryMapIndex));

            Assert.IsNotNull(method);
            Assert.IsFalse(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PoseMirrorMode)));
        }

        [Test]
        public void SkeletonPoseMirrorReflectsPositionsAndSwapsConfiguredPairs()
        {
            var left = new SkeletonJointId("LeftTest");
            var right = new SkeletonJointId("RightTest");
            var centre = new SkeletonJointId("CentreTest");
            var definition = new SkeletonDefinition(
                "test.mirror-pairs",
                "Mirror Pairs",
                new[] { left, right, centre },
                mirrorPairs: new[] { new SkeletonJointPair(left, right) });
            var pose = new SkeletonPose(
                definition,
                new[]
                {
                    SkeletonJointPose.FromPosition(new Vector3(-1f, 2f, 3f)),
                    SkeletonJointPose.FromPosition(new Vector3(4f, 5f, 6f)),
                    SkeletonJointPose.FromPosition(new Vector3(0.5f, 7f, 8f)),
                },
                SkeletonCoordinateSpace.World);

            var mirrored = SkeletonPoseTransforms.MirrorLeftRight(pose);

            Assert.AreEqual(SkeletonCoordinateSpace.World, mirrored.CoordinateSpace);
            Assert.IsTrue(mirrored.TryGetJoint(left, out var mirroredLeft));
            Assert.IsTrue(mirrored.TryGetJoint(right, out var mirroredRight));
            Assert.IsTrue(mirrored.TryGetJoint(centre, out var mirroredCentre));
            Assert.AreEqual(new Vector3(-4f, 5f, 6f), mirroredLeft);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), mirroredRight);
            Assert.AreEqual(new Vector3(-0.5f, 7f, 8f), mirroredCentre);
        }

        [Test]
        public void CommServerMirrorReflectsParsedPositionsAndSwapsBodyPairs()
        {
            var server = (CommServer)FormatterServices.GetUninitializedObject(typeof(CommServer));
            SetPrivateField(server, "inputSkeleton", InputSkeletonSelection.MediaPipePose33);
            SetPrivateField(server, "coordinateSource", PoseCoordinateSource.Free);
            SetPrivateField(server, "mirrorMode", PoseMirrorMode.SwapLeftRight);
            var method = typeof(CommServer).GetMethod(
                "TryParseSkeletonFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var json =
                "{\"skeleton_id\":\"mediapipe.pose.33\",\"free\":[" +
                "{\"index\":0,\"x\":0.25,\"y\":1,\"z\":2}," +
                "{\"index\":15,\"x\":-1,\"y\":2,\"z\":3}," +
                "{\"index\":16,\"x\":2,\"y\":5,\"z\":6}]," +
                "\"anchored\":[]}";
            var args = new object[] { json, default(SkeletonFrame) };

            Assert.IsNotNull(method);
            Assert.IsTrue((bool)method.Invoke(server, args));
            var frame = (SkeletonFrame)args[1];

            Assert.IsTrue(frame.TryGetJoint(BodyJoints.LeftWrist, out var leftWrist));
            Assert.IsTrue(frame.TryGetJoint(BodyJoints.RightWrist, out var rightWrist));
            Assert.IsTrue(frame.TryGetJoint(BodyJoints.Nose, out var nose));
            Assert.AreEqual(new Vector3(-2f, 5f, 6f), leftWrist);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), rightWrist);
            Assert.AreEqual(new Vector3(-0.25f, 1f, 2f), nose);
        }

        [Test]
        public void InputSkeletonRegistryRejectsInvalidRegistrations()
        {
            Assert.Throws<ArgumentException>(() => InputSkeletonRegistry.Register(" ", new TestWireSkeletonMapper(
                new SkeletonDefinition(
                    "student.invalid.id",
                    "Invalid Id",
                    new[] { new SkeletonJointId("Joint") }))));

            Assert.Throws<ArgumentNullException>(() => InputSkeletonRegistry.Register("student.null.mapper", null));
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
            var first = CreateFrame(BodyJoints.LeftWrist, new Vector3(0f, 0f, 0f), 1);
            var second = CreateFrame(BodyJoints.LeftWrist, new Vector3(2f, 4f, 6f), 2);

            smoother.Smooth(first);
            var smoothed = smoother.Smooth(second);

            Assert.IsTrue(smoothed.TryGetJoint(BodyJoints.LeftWrist, out var wrist));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), wrist);
            Assert.AreEqual(2, smoothed.SequenceNumber);
        }

        [Test]
        public void MovingAverageSmootherUsesNewestMetadata()
        {
            var smoother = new MovingAveragePoseSmoother(windowSize: 3);

            smoother.Smooth(CreateFrame(BodyJoints.LeftAnkle, Vector3.zero, 1));
            var smoothed = smoother.Smooth(CreateFrame(BodyJoints.LeftAnkle, Vector3.one, 2));

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
            smoother.Smooth(CreateFrame(BodyJoints.Nose, Vector3.zero, 1));

            var definition = (SkeletonDefinition)cocoType.GetField("Definition", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var positions = new Vector3[definition.JointCount];
            var tracked = new bool[definition.JointCount];
            positions[definition.IndexOf(BodyJoints.Nose)] = Vector3.one * 10f;
            tracked[definition.IndexOf(BodyJoints.Nose)] = true;

            var smoothed = smoother.Smooth(new SkeletonFrame(
                definition,
                positions,
                tracked,
                sequenceNumber: 2,
                receivedTime: 2.0));

            Assert.IsTrue(smoothed.TryGetJoint(BodyJoints.Nose, out var nose));
            Assert.AreEqual(Vector3.one * 10f, nose);
        }

        [Test]
        public void SkeletonSmoothingIsInspectableProviderAdapter()
        {
            var smoothingType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.SkeletonSmoothing");

            Assert.IsNotNull(smoothingType);
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(smoothingType));
            Assert.IsTrue(typeof(ISkeletonProvider).IsAssignableFrom(smoothingType));
            AssertProviderFieldHasAttribute(
                smoothingType,
                "sourceProvider",
                typeof(SkeletonProviderAttribute),
                expectedAllowSelf: false);
        }

        [Test]
        public void SkeletonSmoothingPublishesSmoothedFramesFromSourceProvider()
        {
            var smoothingType = typeof(SkeletonFrame).Assembly.GetType("HEXLab.Hextrackingconnector.SkeletonSmoothing");
            Assert.IsNotNull(smoothingType);

            var gameObject = new GameObject("SkeletonSmoothingProviderTest");
            gameObject.SetActive(false);
            var source = gameObject.AddComponent<TestSkeletonProvider>();
            var smoothing = (MonoBehaviour)gameObject.AddComponent(smoothingType);
            SetPrivateField(smoothing, "sourceProvider", source);
            SetPrivateField(smoothing, "smoothingMode", PoseSmoothingMode.MovingAverage);
            SetPrivateField(smoothing, "movingAverageWindowSize", 2);

            var received = default(SkeletonFrame);
            var receivedCount = 0;
            ((ISkeletonProvider)smoothing).PoseReceived += frame =>
            {
                received = frame;
                receivedCount++;
            };

            try
            {
                gameObject.SetActive(true);
                source.Publish(CreateFrame(BodyJoints.LeftWrist, Vector3.zero, 1));
                source.Publish(CreateFrame(BodyJoints.LeftWrist, new Vector3(2f, 4f, 6f), 2));

                Assert.AreEqual(2, receivedCount);
                Assert.IsTrue(((ISkeletonProvider)smoothing).TryGetLatestPose(out var latest));
                Assert.AreEqual(received.SequenceNumber, latest.SequenceNumber);
                Assert.IsTrue(received.TryGetJoint(BodyJoints.LeftWrist, out var wrist));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), wrist);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CommServerDoesNotOwnSmoothingConfiguration()
        {
            Assert.IsNull(typeof(CommServer).GetProperty("SmoothingMode"));
            Assert.IsNull(typeof(CommServer).GetProperty("MovingAverageWindowSize"));
            Assert.IsNull(typeof(CommServer).GetMethod("ResetSmoother"));
            Assert.IsNull(typeof(CommServer).GetField("smoothingMode", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(CommServer).GetField("movingAverageWindowSize", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void BodyCalibrationIsAComponentWithCalibrateCommand()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(BodyCalibration)));
            Assert.IsTrue(typeof(ISkeletonProvider).IsAssignableFrom(typeof(BodyCalibration)));
            Assert.IsNotNull(typeof(BodyCalibration).GetMethod(nameof(BodyCalibration.Calibrate), System.Type.EmptyTypes));
        }

        [Test]
        public void BodyCalibrationPublishesCalibratedFramesFromSourceProvider()
        {
            var gameObject = new GameObject("BodyCalibrationProviderTest");
            gameObject.SetActive(false);
            var source = gameObject.AddComponent<TestSkeletonProvider>();
            var calibration = gameObject.AddComponent<BodyCalibration>();
            SetPrivateField(calibration, "skeletonProvider", source);
            SetPrivateField(calibration, "autoCalibrate", true);
            SetPrivateField(calibration, "calibrationMode", BodyCalibrationMode.CenterHips);

            var received = default(SkeletonFrame);
            var receivedPose = false;
            ((ISkeletonProvider)calibration).PoseReceived += frame =>
            {
                received = frame;
                receivedPose = true;
            };

            try
            {
                gameObject.SetActive(true);
                source.Publish(CreateStandingHumanPoseFrame());

                Assert.IsTrue(receivedPose);
                Assert.IsTrue(calibration.HasCalibration);
                Assert.IsTrue(((ISkeletonProvider)calibration).TryGetLatestPose(out var latest));
                Assert.AreEqual(received.SequenceNumber, latest.SequenceNumber);
                Assert.IsTrue(received.TryGetJoint(BodyJoints.LeftHip, out var leftHip));
                Assert.IsTrue(received.TryGetJoint(BodyJoints.RightHip, out var rightHip));
                Assert.AreEqual(Vector3.zero, leftHip + rightHip);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BodyCalibrationTryApplyPreservesRotationChannels()
        {
            var calibration = (BodyCalibration)FormatterServices.GetUninitializedObject(typeof(BodyCalibration));
            SetPrivateField(calibration, "calibrationOffset", new Vector3(1f, 2f, 3f));
            SetPrivateField(calibration, "hasCalibration", true);

            var rotation = Quaternion.Euler(5f, 15f, 25f);
            var source = new SkeletonFrame(
                new SkeletonPose(
                    UnityHumanoidControlSkeleton.Definition,
                    CreateUnityHumanoidPoses(
                        (UnityHumanoidControlSkeleton.Hips,
                            SkeletonJointPose.FromPositionAndRotation(
                                new Vector3(2f, 3f, 4f),
                                rotation,
                                0.8f,
                                SkeletonDataProvenance.Direct,
                                "unit-test"))),
                    SkeletonCoordinateSpace.RootRelative),
                new SkeletonFrameMetadata(12, 34.5, 33.5, "source"));

            Assert.IsTrue(calibration.TryApply(source, out var calibrated));
            Assert.IsTrue(calibrated.TryGetJointPose(UnityHumanoidControlSkeleton.Hips, out var hips));
            Assert.AreEqual(new Vector3(3f, 5f, 7f), hips.Position);
            Assert.IsTrue(hips.HasRotation);
            Assert.AreEqual(rotation, hips.Rotation);
            Assert.AreEqual(0.8f, hips.Confidence);
            Assert.AreEqual(SkeletonDataProvenance.Direct, hips.Provenance);
            Assert.AreEqual("unit-test", hips.Source);
            Assert.AreEqual(12, calibrated.SequenceNumber);
            Assert.AreEqual("source", calibrated.Metadata.SourceId);
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
            positions[HumanPoseIndex(BodyJoints.LeftHip)] = new Vector3(-1f, 2f, 3f);
            positions[HumanPoseIndex(BodyJoints.RightHip)] = new Vector3(3f, 4f, 5f);
            tracked[HumanPoseIndex(BodyJoints.LeftHip)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightHip)] = true;

            calibration.Apply(positions, tracked, calibrated);

            Assert.AreEqual(Vector3.zero, calibrated[HumanPoseIndex(BodyJoints.LeftHip)] + calibrated[HumanPoseIndex(BodyJoints.RightHip)]);
            Assert.IsTrue(calibration.HasCalibration);
        }

        [Test]
        public void CalibrationCanCenterHipMidpointAtOrigin()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[HumanPoseIndex(BodyJoints.LeftHip)] = new Vector3(-1f, 2f, 3f);
            positions[HumanPoseIndex(BodyJoints.RightHip)] = new Vector3(3f, 4f, 5f);
            tracked[HumanPoseIndex(BodyJoints.LeftHip)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightHip)] = true;

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
            positions[HumanPoseIndex(BodyJoints.LeftHip)] = new Vector3(-1f, 2f, 2f);
            positions[HumanPoseIndex(BodyJoints.RightHip)] = new Vector3(3f, 2f, 4f);
            positions[HumanPoseIndex(BodyJoints.LeftAnkle)] = new Vector3(-1f, -0.25f, 2f);
            positions[HumanPoseIndex(BodyJoints.RightFootIndex)] = new Vector3(3f, -0.75f, 4f);
            tracked[HumanPoseIndex(BodyJoints.LeftHip)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightHip)] = true;
            tracked[HumanPoseIndex(BodyJoints.LeftAnkle)] = true;
            tracked[HumanPoseIndex(BodyJoints.RightFootIndex)] = true;

            var offset = BodyCalibration.CalculateOffset(
                positions,
                tracked,
                BodyCalibrationMode.CenterHipsGroundFeet,
                groundHeight: 0f);

            Assert.AreEqual(new Vector3(-1f, 0.75f, -3f), offset);
        }

        [Test]
        public void CalibrationCanUseUnityHumanoidHipsAndFeet()
        {
            var positions = new Vector3[UnityHumanoidControlSkeleton.Definition.JointCount];
            var tracked = new bool[UnityHumanoidControlSkeleton.Definition.JointCount];
            SetTracked(UnityHumanoidControlSkeleton.Hips, new Vector3(2f, 3f, 4f));
            SetTracked(UnityHumanoidControlSkeleton.LeftFoot, new Vector3(1f, 0.5f, 3f));
            SetTracked(UnityHumanoidControlSkeleton.RightToes, new Vector3(3f, -1f, 5f));

            var offset = BodyCalibration.CalculateOffset(
                UnityHumanoidControlSkeleton.Definition,
                positions,
                tracked,
                BodyCalibrationMode.CenterHipsGroundFeet,
                groundHeight: 0f);

            Assert.AreEqual(new Vector3(-2f, 1f, -4f), offset);

            void SetTracked(SkeletonJointId joint, Vector3 position)
            {
                var index = UnityHumanoidControlSkeleton.Definition.IndexOf(joint);
                positions[index] = position;
                tracked[index] = true;
            }
        }

        [Test]
        public void CalibrationFallsBackToTrackedCenterAndLowestPoint()
        {
            var first = new SkeletonJointId("First");
            var second = new SkeletonJointId("Second");
            var third = new SkeletonJointId("Third");
            var definition = new SkeletonDefinition(
                "test.generic-calibration",
                "Generic Calibration",
                new[] { first, second, third });
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 2f, 2f),
                new Vector3(4f, -1f, 4f),
            };
            var tracked = new[] { true, true, true };

            var offset = BodyCalibration.CalculateOffset(
                definition,
                positions,
                tracked,
                BodyCalibrationMode.CenterHipsGroundFeet,
                groundHeight: 0f);

            Assert.AreEqual(new Vector3(-2f, 1f, -2f), offset);
        }

        [Test]
        public void HumanoidRetargeterDoesNotInventRestRotationsForCopiedJoints()
        {
            Assert.IsTrue(UnityHumanoidPoseRetargeter.TryCreateFrom(CreateStandingHumanPoseFrame(), out var humanoid));

            Assert.IsTrue(humanoid.TryGetJoint(UnityHumanoidControlSkeleton.LeftShoulder, out _));
            Assert.IsFalse(humanoid.TryGetRotation(UnityHumanoidControlSkeleton.LeftShoulder, out _));
            Assert.IsTrue(humanoid.TryGetJoint(UnityHumanoidControlSkeleton.LeftHand, out _));
            Assert.IsFalse(humanoid.TryGetRotation(UnityHumanoidControlSkeleton.LeftHand, out _));
        }

        [Test]
        public void HumanoidRetargeterInfersHandRotationFromHandLandmarks()
        {
            var source = CreateStandingHumanPoseFrame();
            var poses = source.CopyJointPoses();
            SetHumanPose(poses, BodyJoints.LeftIndex, new Vector3(-1.75f, 1.95f, 0f));
            SetHumanPose(poses, BodyJoints.LeftPinky, new Vector3(-2.15f, 1.85f, 0f));
            SetHumanPose(poses, BodyJoints.LeftThumb, new Vector3(-1.95f, 1.65f, 0.35f));
            source = new SkeletonFrame(
                new SkeletonPose(HumanPoseSkeleton33.Definition, poses),
                source.Metadata);

            Assert.IsTrue(UnityHumanoidPoseRetargeter.TryCreateFrom(source, out var humanoid));
            Assert.IsTrue(humanoid.TryGetJointPose(UnityHumanoidControlSkeleton.LeftHand, out var hand));
            Assert.IsTrue(hand.HasRotation);
            Assert.AreEqual(SkeletonDataProvenance.Inferred, hand.Provenance);
            Assert.AreEqual("LeftWrist+LeftIndex+LeftPinky+LeftThumb", hand.Source);
            Assert.Greater(Vector3.Dot((hand.Rotation * Vector3.up).normalized, new Vector3(-0.2f, 0.98f, 0f).normalized), 0.9f);
        }

        [Test]
        public void DirectHumanoidBoneDriverIgnoresRestRotations()
        {
            var method = typeof(DirectHumanoidBoneDriver).GetMethod(
                "ShouldDriveRotation",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            Assert.IsFalse((bool)method.Invoke(null, new object[]
            {
                SkeletonJointPose.FromRotation(Quaternion.identity, 0f, SkeletonDataProvenance.Rest, "rest")
            }));
            Assert.IsFalse((bool)method.Invoke(null, new object[]
            {
                SkeletonJointPose.FromRotation(Quaternion.identity, 0f, SkeletonDataProvenance.Inferred, "missing")
            }));
            Assert.IsTrue((bool)method.Invoke(null, new object[]
            {
                SkeletonJointPose.FromRotation(Quaternion.Euler(0f, 30f, 0f), 0.5f, SkeletonDataProvenance.Inferred, "tracked")
            }));
        }

        private static SkeletonFrame CreateFrame(SkeletonJointId joint, Vector3 position, int sequenceNumber)
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];
            positions[HumanPoseIndex(joint)] = position;
            tracked[HumanPoseIndex(joint)] = true;
            return new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber,
                receivedTime: sequenceNumber);
        }

        private static SkeletonFrame CreateStandingHumanPoseFrame()
        {
            var positions = new Vector3[HumanPoseSkeleton33.JointCount];
            var tracked = new bool[HumanPoseSkeleton33.JointCount];

            SetTracked(BodyJoints.LeftHip, new Vector3(-0.5f, 0f, 0f));
            SetTracked(BodyJoints.RightHip, new Vector3(0.5f, 0f, 0f));
            SetTracked(BodyJoints.LeftShoulder, new Vector3(-0.75f, 1.5f, 0f));
            SetTracked(BodyJoints.RightShoulder, new Vector3(0.75f, 1.5f, 0f));
            SetTracked(BodyJoints.LeftElbow, new Vector3(-1.25f, 1.5f, 0f));
            SetTracked(BodyJoints.RightElbow, new Vector3(1.25f, 1.5f, 0f));
            SetTracked(BodyJoints.LeftWrist, new Vector3(-1.75f, 1.5f, 0f));
            SetTracked(BodyJoints.RightWrist, new Vector3(1.75f, 1.5f, 0f));
            SetTracked(BodyJoints.LeftKnee, new Vector3(-0.5f, -1f, 0f));
            SetTracked(BodyJoints.RightKnee, new Vector3(0.5f, -1f, 0f));
            SetTracked(BodyJoints.LeftAnkle, new Vector3(-0.5f, -2f, 0f));
            SetTracked(BodyJoints.RightAnkle, new Vector3(0.5f, -2f, 0f));
            SetTracked(BodyJoints.LeftFootIndex, new Vector3(-0.5f, -2f, 0.5f));
            SetTracked(BodyJoints.RightFootIndex, new Vector3(0.5f, -2f, 0.5f));
            SetTracked(BodyJoints.Nose, new Vector3(0f, 2.1f, 0.5f));
            SetTracked(BodyJoints.LeftEar, new Vector3(-0.25f, 2f, 0f));
            SetTracked(BodyJoints.RightEar, new Vector3(0.25f, 2f, 0f));

            return new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                sequenceNumber: 99,
                receivedTime: 100.5);

            void SetTracked(SkeletonJointId joint, Vector3 position)
            {
                positions[HumanPoseIndex(joint)] = position;
                tracked[HumanPoseIndex(joint)] = true;
            }
        }

        private static SkeletonJointPose[] CreateUnityHumanoidPoses(params (SkeletonJointId joint, SkeletonJointPose pose)[] values)
        {
            var poses = new SkeletonJointPose[UnityHumanoidControlSkeleton.Definition.JointCount];
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = SkeletonJointPose.Unavailable;
            }

            foreach (var value in values)
            {
                poses[UnityHumanoidControlSkeleton.Definition.IndexOf(value.joint)] = value.pose;
            }

            return poses;
        }

        private static void SetHumanPose(SkeletonJointPose[] poses, SkeletonJointId joint, Vector3 position)
        {
            poses[HumanPoseIndex(joint)] = SkeletonJointPose.FromPosition(position, 1f, SkeletonDataProvenance.Direct, joint.ToString());
        }

        private static int HumanPoseIndex(SkeletonJointId joint)
        {
            return HumanPoseSkeleton33.Definition.IndexOf(joint);
        }

        private static void AssertProviderFieldHasAttribute(
            Type componentType,
            string fieldName,
            Type attributeType,
            bool expectedAllowSelf)
        {
            var field = componentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(MonoBehaviour), field.FieldType);

            var attribute = field.GetCustomAttributes(attributeType, inherit: false).SingleOrDefault();
            Assert.IsNotNull(attribute);

            var allowSelfProperty = attributeType.GetProperty("AllowSelf", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(allowSelfProperty);
            Assert.AreEqual(expectedAllowSelf, (bool)allowSelfProperty.GetValue(attribute));
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class TestSkeletonProvider : MonoBehaviour, ISkeletonProvider
        {
            private SkeletonFrame latestPose;
            private bool hasLatestPose;

            public event Action<SkeletonFrame> PoseReceived;

            public bool TryGetLatestPose(out SkeletonFrame pose)
            {
                pose = latestPose;
                return hasLatestPose;
            }

            public void Publish(SkeletonFrame frame)
            {
                latestPose = frame;
                hasLatestPose = true;
                PoseReceived?.Invoke(frame);
            }
        }

        private sealed class TestWireSkeletonMapper : IWireSkeletonMapper
        {
            public TestWireSkeletonMapper(SkeletonDefinition definition)
            {
                Definition = definition;
            }

            public SkeletonDefinition Definition { get; }

            public bool TryMapIndex(int sourceIndex, out SkeletonJointId joint)
            {
                if (sourceIndex == 0)
                {
                    joint = new SkeletonJointId("StudentJoint");
                    return true;
                }

                joint = default;
                return false;
            }
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
