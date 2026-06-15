using System;
using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal static class MediaPipeQuickstartFactory
    {
        private const string LocalProviderTypeName =
            "HEXLab.Hextrackingconnector.LocalLandmarkProvider, HEXLab.Hextrackingconnector.MediaPipe";
        private const string LocalProviderFullName =
            "HEXLab.Hextrackingconnector.LocalLandmarkProvider";
        private const string LocalProviderAssemblyName =
            "HEXLab.Hextrackingconnector.MediaPipe";

        internal static bool IsProviderAvailable()
        {
            return TryGetLocalProviderType(out _);
        }

        internal static void CreateQuickstartProvider()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog(
                    "Local MediaPipe Provider",
                    "Unity is still importing or compiling the generated LocalLandmarkProvider. Try again when compilation finishes.",
                    "OK");
                return;
            }

            if (!TryGetLocalProviderType(out var providerType))
            {
                EditorUtility.DisplayDialog(
                    "Local MediaPipe Provider",
                    "LocalLandmarkProvider is not compiled yet. Wait for Unity to finish importing MediaPipe, then try again.",
                    "OK");
                return;
            }

            var root = new GameObject("Local MediaPipe Tracking");
            Undo.RegisterCreatedObjectUndo(root, "Create Local MediaPipe Tracking");

            var providerObject = new GameObject("LocalLandmarkProvider");
            providerObject.transform.SetParent(root.transform, worldPositionStays: false);
            var provider = providerObject.AddComponent(providerType) as MonoBehaviour;
            ConfigureLocalProvider(provider);

            var smoothedObject = new GameObject("Smoothed");
            smoothedObject.transform.SetParent(root.transform, worldPositionStays: false);
            var smoothing = smoothedObject.AddComponent<SkeletonSmoothing>();
            SetObjectReference(smoothing, "sourceProvider", provider);

            var calibratedObject = new GameObject("Calibrated");
            calibratedObject.transform.SetParent(root.transform, worldPositionStays: false);
            var calibration = calibratedObject.AddComponent<BodyCalibration>();
            SetObjectReference(calibration, "skeletonProvider", smoothing);

            Selection.activeGameObject = calibratedObject;
            EditorGUIUtility.PingObject(calibratedObject);
        }

        private static bool TryGetLocalProviderType(out Type providerType)
        {
            providerType = Type.GetType(LocalProviderTypeName);
            if (IsValidProviderType(providerType))
            {
                return true;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != LocalProviderAssemblyName)
                {
                    continue;
                }

                providerType = assembly.GetType(LocalProviderFullName, throwOnError: false);
                if (IsValidProviderType(providerType))
                {
                    return true;
                }
            }

            providerType = null;
            return false;
        }

        private static bool IsValidProviderType(Type providerType)
        {
            return providerType != null &&
                typeof(MonoBehaviour).IsAssignableFrom(providerType);
        }

        private static void ConfigureLocalProvider(MonoBehaviour provider)
        {
            if (provider == null)
            {
                return;
            }

            SetBool(provider, "invertWorldX", true);
            SetBool(provider, "invertWorldY", true);
            SetBool(provider, "invertWorldZ", true);
            SetInt(provider, "mirrorMode", 0);
            SetString(provider, "cameraName", string.Empty);
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
