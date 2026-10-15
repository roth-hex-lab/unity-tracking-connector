using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(BodyCalibration))]
    public class BodyCalibrationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.Add(new IMGUIContainer(DrawCalibrationControls));
            return root;
        }

        private void DrawCalibrationControls()
        {
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
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to calibrate against the current source-provider pose. Consumers are calibrated automatically when they subscribe to this BodyCalibration component as their ISkeletonProvider.",
                    MessageType.Info);
            }
        }
    }
}
