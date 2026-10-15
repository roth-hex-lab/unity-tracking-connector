using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class DebugMaterialLifecycleTests
    {
        [UnityTest]
        public IEnumerator DisableAndReenableReleaseOnlyThatVisualizationsResources()
        {
            using (var fixture = new VisualFixture())
            {
                var first = fixture.CreateVisual("First", out var firstParent);
                var second = fixture.CreateVisual("Second", out var secondParent);
                var firstMaterial = fixture.GetGeneratedMaterial(firstParent);
                var secondMaterial = fixture.GetGeneratedMaterial(secondParent);
                Assert.IsNotNull(firstMaterial);
                Assert.IsNotNull(secondMaterial);
                Assert.AreNotSame(firstMaterial, secondMaterial);

                var queuedDelivery = fixture.Source.SnapshotHandlers();
                first.enabled = false;
                yield return null;
                Assert.IsTrue(firstMaterial == null, "Disabling must destroy the owned native material.");
                Assert.AreEqual(0, firstParent.childCount, "External-parent visuals must also be removed.");
                Assert.IsTrue(secondMaterial != null);
                Assert.IsTrue(fixture.TemplateMaterial != null, "Borrowed prefab materials must survive.");
                Assert.AreSame(secondMaterial, fixture.GetGeneratedMaterial(secondParent));
                queuedDelivery(VisualFixture.Frame(HumanPoseSkeleton33.Definition));
                Assert.AreEqual(0, firstParent.childCount, "A queued callback must not recreate disabled visuals.");

                first.enabled = true;
                var replacement = fixture.GetGeneratedMaterial(firstParent);
                Assert.IsTrue(replacement != null);
                Assert.AreNotSame(firstMaterial, replacement);
                Assert.AreNotSame(secondMaterial, replacement);
                fixture.Source.Publish(VisualFixture.Frame(HumanPoseSkeleton33.Definition));
                Assert.IsTrue(firstParent.GetComponentInChildren<MeshRenderer>() != null,
                    "Re-enabled visuals must still respond to poses.");

                first.enabled = false;
                yield return null;
                Assert.IsTrue(replacement == null);
                Assert.AreEqual(0, firstParent.childCount);
                Assert.IsTrue(secondMaterial != null);
                Assert.IsTrue(second != null);
            }
        }

        [UnityTest]
        public IEnumerator SkeletonRebuildAndDestroyReleaseMaterialsAndExternalVisuals()
        {
            using (var fixture = new VisualFixture())
            {
                var visual = fixture.CreateVisual("Rebuilt", out var parent);
                var material = fixture.GetGeneratedMaterial(parent);
                var definitions = new[] { CocoPoseSkeleton17.Definition, HumanPoseSkeleton33.Definition };
                foreach (var definition in definitions)
                {
                    fixture.Source.Publish(VisualFixture.Frame(definition));
                    yield return null;
                    var replacement = fixture.GetGeneratedMaterial(parent);
                    Assert.AreNotSame(material, replacement);
                    Assert.IsTrue(material == null, "Rebuilding must release the previous material.");
                    Assert.AreEqual(definition.JointCount + definition.DebugLineStrips.Count + 1, parent.childCount);
                    material = replacement;
                }

                var queuedDelivery = fixture.Source.SnapshotHandlers();
                UnityEngine.Object.Destroy(visual.gameObject);
                yield return null;
                Assert.DoesNotThrow(() => queuedDelivery(VisualFixture.Frame(HumanPoseSkeleton33.Definition)));
                Assert.IsTrue(material == null);
                Assert.AreEqual(0, parent.childCount);
                Assert.IsTrue(fixture.TemplateMaterial != null);
            }
        }

        private sealed class VisualFixture : IDisposable
        {
            private readonly GameObject root = new GameObject("DebugMaterialFixture");
            private readonly GameObject joint;
            private readonly GameObject line;
            private readonly GameObject head;
            private readonly HashSet<Material> generated = new HashSet<Material>();
            internal readonly Material TemplateMaterial;
            internal readonly LifecycleTestProvider Source;

            internal VisualFixture()
            {
                Source = root.AddComponent<LifecycleTestProvider>();
                var templates = new GameObject("Templates");
                templates.transform.SetParent(root.transform);
                templates.SetActive(false);
                var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
                Assert.IsNotNull(shader);
                TemplateMaterial = new Material(shader);
                joint = new GameObject("Joint", typeof(MeshRenderer));
                joint.transform.SetParent(templates.transform);
                joint.GetComponent<MeshRenderer>().sharedMaterial = TemplateMaterial;
                line = new GameObject("Line", typeof(LineRenderer));
                line.transform.SetParent(templates.transform);
                line.GetComponent<LineRenderer>().sharedMaterial = TemplateMaterial;
                head = new GameObject("Head", typeof(MeshRenderer));
                head.transform.SetParent(templates.transform);
                head.GetComponent<MeshRenderer>().sharedMaterial = TemplateMaterial;
            }

            internal BodyDebugVis CreateVisual(string name, out Transform parent)
            {
                var owner = new GameObject(name);
                owner.transform.SetParent(root.transform);
                owner.SetActive(false);
                parent = new GameObject(name + " External Visuals").transform;
                parent.SetParent(root.transform);
                var visual = owner.AddComponent<BodyDebugVis>();
                Set(visual, "skeletonProvider", Source);
                Set(visual, "parent", parent);
                Set(visual, "jointPrefab", joint);
                Set(visual, "linePrefab", line);
                Set(visual, "headPrefab", head);
                // Isolate this fixture's materials from any visualization in the user's scene.
                Set(visual, "jointColor", new Color(0.1234f, 0.3456f, 0.5678f, 1f));
                owner.SetActive(true);
                return visual;
            }

            internal Material GetGeneratedMaterial(Transform parent)
            {
                var renderers = parent.GetComponentsInChildren<MeshRenderer>(true);
                Assert.IsNotEmpty(renderers);
                var material = renderers[0].sharedMaterial;
                foreach (var renderer in renderers) Assert.AreSame(material, renderer.sharedMaterial);
                if (material != null && material != TemplateMaterial) generated.Add(material);
                return material;
            }

            internal static SkeletonFrame Frame(SkeletonDefinition definition)
            {
                var poses = new SkeletonJointPose[definition.JointCount];
                poses[definition.IndexOf(BodyJoints.Nose)] = SkeletonJointPose.FromPosition(Vector3.up);
                return new SkeletonFrame(new SkeletonPose(definition, poses), new SkeletonFrameMetadata(1, 1));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                // Also clean up a deliberately failing regression run on the old implementation.
                foreach (var material in generated)
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (TemplateMaterial != null) UnityEngine.Object.DestroyImmediate(TemplateMaterial);
            }

            private static void Set(object target, string name, object value)
            {
                target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            }
        }
    }
}
