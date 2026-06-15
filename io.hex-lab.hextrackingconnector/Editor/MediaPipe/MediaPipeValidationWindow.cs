using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal sealed class MediaPipeValidationWindow : EditorWindow
    {
        private MediaPipeValidationReport report;
        private Vector2 scrollPosition;

        public static void ShowWindow()
        {
            var window = GetWindow<MediaPipeValidationWindow>("MediaPipe Validator");
            window.minSize = new Vector2(560f, 460f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            MediaPipeInstaller.LocalSetupChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            MediaPipeInstaller.LocalSetupChanged -= Refresh;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Refresh();
            }

            if (GUILayout.Button("Open Setup", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                MediaPipeSetupWindow.ShowWindow();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (report == null)
            {
                Refresh();
            }

            DrawSummary();
            DrawResults();
        }

        private void Refresh()
        {
            report = MediaPipeSetupValidator.EvaluateSetup(includeBuildSettings: true);
            Repaint();
        }

        internal static void RefreshOpenWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<MediaPipeValidationWindow>())
            {
                window.Refresh();
            }
        }

        private void DrawSummary()
        {
            var message = report != null && report.IsReady
                ? "Local MediaPipe pose tracking is ready. Review build notes before mobile deployment."
                : "Local MediaPipe pose tracking needs attention.";
            var messageType = report != null && report.IsReady ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(message, messageType);
        }

        private void DrawResults()
        {
            if (report == null)
            {
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var group in GetGroups(report))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                foreach (var item in report.Items)
                {
                    if (item.Group != group)
                    {
                        continue;
                    }

                    DrawItem(item);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawItem(MediaPipeValidationItem item)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(GetIcon(item.Status), GUILayout.Width(24f), GUILayout.Height(20f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(item.Name, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(item.Detail))
            {
                EditorGUILayout.LabelField(item.Detail, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private static GUIContent GetIcon(MediaPipeValidationStatus status)
        {
            switch (status)
            {
                case MediaPipeValidationStatus.Pass:
                    return EditorGUIUtility.IconContent("TestPassed");
                case MediaPipeValidationStatus.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon");
                case MediaPipeValidationStatus.Error:
                    return EditorGUIUtility.IconContent("console.erroricon");
                case MediaPipeValidationStatus.Info:
                default:
                    return EditorGUIUtility.IconContent("console.infoicon");
            }
        }

        private static IEnumerable<string> GetGroups(MediaPipeValidationReport report)
        {
            var groups = new List<string>();
            foreach (var item in report.Items)
            {
                if (!groups.Contains(item.Group))
                {
                    groups.Add(item.Group);
                }
            }

            return groups;
        }
    }
}
