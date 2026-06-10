using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(SkeletonPosePlayback))]
    public sealed class SkeletonPosePlaybackEditor : UnityEditor.Editor
    {
        private SerializedProperty recordingPath;
        private SerializedProperty playOnStart;
        private SerializedProperty loop;
        private SerializedProperty playbackSpeed;
        private SerializedProperty timeSource;
        private SerializedProperty catchUpMode;
        private SerializedProperty maxFramesPerUpdate;
        private SerializedProperty endBehavior;
        private SerializedProperty useUnscaledTime;
        private SerializedProperty fixedFrameRate;

        private void OnEnable()
        {
            recordingPath = serializedObject.FindProperty("recordingPath");
            playOnStart = serializedObject.FindProperty("playOnStart");
            loop = serializedObject.FindProperty("loop");
            playbackSpeed = serializedObject.FindProperty("playbackSpeed");
            timeSource = serializedObject.FindProperty("timeSource");
            catchUpMode = serializedObject.FindProperty("catchUpMode");
            maxFramesPerUpdate = serializedObject.FindProperty("maxFramesPerUpdate");
            endBehavior = serializedObject.FindProperty("endBehavior");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            fixedFrameRate = serializedObject.FindProperty("fixedFrameRate");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            DrawPathField();
            EditorGUILayout.PropertyField(playOnStart);
            EditorGUILayout.PropertyField(loop);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playbackSpeed);
            EditorGUILayout.PropertyField(timeSource);
            EditorGUILayout.PropertyField(catchUpMode);
            EditorGUILayout.PropertyField(maxFramesPerUpdate);
            EditorGUILayout.PropertyField(endBehavior);
            EditorGUILayout.PropertyField(useUnscaledTime);
            EditorGUILayout.PropertyField(fixedFrameRate);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            DrawRuntimeControls();
        }

        private void DrawPathField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(recordingPath);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    var path = EditorUtility.OpenFilePanel(
                        "Select Pose Recording",
                        Application.persistentDataPath,
                        "hexpose,jsonl");
                    if (!string.IsNullOrEmpty(path))
                    {
                        recordingPath.stringValue = path;
                    }
                }
            }
        }

        private void DrawRuntimeControls()
        {
            var playback = (SkeletonPosePlayback)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Playback controls are available in Play Mode.", MessageType.Info);
                return;
            }

            var info = playback.RecordingInfo;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("State", playback.State);
                EditorGUILayout.DoubleField("Playback Time", playback.PlaybackTime);
                if (info != null)
                {
                    EditorGUILayout.TextField("Format", info.Format.ToString());
                    EditorGUILayout.TextField("Definition", info.Definition.Name);
                    EditorGUILayout.IntField("Frames", info.FrameCount);
                    EditorGUILayout.DoubleField("Duration", info.Duration);
                }
            }

            if (info != null && info.Duration > 0.0)
            {
                var newTime = EditorGUILayout.Slider(
                    "Seek",
                    (float)playback.PlaybackTime,
                    0f,
                    (float)info.Duration);
                if (!Mathf.Approximately(newTime, (float)playback.PlaybackTime))
                {
                    playback.Seek(newTime);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load"))
                {
                    playback.Load();
                }

                if (GUILayout.Button(playback.IsPlaying ? "Pause" : "Play"))
                {
                    if (playback.IsPlaying)
                    {
                        playback.Pause();
                    }
                    else
                    {
                        playback.Play();
                    }
                }

                if (GUILayout.Button("Stop"))
                {
                    playback.StopPlayback();
                }

                if (GUILayout.Button("Restart"))
                {
                    playback.Restart();
                }
            }
        }
    }
}
