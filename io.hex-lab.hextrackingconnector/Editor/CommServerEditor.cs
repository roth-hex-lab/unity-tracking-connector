using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(CommServer))]
    public class CommServerEditor : UnityEditor.Editor
    {
        private SerializedProperty transportMode;
        private SerializedProperty pipeName;
        private SerializedProperty udpPort;
        private SerializedProperty coordinateSource;
        private SerializedProperty mirrorMode;
        private SerializedProperty smoothingMode;
        private SerializedProperty movingAverageWindowSize;
        private SerializedProperty logConnectionEvents;

        private void OnEnable()
        {
            transportMode = serializedObject.FindProperty("transportMode");
            pipeName = serializedObject.FindProperty("pipeName");
            udpPort = serializedObject.FindProperty("udpPort");
            coordinateSource = serializedObject.FindProperty("coordinateSource");
            mirrorMode = serializedObject.FindProperty("mirrorMode");
            smoothingMode = serializedObject.FindProperty("smoothingMode");
            movingAverageWindowSize = serializedObject.FindProperty("movingAverageWindowSize");
            logConnectionEvents = serializedObject.FindProperty("logConnectionEvents");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTransportSection();
            EditorGUILayout.Space(8f);
            DrawPoseSection();
            EditorGUILayout.Space(8f);
            DrawSmoothingSection();
            EditorGUILayout.Space(8f);
            DrawRuntimeSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTransportSection()
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(transportMode, new GUIContent("Transport"));

            var selectedTransport = (TransportMode)transportMode.enumValueIndex;
            if (selectedTransport == TransportMode.Pipe)
            {
                EditorGUILayout.PropertyField(pipeName, new GUIContent("Pipe Name"));
            }
            else
            {
                EditorGUILayout.PropertyField(udpPort, new GUIContent("UDP Port"));
            }

            EditorGUILayout.PropertyField(logConnectionEvents, new GUIContent("Log Connection Events"));
        }

        private void DrawPoseSection()
        {
            EditorGUILayout.LabelField("Pose Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(coordinateSource, new GUIContent("Coordinate Source"));
            EditorGUILayout.PropertyField(mirrorMode, new GUIContent("Mirror Mode"));
        }

        private void DrawSmoothingSection()
        {
            EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(smoothingMode, new GUIContent("Algorithm"));

            var selectedSmoothing = (PoseSmoothingMode)smoothingMode.enumValueIndex;
            switch (selectedSmoothing)
            {
                case PoseSmoothingMode.MovingAverage:
                    EditorGUILayout.PropertyField(
                        movingAverageWindowSize,
                        new GUIContent("Window Size", "Number of incoming pose frames averaged together."));
                    EditorGUILayout.HelpBox(
                        "Moving average reduces jitter but adds latency as the window grows.",
                        MessageType.Info);
                    break;
                case PoseSmoothingMode.None:
                default:
                    EditorGUILayout.HelpBox(
                        "No smoothing. The latest received pose is published once per Unity frame.",
                        MessageType.Info);
                    break;
            }
        }

        private void DrawRuntimeSection()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var server = (CommServer)target;
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Running", server.IsRunning);
                EditorGUILayout.IntField("Pending Frames", server.PendingFrameCount);
                EditorGUILayout.Toggle("Has Pose", server.TryGetLatestPose(out _));
            }

            if (GUILayout.Button("Reset Smoother"))
            {
                server.ResetSmoother();
            }
        }
    }
}
