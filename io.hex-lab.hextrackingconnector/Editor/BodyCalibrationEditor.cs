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
            var script = AddProperty("m_Script");
            script.SetEnabled(false);
            AddProperty("skeletonProvider");
            AddProperty("publishCalibratedPose");
            var firstPose = serializedObject.FindProperty("calibrateOnFirstPose");
            var continuous = serializedObject.FindProperty("continuousCalibration");
            var smoothing = serializedObject.FindProperty("smoothContinuousCalibration");
            AddProperty(firstPose.name, "Calibrate on First Valid Pose");
            var initialMode = AddProperty("initialCalibrationMode");
            AddProperty(continuous.name);
            var continuousMode = AddProperty("continuousCalibrationMode");
            var smoothingToggle = AddProperty(smoothing.name);
            var smoothingTime = AddProperty("continuousSmoothingTime", "Response Time (Seconds)");
            AddProperty("groundHeight");
            AddProperty("calibrationOffset");

            void RefreshVisibility()
            {
                initialMode.style.display = firstPose.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                continuousMode.style.display = continuous.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                smoothingToggle.style.display = continuous.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                smoothingTime.style.display = continuous.boolValue && smoothing.boolValue
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RefreshVisibility();
            root.TrackPropertyValue(firstPose, _ => RefreshVisibility());
            root.TrackPropertyValue(continuous, _ => RefreshVisibility());
            root.TrackPropertyValue(smoothing, _ => RefreshVisibility());

            var manualMode = new EnumField("Manual Calibration Mode", ((BodyCalibration)target).InitialCalibrationMode);
            manualMode.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            manualMode.style.marginTop = 8f;
            root.Add(manualMode);
            var calibrate = new Button(() =>
            {
                foreach (var selectedTarget in targets)
                {
                    var calibration = (BodyCalibration)selectedTarget;
                    if (calibration.Calibrate((BodyCalibrationMode)manualMode.value))
                    {
                        EditorUtility.SetDirty(calibration);
                    }
                }
            }) { text = "Calibrate Now" };
            root.Add(calibrate);
            var info = new HelpBox(
                "Enter Play Mode to calibrate the current source pose manually. Initial calibration runs on the first usable pose; continuous calibration updates on each new source pose.",
                HelpBoxMessageType.Info);
            root.Add(info);
            void RefreshPlayMode()
            {
                calibrate.SetEnabled(Application.isPlaying);
                info.style.display = Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
            }
            RefreshPlayMode();
            root.schedule.Execute(RefreshPlayMode).Every(250);
            return root;

            PropertyField AddProperty(string path, string label = null)
            {
                var field = new PropertyField(serializedObject.FindProperty(path), label);
                root.Add(field);
                return field;
            }
        }
    }
}
