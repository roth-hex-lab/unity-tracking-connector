using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Source") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new PropertyField(source));
            root.Add(new PropertyField(sourceMode));

            root.Add(new Label("Recording")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f },
            });
            root.Add(new PropertyField(recordingFormat));
            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            var folderField = new PropertyField(recordingFolder);
            folderField.style.flexGrow = 1f;
            folderRow.Add(folderField);
            folderRow.Add(new Button(BrowseForFolder) { text = "Browse" });
            root.Add(folderRow);
            root.Add(new HelpBox(
                "Each recording creates a new timestamped file in this folder. Existing recordings are not overwritten.",
                HelpBoxMessageType.Info));
            root.Add(new PropertyField(recordOnStart));
            root.Add(new PropertyField(maxDurationSeconds, "Max Duration Seconds"));
            root.Add(new PropertyField(maxFrameCount));
            root.Add(new PropertyField(maxQueuedFrames));
            root.Add(new PropertyField(overflowMode));
            root.Add(new PropertyField(stopOnDefinitionChange));
            root.Add(new IMGUIContainer(() =>
            {
                EditorGUILayout.Space(8f);
                DrawRuntimeControls();
            }));
            return root;
        }

        private void BrowseForFolder()
        {
            var folder = EditorUtility.OpenFolderPanel(
                "Select Pose Recording Folder",
                Application.persistentDataPath,
                string.Empty);
            if (!string.IsNullOrEmpty(folder))
            {
                serializedObject.Update();
                recordingFolder.stringValue = folder;
                serializedObject.ApplyModifiedProperties();
            }
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
                EditorGUILayout.Toggle("Finishing", recorder.IsStopping);
                EditorGUILayout.DoubleField("Duration", recorder.RecordingDuration);
                EditorGUILayout.IntField("Frames Written", recorder.WrittenFrameCount);
                EditorGUILayout.IntField("Queued Frames", recorder.QueuedFrameCount);
                EditorGUILayout.IntField("Dropped Frames", recorder.DroppedFrameCount);
                EditorGUILayout.TextField("Folder", recorder.RecordingFolder);
                EditorGUILayout.TextField("Active Path", recorder.ActiveOutputPath);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(recorder.IsRecording || recorder.IsStopping))
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
