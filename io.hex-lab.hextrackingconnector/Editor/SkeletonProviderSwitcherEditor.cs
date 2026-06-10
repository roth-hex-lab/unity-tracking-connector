using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(SkeletonProviderSwitcher))]
    public sealed class SkeletonProviderSwitcherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

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
            }
        }
    }
}
