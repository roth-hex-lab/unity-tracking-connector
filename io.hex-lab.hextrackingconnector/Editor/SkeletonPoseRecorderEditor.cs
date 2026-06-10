using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(SkeletonPoseRecorder))]
    public sealed class SkeletonPoseRecorderEditor : UnityEditor.Editor
    {
        private SerializedProperty source;
        private SerializedProperty sourceMode;
        private SerializedProperty recordingFormat;
        private SerializedProperty recordingFolder;
        private SerializedProperty recordOnStart;
        private SerializedProperty maxDurationSeconds;
        private SerializedProperty maxFrameCount;
        private SerializedProperty maxQueuedFrames;
        private SerializedProperty overflowMode;
        private SerializedProperty stopOnDefinitionChange;

        private void OnEnable()
        {
            source = serializedObject.FindProperty("source");
            sourceMode = serializedObject.FindProperty("sourceMode");
            recordingFormat = serializedObject.FindProperty("recordingFormat");
            recordingFolder = serializedObject.FindProperty("recordingFolder");
            recordOnStart = serializedObject.FindProperty("recordOnStart");
            maxDurationSeconds = serializedObject.FindProperty("maxDurationSeconds");
            maxFrameCount = serializedObject.FindProperty("maxFrameCount");
            maxQueuedFrames = serializedObject.FindProperty("maxQueuedFrames");
            overflowMode = serializedObject.FindProperty("overflowMode");
            stopOnDefinitionChange = serializedObject.FindProperty("stopOnDefinitionChange");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(source);
            EditorGUILayout.PropertyField(sourceMode);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(recordingFormat);
            DrawFolderField();
            EditorGUILayout.PropertyField(recordOnStart);
            EditorGUILayout.PropertyField(maxDurationSeconds, new GUIContent("Max Duration Seconds"));
            EditorGUILayout.PropertyField(maxFrameCount);
            EditorGUILayout.PropertyField(maxQueuedFrames);
            EditorGUILayout.PropertyField(overflowMode);
            EditorGUILayout.PropertyField(stopOnDefinitionChange);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            DrawRuntimeControls();
        }

        private void DrawFolderField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(recordingFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    var folder = EditorUtility.OpenFolderPanel(
                        "Select Pose Recording Folder",
                        Application.persistentDataPath,
                        string.Empty);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        recordingFolder.stringValue = folder;
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Each recording creates a new timestamped file in this folder. Existing recordings are not overwritten.",
                MessageType.Info);
        }

        private void DrawRuntimeControls()
        {
            var recorder = (SkeletonPoseRecorder)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Recording controls are available in Play Mode.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Recording", recorder.IsRecording);
                EditorGUILayout.DoubleField("Duration", recorder.RecordingDuration);
                EditorGUILayout.IntField("Frames Written", recorder.WrittenFrameCount);
                EditorGUILayout.IntField("Queued Frames", recorder.QueuedFrameCount);
                EditorGUILayout.IntField("Dropped Frames", recorder.DroppedFrameCount);
                EditorGUILayout.TextField("Folder", recorder.RecordingFolder);
                EditorGUILayout.TextField("Active Path", recorder.ActiveOutputPath);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(recorder.IsRecording))
                {
                    if (GUILayout.Button("Start Recording"))
                    {
                        recorder.StartRecording();
                    }
                }

                using (new EditorGUI.DisabledScope(!recorder.IsRecording))
                {
                    if (GUILayout.Button("Stop"))
                    {
                        recorder.StopRecording();
                    }
                }
            }
        }
    }
}
