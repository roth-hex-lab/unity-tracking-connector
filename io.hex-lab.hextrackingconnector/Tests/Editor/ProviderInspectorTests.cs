using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HEXLab.Hextrackingconnector.Editor.Tests
{
    public class ProviderInspectorTests
    {
        [UnityTest]
        public IEnumerator MessagesReflowAtNarrowWidthsAndProviderEditsRemainBound()
        {
            using (var fixture = new InspectorFixture())
            {
                var consumer = fixture.CreateConsumer();
                using (var serialized = new SerializedObject(consumer))
                {
                    var property = serialized.FindProperty("source");
                    fixture.Content.Add(new PropertyField(property));
                    var followingField = new Label("Following field");
                    fixture.Content.Add(followingField);
                    fixture.Content.Bind(serialized);
                    yield return WaitFor(() => fixture.FlowBox != null, "Provider flow was not displayed.");

                    float wideHeight = 0f;
                    foreach (var width in new[] { 600f, 260f, 140f, 420f })
                    {
                        fixture.Content.style.width = width;
                        yield return WaitFor(() => Mathf.Abs(fixture.Content.layout.width - width) < 1f,
                            "Inspector width did not update.");
                        // Allow text measurement and its parent layout to settle after resizing.
                        yield return null;
                        yield return null;
                        var box = fixture.FlowBox;
                        var label = box.Q<Label>();
                        var required = label.MeasureTextSize(label.text, label.contentRect.width,
                            VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                        Assert.GreaterOrEqual(label.contentRect.height + 1f, required.y,
                            "Wrapped flow text must fit at width " + width);
                        Assert.GreaterOrEqual(box.worldBound.yMax + 1f, label.worldBound.yMax);
                        Assert.GreaterOrEqual(followingField.worldBound.yMin + 1f, box.worldBound.yMax,
                            "The next field must not overlap the flow box.");
                        if (width == 600f) wideHeight = box.layout.height;
                        if (width == 140f) Assert.Greater(box.layout.height, wideHeight);
                    }

                    var field = fixture.Content.Q<ObjectField>();
                    Assert.IsNotNull(field);
                    field.value = consumer;
                    yield return WaitFor(() => property.objectReferenceValue == consumer,
                        "Provider edits must update the serialized reference.");
                    yield return WaitFor(() => fixture.VisibleBox(HelpBoxMessageType.Error) != null,
                        "An invalid self reference must show validation feedback.");
                    Assert.IsNull(fixture.FlowBox);

                    field.value = fixture.Provider;
                    yield return WaitFor(() => fixture.FlowBox != null, "Valid provider feedback did not recover.");
                    Assert.IsNull(fixture.VisibleBox(HelpBoxMessageType.Error));
                }
            }
        }

        [UnityTest]
        public IEnumerator FlowAndWarningsRefreshWhenAnUpstreamProviderChanges()
        {
            using (var fixture = new InspectorFixture())
            {
                var consumer = fixture.CreateConsumer();
                using (var serialized = new SerializedObject(consumer))
                {
                    fixture.Content.Add(new PropertyField(serialized.FindProperty("source")));
                    fixture.Content.Bind(serialized);
                    yield return WaitFor(() => fixture.FlowBox != null, "Provider flow was not displayed.");
                    Assert.IsNull(fixture.VisibleBox(HelpBoxMessageType.Warning));

                    var upstream = fixture.CreateProvider("New upstream tracking source");
                    fixture.Provider.source = upstream;
                    yield return WaitFor(() => fixture.FlowBox.text.Contains("New upstream tracking source"),
                        "Flow must refresh when another component changes upstream.");
                    Assert.IsNotNull(fixture.VisibleBox(HelpBoxMessageType.Warning),
                        "Duplicate pipeline nodes must still produce warnings.");

                    fixture.Provider.source = null;
                    yield return WaitFor(() => fixture.VisibleBox(HelpBoxMessageType.Warning) == null,
                        "Obsolete warnings must disappear.");
                    StringAssert.DoesNotContain("New upstream tracking source", fixture.FlowBox.text);
                }
            }
        }

        [UnityTest]
        public IEnumerator CalibrationControlsFollowTheirEnabledTriggers()
        {
            using (var fixture = new InspectorFixture())
            {
                var calibration = fixture.CreateComponent(typeof(BodyCalibration));
                var editor = UnityEditor.Editor.CreateEditor(calibration);
                try
                {
                    var inspector = editor.CreateInspectorGUI();
                    fixture.Content.Add(inspector);
                    inspector.Bind(editor.serializedObject);
                    PropertyField Field(string path) => inspector.Query<PropertyField>().ToList()
                        .Single(field => field.bindingPath == path);
                    yield return WaitFor(() => Field("calibrateOnFirstPose").Q<Toggle>() != null,
                        "Calibration controls were not bound.");
                    Field("calibrateOnFirstPose").Q<Toggle>().value = false;
                    yield return WaitFor(() => Field("initialCalibrationMode").resolvedStyle.display == DisplayStyle.None,
                        "Disabling initial calibration must hide its mode selector.");
                    Field("continuousCalibration").Q<Toggle>().value = false;
                    yield return WaitFor(() => Field("continuousCalibrationMode").resolvedStyle.display == DisplayStyle.None,
                        "Disabling continuous calibration must hide its mode selector.");
                    Assert.AreEqual(DisplayStyle.None, Field("smoothContinuousCalibration").resolvedStyle.display);
                    Assert.AreEqual(DisplayStyle.None, Field("continuousSmoothingTime").resolvedStyle.display);
                    Assert.AreNotEqual(DisplayStyle.None, Field("groundHeight").resolvedStyle.display,
                        "Ground Height is also used by manual calibration.");
                    Field("calibrateOnFirstPose").Q<Toggle>().value = true;
                    yield return WaitFor(() => Field("initialCalibrationMode").resolvedStyle.display != DisplayStyle.None,
                        "Re-enabling initial calibration must restore its mode selector.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
        }

        [UnityTest]
        public IEnumerator PackageCustomInspectorsDisplayProviderFlow()
        {
            using (var fixture = new InspectorFixture())
            {
                foreach (var type in new[] { typeof(BodyCalibration), typeof(SkeletonProviderSwitcher), typeof(SkeletonPoseRecorder) })
                {
                    var component = fixture.CreateComponent(type);
                    var fieldName = type == typeof(BodyCalibration) ? "skeletonProvider"
                        : type == typeof(SkeletonProviderSwitcher) ? "primaryProvider" : "source";
                    using (var serialized = new SerializedObject(component))
                    {
                        serialized.FindProperty(fieldName).objectReferenceValue = fixture.Provider;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }

                    var editor = UnityEditor.Editor.CreateEditor(component);
                    try
                    {
                        var inspector = editor.CreateInspectorGUI();
                        Assert.IsNotNull(inspector, type.Name + " must host the auto-layout provider drawer.");
                        fixture.Content.Add(inspector);
                        inspector.Bind(editor.serializedObject);
                        yield return WaitFor(() => fixture.FlowBox != null,
                            type.Name + " did not display its provider flow.");
                        StringAssert.Contains(fixture.Provider.name, fixture.FlowBox.text);
                        Assert.IsTrue(inspector.Query<ObjectField>().ToList().Any(field => field.value == fixture.Provider));
                        inspector.RemoveFromHierarchy();
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(editor);
                    }
                }
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            var deadline = EditorApplication.timeSinceStartup + 4d;
            while (!condition() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), message);
        }

        private sealed class InspectorFixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Provider inspector test objects");
            private readonly ProviderInspectorTestWindow window;
            internal readonly VisualElement Content = new VisualElement();
            internal readonly ProviderInspectorTestNode Provider;

            internal InspectorFixture()
            {
                Provider = CreateProvider("Tracking source with a long descriptive name for rehabilitation guidance");
                window = ScriptableObject.CreateInstance<ProviderInspectorTestWindow>();
                window.titleContent = new GUIContent("Provider inspector test");
                window.position = new Rect(100f, 100f, 800f, 700f);
                Content.style.flexShrink = 0f;
                Content.style.width = 600f;
                window.rootVisualElement.Add(Content);
                window.ShowUtility();
            }

            internal HelpBox FlowBox => Content.Query<HelpBox>().ToList().FirstOrDefault(box =>
                box.resolvedStyle.display != DisplayStyle.None && box.text.StartsWith("<b>Flow:</b>"));

            internal HelpBox VisibleBox(HelpBoxMessageType type) => Content.Query<HelpBox>().ToList().FirstOrDefault(box =>
                box.resolvedStyle.display != DisplayStyle.None && box.messageType == type);

            internal ProviderInspectorTestNode CreateProvider(string name)
            {
                var node = new GameObject(name);
                node.transform.SetParent(root.transform);
                return node.AddComponent<ProviderInspectorTestNode>();
            }

            internal ProviderInspectorTestConsumer CreateConsumer()
            {
                var consumer = (ProviderInspectorTestConsumer)CreateComponent(typeof(ProviderInspectorTestConsumer));
                consumer.name = "Consumer with another long descriptive name for a skeleton pipeline";
                consumer.source = Provider;
                return consumer;
            }

            internal Component CreateComponent(Type type)
            {
                var owner = new GameObject(type.Name);
                owner.transform.SetParent(root.transform);
                return owner.AddComponent(type);
            }

            public void Dispose()
            {
                Content.Unbind();
                window.Close();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    internal sealed class ProviderInspectorTestWindow : EditorWindow { }

    internal sealed class ProviderInspectorTestNode : MonoBehaviour, ISkeletonProvider
    {
        [SkeletonProvider(allowSelf: false)] public MonoBehaviour source;
        public event Action<SkeletonFrame> PoseReceived { add { } remove { } }
        public bool TryGetLatestPose(out SkeletonFrame pose) { pose = default; return false; }
    }

    internal sealed class ProviderInspectorTestConsumer : MonoBehaviour
    {
        [SkeletonProvider(allowSelf: false)] public MonoBehaviour source;
    }
}
