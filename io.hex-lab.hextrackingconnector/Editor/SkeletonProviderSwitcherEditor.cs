using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(SkeletonProviderSwitcher))]
    public sealed class SkeletonProviderSwitcherEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.Add(new IMGUIContainer(DrawSwitchControls));
            return root;
        }

        private void DrawSwitchControls()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            var switcher = (SkeletonProviderSwitcher)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Primary"))
                {
                    switcher.UsePrimary();
                }

                if (GUILayout.Button("Use Secondary"))
                {
                    switcher.UseSecondary();
                }

                if (GUILayout.Button("Use Tertiary"))
                {
                    switcher.UseTertiary();
                }
            }
        }
    }
}
