using UnityEditor;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor.Tests
{
    class EditorTests
    {
        [Test]
        public void SkeletonProviderDrawerExistsForProviderAttribute()
        {
            var drawerType = typeof(CommServerEditor).Assembly.GetType(
                "HEXLab.Hextrackingconnector.Editor.SkeletonProviderDrawer");

            Assert.IsNotNull(drawerType);
            Assert.IsTrue(typeof(PropertyDrawer).IsAssignableFrom(drawerType));
            Assert.IsTrue(drawerType
                .GetCustomAttributes(typeof(CustomPropertyDrawer), inherit: false)
                .Length > 0);
        }

        [Test]
        public void PipelineAnalyzerUsesActiveProviderFromSwitcher()
        {
            var primaryObject = new GameObject("PrimarySource");
            var secondaryObject = new GameObject("SecondarySource");
            var switcherObject = new GameObject("Switcher");
            var primary = primaryObject.AddComponent<TestSkeletonProvider>();
            var secondary = secondaryObject.AddComponent<TestSkeletonProvider>();
            var switcher = switcherObject.AddComponent<SkeletonProviderSwitcher>();
            SetPrivateField(switcher, "primaryProvider", primary);
            SetPrivateField(switcher, "secondaryProvider", secondary);
            SetPrivateField(switcher, "activeSource", SkeletonProviderSwitchSelection.Secondary);

            try
            {
                var flow = AnalyzeFlow(switcher);

                StringAssert.Contains("SecondarySource", flow);
                StringAssert.DoesNotContain("PrimarySource", flow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primaryObject);
                UnityEngine.Object.DestroyImmediate(secondaryObject);
                UnityEngine.Object.DestroyImmediate(switcherObject);
            }
        }

        [Test]
        public void PipelineAnalyzerDoesNotFallBackToPrimaryWhenSelectedSwitcherBranchIsEmpty()
        {
            var primaryObject = new GameObject("PrimarySource");
            var switcherObject = new GameObject("Switcher");
            var primary = primaryObject.AddComponent<TestSkeletonProvider>();
            var switcher = switcherObject.AddComponent<SkeletonProviderSwitcher>();
            SetPrivateField(switcher, "primaryProvider", primary);
            SetPrivateField(switcher, "activeSource", SkeletonProviderSwitchSelection.Secondary);

            try
            {
                var flow = AnalyzeFlow(switcher);

                StringAssert.DoesNotContain("PrimarySource", flow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primaryObject);
                UnityEngine.Object.DestroyImmediate(switcherObject);
            }
        }

        private static string AnalyzeFlow(MonoBehaviour provider)
        {
            var analyzerType = typeof(CommServerEditor).Assembly.GetType(
                "HEXLab.Hextrackingconnector.Editor.SkeletonPipelineAnalyzer");
            Assert.IsNotNull(analyzerType);

            var method = analyzerType.GetMethod(
                "Analyze",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var analysis = method.Invoke(null, new object[] { provider, null });
            var flowProperty = analysis.GetType().GetProperty(
                "FlowMessage",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(flowProperty);
            return (string)flowProperty.GetValue(analysis);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class TestSkeletonProvider : MonoBehaviour, ISkeletonProvider
        {
            public event Action<SkeletonFrame> PoseReceived;

            public bool TryGetLatestPose(out SkeletonFrame pose)
            {
                pose = default;
                return false;
            }

            public void Publish(SkeletonFrame frame)
            {
                PoseReceived?.Invoke(frame);
            }
        }
    }
}
