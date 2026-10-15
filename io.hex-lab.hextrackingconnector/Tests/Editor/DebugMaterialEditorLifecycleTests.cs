using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor.Tests
{
    public class DebugMaterialEditorLifecycleTests
    {
        [Test]
        public void CleanupOutsidePlayModeReleasesOwnedResourcesImmediately()
        {
            var root = new GameObject("EditorDebugMaterialFixture");
            var owner = new GameObject("Visualization");
            owner.transform.SetParent(root.transform);
            owner.SetActive(false);
            var templates = new GameObject("Templates");
            templates.transform.SetParent(root.transform);
            templates.SetActive(false);
            var joint = new GameObject("Joint", typeof(MeshRenderer));
            joint.transform.SetParent(templates.transform);
            var line = new GameObject("Line", typeof(LineRenderer));
            line.transform.SetParent(templates.transform);
            var parent = new GameObject("External Visuals").transform;
            parent.SetParent(root.transform);
            Material material = null;
            Material replacement = null;
            try
            {
                var visual = owner.AddComponent<BodyDebugVis>();
                Set(visual, "jointPrefab", joint);
                Set(visual, "linePrefab", line);
                Set(visual, "parent", parent);
                Set(visual, "jointColor", new Color(0.5678f, 0.3456f, 0.1234f, 1f));
                owner.SetActive(true);
                // Non-ExecuteAlways components do not receive these callbacks in EditMode.
                // Exercise the synchronous cleanup branch used when Play Mode has ended.
                Invoke(visual, "OnEnable");
                material = parent.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                Assert.IsTrue(material != null);
                Invoke(visual, "OnDisable");
                Assert.IsTrue(material == null);
                Assert.AreEqual(0, parent.childCount);
                Invoke(visual, "OnEnable");
                replacement = parent.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                Assert.IsTrue(replacement != null);
                Invoke(visual, "OnDisable");
                Invoke(visual, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(owner);
                Assert.IsTrue(replacement == null);
                Assert.AreEqual(0, parent.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
