using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomPropertyDrawer(typeof(SkeletonProviderAttribute))]
    public sealed class SkeletonProviderDrawer : PropertyDrawer
    {
        private const float HelpBoxPadding = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            var message = GetValidationMessage(property);
            if (message != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing + GetHelpBoxHeight(message);
                return height;
            }

            var analysis = GetPipelineAnalysis(property);
            if (analysis.HasFlow)
            {
                height += EditorGUIUtility.standardVerticalSpacing + GetHelpBoxHeight(analysis.FlowMessage);
            }

            if (analysis.HasWarnings)
            {
                height += EditorGUIUtility.standardVerticalSpacing + GetHelpBoxHeight(analysis.WarningMessage);
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                !typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType))
            {
                EditorGUI.PropertyField(position, property, label, includeChildren: true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var fieldRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.ObjectField(
                fieldRect,
                property,
                typeof(MonoBehaviour),
                label);

            var message = GetValidationMessage(property);
            if (message != null)
            {
                DrawHelpBox(position, ref fieldRect, message, MessageType.Error);
            }
            else
            {
                var analysis = GetPipelineAnalysis(property);
                if (analysis.HasFlow)
                {
                    DrawHelpBox(position, ref fieldRect, analysis.FlowMessage, MessageType.Info);
                }

                if (analysis.HasWarnings)
                {
                    DrawHelpBox(position, ref fieldRect, analysis.WarningMessage, MessageType.Warning);
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawHelpBox(
            Rect position,
            ref Rect previousRect,
            string message,
            MessageType messageType)
        {
            var helpRect = new Rect(
                position.x,
                previousRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                GetHelpBoxHeight(message, position.width));
            EditorGUI.HelpBox(helpRect, message, messageType);
            previousRect = helpRect;
        }

        private static float GetHelpBoxHeight(string message, float width = 0f)
        {
            width = width > 0f
                ? width
                : Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 48f);

            return Mathf.Max(
                EditorGUIUtility.singleLineHeight * 2f + HelpBoxPadding,
                EditorStyles.helpBox.CalcHeight(new GUIContent(message), width) + HelpBoxPadding);
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
