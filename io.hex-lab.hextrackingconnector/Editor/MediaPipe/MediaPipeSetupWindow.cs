using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal sealed class MediaPipeSetupWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool advancedModelSelectionFoldout;

        [MenuItem("Tools/HEX Tracking Connector/Install Or Uninstall Local MediaPipe")]
        public static void ShowWindow()
        {
            var window = GetWindow<MediaPipeSetupWindow>("MediaPipe Setup");
            window.minSize = new Vector2(520f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            MediaPipeInstaller.LocalSetupChanged += Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            MediaPipeInstaller.LocalSetupChanged -= Repaint;
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Local MediaPipe Pose Tracking", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(GetStatusMessage(), GetStatusMessageType());

            DrawInformation();
            DrawAdvancedModelSelection();
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private static string GetStatusMessage()
        {
            if (MediaPipeInstaller.IsPackageOperationRunning())
            {
                return "Unity Package Manager operation is running.";
            }

            if (MediaPipeInstaller.IsUninstallCleanupPending())
            {
                return "Local MediaPipe uninstall is pending because Unity is holding a native plugin file open. Restart Unity to finish cleanup automatically, or click Uninstall again to retry.";
            }

            if (MediaPipeInstaller.IsPostInstallPending())
            {
                return "Installing local MediaPipe files. Unity is importing and compiling the generated provider integration; the validator will open when this finishes.";
            }

            if (MediaPipeInstaller.IsLocalSetupReady())
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return "Local MediaPipe files are installed. Unity is still importing or compiling the generated provider integration.";
                }

                if (!MediaPipeQuickstartFactory.IsProviderAvailable())
                {
                    return "Local MediaPipe files are installed. Waiting for Unity to load the generated LocalLandmarkProvider assembly.";
                }

                return "Local MediaPipe pose tracking is installed and ready.";
            }

            if (MediaPipeInstaller.IsPackageInstalled() && !MediaPipeInstaller.IsEmbeddedPackageInstalled())
            {
                return "MediaPipe is installed through Package Manager. Click Install to migrate it to the stripped embedded package layout, generated provider integration, and retained Resource models.";
            }

            if (MediaPipeInstaller.IsPackageInstalled())
            {
                return "The MediaPipe package is installed, but the local setup needs repair. Click Install to restore the provider integration and required Resource models.";
            }

            return "Local MediaPipe pose tracking is not installed in this Unity project.";
        }

        private static MessageType GetStatusMessageType()
        {
            if (MediaPipeInstaller.IsLocalSetupReady() &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                MediaPipeQuickstartFactory.IsProviderAvailable())
            {
                return MessageType.Info;
            }

            if (MediaPipeInstaller.IsPackageOperationRunning() || MediaPipeInstaller.IsPostInstallPending())
            {
                return MessageType.None;
            }

            return MessageType.Warning;
        }

        private static void DrawInformation()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(MediaPipeInstaller.ProjectUrl, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.LabelField("Release Archive", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(MediaPipeInstaller.ReleaseTarballUrl, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Download", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Archive size", GetArchiveSizeText());
            EditorGUILayout.LabelField("Embedded package", MediaPipeInstaller.EmbeddedPackageDirectory);
            EditorGUILayout.LabelField("Temporary download", MediaPipeInstaller.CachedTarballPath);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "The downloaded archive is extracted into Packages/com.github.homuler.mediapipe as a stripped embedded package, the connector integration is generated inside that embedded package, selected models are kept as Resources, then the archive is deleted.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "For a clone-ready project, commit the embedded MediaPipe package and the updated Packages/packages-lock.json. The temporary archive cache remains local and is not intended for source control.",
                MessageType.Info);
        }

        private void DrawAdvancedModelSelection()
        {
            EditorGUILayout.Space(8f);
            advancedModelSelectionFoldout = EditorGUILayout.Foldout(
                advancedModelSelectionFoldout,
                "Advanced Model Selection",
                toggleOnLabelClick: true);
            if (!advancedModelSelectionFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    "Keeping " +
                    MediaPipeInstaller.GetSelectedModelRetentionCount() +
                    " model(s), about " +
                    MediaPipeInstaller.GetSelectedModelRetentionMebibytes().ToString("0.0") +
                    " MiB.");
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "These choices are applied when installing or rebuilding the embedded package from the Homuler release archive. The three Pose Landmarker task models used by LocalLandmarkProvider are required and always kept. Optional models can be retained for custom Homuler usage. To restore a model that was stripped earlier, uninstall and install again with that model selected.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Defaults", GUILayout.Width(90f)))
            {
                MediaPipeInstaller.ResetModelRetentionToDefaults();
            }

            if (GUILayout.Button("Select All", GUILayout.Width(90f)))
            {
                foreach (var option in MediaPipeInstaller.ModelRetentionOptions)
                {
                    MediaPipeInstaller.SetModelSelectedForRetention(option.FileName, true);
                }
            }

            if (GUILayout.Button("Clear All", GUILayout.Width(90f)))
            {
                foreach (var option in MediaPipeInstaller.ModelRetentionOptions)
                {
                    MediaPipeInstaller.SetModelSelectedForRetention(option.FileName, false);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var option in MediaPipeInstaller.ModelRetentionOptions)
            {
                var selected = MediaPipeInstaller.IsModelSelectedForRetention(option.FileName);
                var label = option.Label +
                    (option.DefaultSelected ? " (required)" : string.Empty) +
                    " (" +
                    option.FileName +
                    ", " +
                    option.ApproximateMebibytes.ToString("0.##") +
                    " MiB)";

                using (new EditorGUI.DisabledScope(option.DefaultSelected))
                {
                    var nextSelected = EditorGUILayout.ToggleLeft(label, selected);
                    if (nextSelected != selected)
                    {
                        MediaPipeInstaller.SetModelSelectedForRetention(option.FileName, nextSelected);
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawActions()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            var operationRunning = MediaPipeInstaller.IsPackageOperationRunning() ||
                MediaPipeInstaller.IsPostInstallPending();
            var uninstallCleanupPending = MediaPipeInstaller.IsUninstallCleanupPending();
            var ready = MediaPipeInstaller.IsLocalSetupReady();
            var canInstall = !operationRunning && !ready && !uninstallCleanupPending;
            var canUninstall = !operationRunning && MediaPipeInstaller.HasAnyLocalSetupArtifact();
            var canQuickstart = ready &&
                !operationRunning &&
                !uninstallCleanupPending &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                MediaPipeQuickstartFactory.IsProviderAvailable();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!canInstall))
            {
                if (GUILayout.Button("Install", GUILayout.Height(32f)))
                {
                    MediaPipeInstaller.InstallOrRepairLocalMediaPipeAsync();
                }
            }

            using (new EditorGUI.DisabledScope(!canUninstall))
            {
                if (GUILayout.Button("Uninstall", GUILayout.Height(32f)) &&
                    EditorUtility.DisplayDialog(
                        "Uninstall Local MediaPipe Pose Tracking",
                        "This removes the local MediaPipe package and generated provider integration from this Unity project, deletes legacy copied pose models from Assets/StreamingAssets/MediaPipe if present, and deletes the cached release archive from Packages/.hextrackingconnector.\n\nIf Unity is still holding a native MediaPipe plugin file open, cleanup will finish after restarting the editor.",
                        "Uninstall",
                        "Cancel"))
                {
                    MediaPipeInstaller.UninstallLocalMediaPipe();
                }
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!canQuickstart))
            {
                if (GUILayout.Button("Add Quickstart Provider To Scene", GUILayout.Height(28f)))
                {
                    MediaPipeQuickstartFactory.CreateQuickstartProvider();
                }
            }

            if (GUILayout.Button("Open Validator", GUILayout.Height(24f)))
            {
                MediaPipeValidationWindow.ShowWindow();
            }
        }

        private static string GetArchiveSizeText()
        {
            if (!MediaPipeInstaller.HasCachedArchive())
            {
                return "About 277 MiB from GitHub Releases";
            }

            var mebibytes = MediaPipeInstaller.GetCachedArchiveSizeBytes() / (1024.0 * 1024.0);
            return mebibytes.ToString("0.0") + " MiB cached locally";
        }
    }
}
