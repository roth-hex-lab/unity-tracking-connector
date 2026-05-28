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
        private SerializedProperty inputSkeleton;
        private SerializedProperty coordinateSource;
        private SerializedProperty mirrorMode;
        private SerializedProperty logConnectionEvents;

        private void OnEnable()
        {
            transportMode = serializedObject.FindProperty("transportMode");
            pipeName = serializedObject.FindProperty("pipeName");
            udpPort = serializedObject.FindProperty("udpPort");
            inputSkeleton = serializedObject.FindProperty("inputSkeleton");
            coordinateSource = serializedObject.FindProperty("coordinateSource");
            mirrorMode = serializedObject.FindProperty("mirrorMode");
            logConnectionEvents = serializedObject.FindProperty("logConnectionEvents");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTransportSection();
            EditorGUILayout.Space(8f);
            DrawPoseSection();
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
            EditorGUILayout.PropertyField(
                inputSkeleton,
                new GUIContent("Input Skeleton", "Expected landmark layout in the incoming payload. Auto uses skeleton_id when present."));
            EditorGUILayout.PropertyField(coordinateSource, new GUIContent("Coordinate Source"));
            EditorGUILayout.PropertyField(mirrorMode, new GUIContent("Mirror Mode"));
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
        }
    }
}
