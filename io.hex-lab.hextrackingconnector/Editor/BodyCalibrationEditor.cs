using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(BodyCalibration))]
    public class BodyCalibrationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Calibrate Now"))
                {
                    foreach (var selectedTarget in targets)
                    {
                        var calibration = (BodyCalibration)selectedTarget;
                        calibration.Calibrate();
                        EditorUtility.SetDirty(calibration);
                    }
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to calibrate against the current incoming pose.", MessageType.Info);
            }
        }
    }
}
