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
            if (GetValidationMessage(property) != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing +
                          EditorGUIUtility.singleLineHeight * 2f +
                          HelpBoxPadding;
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
                var helpRect = new Rect(
                    position.x,
                    fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    EditorGUIUtility.singleLineHeight * 2f + HelpBoxPadding);
                EditorGUI.HelpBox(helpRect, message, MessageType.Error);
            }

            EditorGUI.EndProperty();
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
    }
}
