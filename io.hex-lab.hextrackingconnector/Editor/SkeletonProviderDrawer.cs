using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomPropertyDrawer(typeof(SkeletonProviderAttribute))]
    public sealed class SkeletonProviderDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var field = new ObjectField(property.displayName)
            {
                objectType = typeof(MonoBehaviour),
                allowSceneObjects = !EditorUtility.IsPersistent(property.serializedObject.targetObject),
                bindingPath = property.propertyPath,
                tooltip = property.tooltip,
            };
            field.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            root.Add(field);

            var validationBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            var flowBox = new HelpBox(string.Empty, HelpBoxMessageType.None);
            flowBox.Q<Label>().enableRichText = true;
            var warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(validationBox);
            root.Add(flowBox);
            root.Add(warningBox);

            void Refresh()
            {
                if (property.serializedObject.targetObject == null)
                {
                    return;
                }

                var validation = GetValidationMessage(property);
                var analysis = validation == null ? GetPipelineAnalysis(property) : SkeletonPipelineAnalysis.Empty;
                SetMessage(validationBox, validation);
                SetMessage(flowBox, analysis.HasFlow
                    ? "<b>Flow:</b>" + analysis.FlowMessage.Substring("Flow:".Length)
                    : null);
                SetMessage(warningBox, analysis.WarningMessage);
            }

            Refresh();
            root.TrackPropertyValue(property, _ => Refresh());
            // Changes to other pipeline components (including runtime switching) do not
            // change this serialized reference. Scheduled work pauses when the UI detaches.
            root.schedule.Execute(Refresh).Every(250);

            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                !typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType))
            {
                EditorGUI.PropertyField(position, property, label, includeChildren: true);
                return;
            }

            // Third-party IMGUI inspectors cannot host UI Toolkit drawers. Keep the
            // reference editable there, with pipeline details available in the tooltip.
            var content = new GUIContent(label);
            var validation = GetValidationMessage(property);
            var analysis = validation == null ? GetPipelineAnalysis(property) : SkeletonPipelineAnalysis.Empty;
            content.tooltip = validation ?? string.Join("\n", new[] { analysis.FlowMessage, analysis.WarningMessage });
            using (new EditorGUI.PropertyScope(position, content, property))
            {
                EditorGUI.ObjectField(position, property, typeof(MonoBehaviour), content);
            }
        }

        private static void SetMessage(HelpBox box, string message)
        {
            box.text = message ?? string.Empty;
            box.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private string GetValidationMessage(SerializedProperty property)
        {
            var component = property.objectReferenceValue as MonoBehaviour;
            if (component == null)
            {
                return null;
            }

            var owner = property.serializedObject.targetObject as MonoBehaviour;
            var providerAttribute = (SkeletonProviderAttribute)attribute;
            return SkeletonProviderUtility.GetValidationMessage(
                component,
                owner,
                providerAttribute.AllowSelf);
        }

        private static SkeletonPipelineAnalysis GetPipelineAnalysis(SerializedProperty property)
        {
            var component = property.objectReferenceValue as MonoBehaviour;
            if (component == null)
            {
                return SkeletonPipelineAnalysis.Empty;
            }

            var owner = property.serializedObject.targetObject as MonoBehaviour;
            return SkeletonPipelineAnalyzer.Analyze(component, owner);
        }
    }
}
