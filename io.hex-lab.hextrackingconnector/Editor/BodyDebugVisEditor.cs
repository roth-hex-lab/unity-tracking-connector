using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    [CustomEditor(typeof(BodyDebugVis))]
    public class BodyDebugVisEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var debugVis = (BodyDebugVis)target;
            if (debugVis.UsesLocalOneShotCalibration)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    "Found a BodyCalibration component on this GameObject. BodyDebugVis is using it for local one-shot visualization calibration. Student scripts and avatar drivers are only calibrated automatically when they consume a BodyCalibration provider in the skeleton pipeline.",
                    MessageType.Info);
            }
        }
    }
}
