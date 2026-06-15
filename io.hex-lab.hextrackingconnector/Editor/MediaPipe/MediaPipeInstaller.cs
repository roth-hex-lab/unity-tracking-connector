using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal sealed class MediaPipeModelRetentionOption
    {
        public MediaPipeModelRetentionOption(
            string fileName,
            string label,
            double approximateMebibytes,
            bool defaultSelected)
        {
            FileName = fileName;
            Label = label;
            ApproximateMebibytes = approximateMebibytes;
            DefaultSelected = defaultSelected;
        }

        public string FileName { get; }
        public string Label { get; }
        public double ApproximateMebibytes { get; }
        public bool DefaultSelected { get; }
    }

    internal static class MediaPipeInstaller
    {
        internal const string PackageName = "com.github.homuler.mediapipe";
        internal const string PackageVersion = "0.16.3";
        internal const string ProjectUrl = "https://github.com/homuler/MediaPipeUnityPlugin";
        internal const string TarballFileName = PackageName + "-" + PackageVersion + ".tgz";
        internal const string ReleaseTarballUrl =
            ProjectUrl +
            "/releases/download/v" +
            PackageVersion +
            "/" +
            TarballFileName;

        private const string CacheFolderName = ".hextrackingconnector";
        private const string MediaPipeCacheFolderName = "mediapipe";
        private const string IntegrationFolderName = "HEXTrackingConnector";
        private const string MediaPipeAssemblyTemplateName =
            "HEXLab.Hextrackingconnector.MediaPipe.asmdef.template";
        private const string MediaPipeAssemblyFileName = "HEXLab.Hextrackingconnector.MediaPipe.asmdef";
        private const string LocalLandmarkProviderTemplateName = "LocalLandmarkProvider.cs.template";
        private const string LocalLandmarkProviderFileName = "LocalLandmarkProvider.cs";
        private const int TarBlockSize = 512;
        private const string PendingPostInstallSessionKey =
            "HEXLab.Hextrackingconnector.MediaPipe.PendingPostInstall";
        private const string PostInstallPollCountSessionKey =
            "HEXLab.Hextrackingconnector.MediaPipe.PostInstallPollCount";
        private const string PendingInstallAfterLegacyRemoveSessionKey =
            "HEXLab.Hextrackingconnector.MediaPipe.PendingInstallAfterLegacyRemove";
        private const string PendingUninstallMarkerFileName = "uninstall-pending.txt";
        private const string ModelRetentionEditorPrefsPrefix =
            "HEXLab.Hextrackingconnector.MediaPipe.ModelRetention.";
        private const string PackageResourceModelRelativeDirectory =
            "PackageResources/Resources/MediaPipe";
        private const string LegacyPackageModelRelativeDirectory =
            "PackageResources/MediaPipe";
        private const int MaxPostInstallPollCount = 600;

        private static readonly MediaPipeModelRetentionOption[] modelRetentionOptions =
        {
            new MediaPipeModelRetentionOption("pose_landmarker_lite.bytes", "Pose Landmarker Lite", 5.51, true),
            new MediaPipeModelRetentionOption("pose_landmarker_full.bytes", "Pose Landmarker Full", 8.96, true),
            new MediaPipeModelRetentionOption("pose_landmarker_heavy.bytes", "Pose Landmarker Heavy", 29.24, true),
            new MediaPipeModelRetentionOption("blaze_face_short_range.bytes", "Blaze Face Short Range", 0.22, false),
            new MediaPipeModelRetentionOption("deeplab_v3.bytes", "DeepLab V3", 2.65, false),
            new MediaPipeModelRetentionOption("efficientdet_lite0_float16.bytes", "EfficientDet Lite0 Float16", 6.92, false),
            new MediaPipeModelRetentionOption("efficientdet_lite0_float32.bytes", "EfficientDet Lite0 Float32", 13.2, false),
            new MediaPipeModelRetentionOption("efficientdet_lite0_int8.bytes", "EfficientDet Lite0 Int8", 4.39, false),
            new MediaPipeModelRetentionOption("efficientdet_lite2_float16.bytes", "EfficientDet Lite2 Float16", 11.58, false),
            new MediaPipeModelRetentionOption("efficientdet_lite2_float32.bytes", "EfficientDet Lite2 Float32", 22.03, false),
            new MediaPipeModelRetentionOption("efficientdet_lite2_int8.bytes", "EfficientDet Lite2 Int8", 7.17, false),
            new MediaPipeModelRetentionOption("face_detection_full_range.bytes", "Face Detection Full Range", 1.03, false),
            new MediaPipeModelRetentionOption("face_detection_full_range_sparse.bytes", "Face Detection Full Range Sparse", 0.65, false),
            new MediaPipeModelRetentionOption("face_detection_short_range.bytes", "Face Detection Short Range", 0.22, false),
            new MediaPipeModelRetentionOption("face_landmark.bytes", "Face Landmark", 1.19, false),
            new MediaPipeModelRetentionOption("face_landmark_with_attention.bytes", "Face Landmark With Attention", 2.38, false),
            new MediaPipeModelRetentionOption("face_landmarker_v2.bytes", "Face Landmarker V2", 1.34, false),
            new MediaPipeModelRetentionOption("face_landmarker_v2_with_blendshapes.bytes", "Face Landmarker V2 With Blendshapes", 2.25, false),
            new MediaPipeModelRetentionOption("gesture_recognizer.bytes", "Gesture Recognizer", 7.99, false),
            new MediaPipeModelRetentionOption("hair_segmentation.bytes", "Hair Segmentation", 0.75, false),
            new MediaPipeModelRetentionOption("hand_landmark_full.bytes", "Hand Landmark Full", 5.23, false),
            new MediaPipeModelRetentionOption("hand_landmark_lite.bytes", "Hand Landmark Lite", 1.98, false),
            new MediaPipeModelRetentionOption("hand_landmarker.bytes", "Hand Landmarker", 7.46, false),
            new MediaPipeModelRetentionOption("hand_recrop.bytes", "Hand Recrop", 0.12, false),
            new MediaPipeModelRetentionOption("holistic_landmarker.bytes", "Holistic Landmarker", 13.05, false),
            new MediaPipeModelRetentionOption("iris_landmark.bytes", "Iris Landmark", 2.52, false),
            new MediaPipeModelRetentionOption("palm_detection_full.bytes", "Palm Detection Full", 2.23, false),
            new MediaPipeModelRetentionOption("palm_detection_lite.bytes", "Palm Detection Lite", 1.89, false),
            new MediaPipeModelRetentionOption("pose_detection.bytes", "Legacy Pose Detection", 2.82, false),
            new MediaPipeModelRetentionOption("pose_landmark_lite.bytes", "Legacy Pose Landmark Lite", 2.69, false),
            new MediaPipeModelRetentionOption("pose_landmark_full.bytes", "Legacy Pose Landmark Full", 6.14, false),
            new MediaPipeModelRetentionOption("pose_landmark_heavy.bytes", "Legacy Pose Landmark Heavy", 26.43, false),
            new MediaPipeModelRetentionOption("selfie_multiclass_256x256.bytes", "Selfie Multiclass", 15.61, false),
            new MediaPipeModelRetentionOption("selfie_segmentation.bytes", "Selfie Segmentation", 0.24, false),
            new MediaPipeModelRetentionOption("selfie_segmentation_landscape.bytes", "Selfie Segmentation Landscape", 0.24, false),
            new MediaPipeModelRetentionOption("ssd_mobilenet_v2_float16.bytes", "SSD MobileNet V2 Float16", 5.65, false),
            new MediaPipeModelRetentionOption("ssd_mobilenet_v2_float32.bytes", "SSD MobileNet V2 Float32", 10.79, false),
            new MediaPipeModelRetentionOption("ssdlite_object_detection.bytes", "SSDLite Object Detection", 5.97, false),
            new MediaPipeModelRetentionOption("yamnet_audio_classifier_with_metadata.bytes", "YAMNet Audio Classifier", 3.94, false),
        };

        private static RemoveRequest removeRequest;
        internal static event Action LocalSetupChanged;

        internal static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        internal static string PackagesRoot => Path.Combine(ProjectRoot, "Packages");
        internal static string CacheDirectory =>
            Path.Combine(PackagesRoot, CacheFolderName, MediaPipeCacheFolderName);
        internal static string CachedTarballPath => Path.Combine(CacheDirectory, TarballFileName);
        internal static string EmbeddedPackageDirectory => Path.Combine(PackagesRoot, PackageName);
        internal static string IntegrationDestinationDirectory =>
            Path.Combine(EmbeddedPackageDirectory, IntegrationFolderName);
        internal static string PendingUninstallMarkerPath =>
            Path.Combine(CacheDirectory, PendingUninstallMarkerFileName);
        internal static string UpmFileIdentifier =>
            "file:" + ToForwardSlashes(Path.Combine(CacheFolderName, MediaPipeCacheFolderName, TarballFileName));
        internal static IReadOnlyList<MediaPipeModelRetentionOption> ModelRetentionOptions =>
            modelRetentionOptions;
        internal static string EmbeddedPackageResourceModelDirectory =>
            GetPackageResourceModelDirectory(EmbeddedPackageDirectory);
        internal static string EmbeddedPackageLegacyModelDirectory =>
            GetLegacyPackageModelDirectory(EmbeddedPackageDirectory);

        [InitializeOnLoadMethod]
        private static void CompletePendingPostInstallAfterReload()
        {
            EditorApplication.delayCall += BeginPendingPostInstallPollingIfNeeded;
            EditorApplication.delayCall += ContinuePendingInstallAfterLegacyRemoveIfNeeded;
            EditorApplication.delayCall += RetryPendingUninstallCleanup;
        }

        internal static bool IsPackageInstalled()
        {
            return UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName) != null;
        }

        internal static bool TryGetInstalledPackageRoot(out string packageRoot)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            packageRoot = packageInfo == null ? string.Empty : packageInfo.resolvedPath;
            return !string.IsNullOrEmpty(packageRoot) && Directory.Exists(packageRoot);
        }

        internal static bool IsEmbeddedPackageInstalled()
        {
            if (!TryGetInstalledPackageRoot(out var packageRoot))
            {
                return false;
            }

            return PathsEqual(packageRoot, EmbeddedPackageDirectory);
        }

        internal static bool TryGetLocalPackageRoot(out string packageRoot)
        {
            if (TryGetInstalledPackageRoot(out packageRoot))
            {
                return true;
            }

            if (Directory.Exists(EmbeddedPackageDirectory))
            {
                packageRoot = EmbeddedPackageDirectory;
                return true;
            }

            packageRoot = string.Empty;
            return false;
        }

        internal static bool IsPackageOperationRunning()
        {
            return removeRequest != null;
        }

        internal static bool IsPostInstallPending()
        {
            return SessionState.GetBool(PendingPostInstallSessionKey, false);
        }

        internal static bool HasCachedArchive()
        {
            return File.Exists(CachedTarballPath);
        }

        internal static long GetCachedArchiveSizeBytes()
        {
            return File.Exists(CachedTarballPath) ? new FileInfo(CachedTarballPath).Length : 0L;
        }

        internal static bool IsLocalSetupReady()
        {
            return Directory.Exists(EmbeddedPackageDirectory) &&
                AreIntegrationScriptsInstalled() &&
                MediaPipeSetupValidator.AreRequiredPoseModelsAvailable();
        }

        internal static bool HasAnyLocalSetupArtifact()
        {
            return IsPackageInstalled() ||
                HasCachedArchive() ||
                IsUninstallCleanupPending() ||
                Directory.Exists(EmbeddedPackageDirectory) ||
                MediaPipeSetupValidator.HasAnyPoseModelCopy();
        }

        internal static bool IsUninstallCleanupPending()
        {
            return File.Exists(PendingUninstallMarkerPath);
        }

        internal static string GetPackageResourceModelDirectory(string packageRoot)
        {
            return Path.Combine(
                packageRoot,
                PackageResourceModelRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static string GetLegacyPackageModelDirectory(string packageRoot)
        {
            return Path.Combine(
                packageRoot,
                LegacyPackageModelRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static bool IsModelSelectedForRetention(string fileName)
        {
            var option = FindModelRetentionOption(fileName);
            if (option != null && option.DefaultSelected)
            {
                return true;
            }

            return option != null &&
                EditorPrefs.GetBool(GetModelRetentionEditorPrefsKey(fileName), option.DefaultSelected);
        }

        internal static void SetModelSelectedForRetention(string fileName, bool selected)
        {
            var option = FindModelRetentionOption(fileName);
            if (option == null)
            {
                return;
            }

            if (option.DefaultSelected && !selected)
            {
                EditorPrefs.DeleteKey(GetModelRetentionEditorPrefsKey(fileName));
                return;
            }

            EditorPrefs.SetBool(GetModelRetentionEditorPrefsKey(fileName), selected);
        }

        internal static void ResetModelRetentionToDefaults()
        {
            foreach (var option in modelRetentionOptions)
            {
                EditorPrefs.DeleteKey(GetModelRetentionEditorPrefsKey(option.FileName));
            }
        }

        internal static int GetSelectedModelRetentionCount()
        {
            var count = 0;
            foreach (var option in modelRetentionOptions)
            {
                if (IsModelSelectedForRetention(option.FileName))
                {
                    count++;
                }
            }

            return count;
        }

        internal static double GetSelectedModelRetentionMebibytes()
        {
            var mebibytes = 0.0;
            foreach (var option in modelRetentionOptions)
            {
                if (IsModelSelectedForRetention(option.FileName))
                {
                    mebibytes += option.ApproximateMebibytes;
                }
            }

            return mebibytes;
        }

        internal static async void InstallOrRepairLocalMediaPipeAsync()
        {
            try
            {
                if (IsPackageInstalled() && !IsEmbeddedPackageInstalled())
                {
                    RemoveLegacyPackageThenInstall();
                    return;
                }

                if (!Directory.Exists(EmbeddedPackageDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                    if (!File.Exists(CachedTarballPath))
                    {
                        await DownloadTarballAsync(CachedTarballPath);
                    }

                    ExtractTarballToEmbeddedPackage(CachedTarballPath);
                    StartPostInstallImport();
                    return;
                }

                StartPostInstallImport();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Local MediaPipe Install Failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async Task DownloadTarballAsync(string destinationPath)
        {
            using (var request = UnityWebRequest.Get(ReleaseTarballUrl))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath)
                {
                    removeFileOnAbort = true,
                };

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    EditorUtility.DisplayProgressBar(
                        "Downloading Local MediaPipe",
                        ReleaseTarballUrl,
                        Mathf.Clamp01(operation.progress));
                    await Task.Delay(100);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(
                        "Failed to download the MediaPipe release archive: " + request.error);
                }
            }
        }

        private static void RemoveLegacyPackageThenInstall()
        {
            SessionState.SetBool(PendingInstallAfterLegacyRemoveSessionKey, true);
            removeRequest = Client.Remove(PackageName);
            EditorApplication.update += MonitorPackageRemove;
        }

        private static void BeginPendingPostInstallPollingIfNeeded()
        {
            if (!SessionState.GetBool(PendingPostInstallSessionKey, false))
            {
                return;
            }

            EditorApplication.update -= PollPendingPostInstall;
            EditorApplication.update += PollPendingPostInstall;
        }

        private static void PollPendingPostInstall()
        {
            if (!SessionState.GetBool(PendingPostInstallSessionKey, false))
            {
                EditorApplication.update -= PollPendingPostInstall;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            RepairLocalSetupFiles();
            if (IsPostInstallReady())
            {
                EditorApplication.update -= PollPendingPostInstall;
                FinishPostInstall();
                return;
            }

            var pollCount = SessionState.GetInt(PostInstallPollCountSessionKey, 0) + 1;
            SessionState.SetInt(PostInstallPollCountSessionKey, pollCount);
            if (pollCount <= MaxPostInstallPollCount)
            {
                return;
            }

            EditorApplication.update -= PollPendingPostInstall;
            ClearPostInstallPending();
            var message =
                "Local MediaPipe files were extracted, but setup did not finish after Unity became idle. " +
                "Missing local artifacts: " + GetMissingLocalSetupArtifactSummary() + ". " +
                "Reopen Tools > HEX Tracking Connector > Install Or Uninstall Local MediaPipe and click Install to repair the local setup.";
            Debug.LogWarning(message);
            EditorUtility.DisplayDialog("Local MediaPipe Setup Pending", message, "OK");
        }

        private static void StartPostInstallImport()
        {
            RepairLocalSetupFiles();
            DeleteCachedArchive();
            SetPostInstallPending();
            RequestCleanScriptCompilation();
            AssetDatabase.Refresh();
            BeginPendingPostInstallPollingIfNeeded();
        }

        private static void FinishPostInstall()
        {
            ClearPostInstallPending();
            NotifyLocalSetupChanged();
            MediaPipeValidationWindow.ShowWindow();
        }

        internal static void UninstallLocalMediaPipe()
        {
            ClearPostInstallPending();
            MediaPipeSetupValidator.DeletePoseModelsFromStreamingAssets();
            DeleteIntegrationScripts();

            if (IsEmbeddedPackageInstalled() || Directory.Exists(EmbeddedPackageDirectory))
            {
                CompleteEmbeddedPackageUninstall();
                return;
            }

            if (IsPackageInstalled())
            {
                removeRequest = Client.Remove(PackageName);
                EditorApplication.update += MonitorPackageRemove;
                return;
            }

            DeleteCachedArchive();
            ClearPendingUninstallCleanup();
            RequestCleanScriptCompilation();
            AssetDatabase.Refresh();
            NotifyLocalSetupChanged();
            EditorUtility.DisplayDialog(
                "Local MediaPipe Removed",
                "Local MediaPipe models and cached archive were removed. The MediaPipe package was not installed.",
                "OK");
        }

        private static void MonitorPackageRemove()
        {
            if (removeRequest == null || !removeRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= MonitorPackageRemove;
            var completedRequest = removeRequest;
            removeRequest = null;

            if (completedRequest.Status == StatusCode.Success)
            {
                if (SessionState.GetBool(PendingInstallAfterLegacyRemoveSessionKey, false))
                {
                    SessionState.EraseBool(PendingInstallAfterLegacyRemoveSessionKey);
                    DeleteCachedArchive();
                    InstallOrRepairLocalMediaPipeAsync();
                    return;
                }

                DeleteCachedArchive();
                ClearPendingUninstallCleanup();
                RequestCleanScriptCompilation();
                AssetDatabase.Refresh();
                NotifyLocalSetupChanged();
                EditorUtility.DisplayDialog(
                    "Local MediaPipe Removed",
                    "The local MediaPipe package, legacy copied pose models, and cached release archive were removed from this Unity project.",
                    "OK");
                return;
            }

            var message = completedRequest.Error == null
                ? "Unity Package Manager failed to remove the local MediaPipe package."
                : completedRequest.Error.message;
            Debug.LogError(message);
            EditorUtility.DisplayDialog("Local MediaPipe Uninstall Failed", message, "OK");
        }

        private static void CompleteEmbeddedPackageUninstall()
        {
            if (!TryDeleteEmbeddedPackage(out var deleteError))
            {
                MarkPendingUninstallCleanup(deleteError);
                RequestCleanScriptCompilation();
                AssetDatabase.Refresh();
                NotifyLocalSetupChanged();

                var message =
                    "Local MediaPipe was disabled, and legacy copied pose models were removed. Unity is still holding one of MediaPipe's native plugin files open, so the embedded package folder cannot be deleted until the editor releases it.\n\n" +
                    "Close and reopen this Unity project to finish cleanup automatically. If the folder remains after restart, run Uninstall again.\n\n" +
                    "Locked file detail: " + deleteError;
                Debug.LogWarning(message);
                EditorUtility.DisplayDialog("Restart Unity To Finish MediaPipe Uninstall", message, "OK");
                return;
            }

            ClearPendingUninstallCleanup();
            DeleteCachedArchive();
            RequestCleanScriptCompilation();
            AssetDatabase.Refresh();
            NotifyLocalSetupChanged();
            EditorUtility.DisplayDialog(
                "Local MediaPipe Removed",
                "The embedded MediaPipe package, generated provider integration, legacy copied pose models, and temporary release archive were removed from this Unity project.",
                "OK");
        }

        private static void RetryPendingUninstallCleanup()
        {
            if (!IsUninstallCleanupPending())
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RetryPendingUninstallCleanup;
                return;
            }

            MediaPipeSetupValidator.DeletePoseModelsFromStreamingAssets();
            DeleteIntegrationScripts();
            if (!TryDeleteEmbeddedPackage(out var deleteError))
            {
                Debug.LogWarning(
                    "Local MediaPipe uninstall cleanup is still pending because Unity could not delete the embedded package folder: " +
                    deleteError);
                NotifyLocalSetupChanged();
                return;
            }

            ClearPendingUninstallCleanup();
            DeleteCachedArchive();
            RequestCleanScriptCompilation();
            AssetDatabase.Refresh();
            NotifyLocalSetupChanged();
            Debug.Log("Completed pending Local MediaPipe uninstall cleanup.");
        }

        private static void ContinuePendingInstallAfterLegacyRemoveIfNeeded()
        {
            if (!SessionState.GetBool(PendingInstallAfterLegacyRemoveSessionKey, false) ||
                IsPackageInstalled())
            {
                return;
            }

            SessionState.EraseBool(PendingInstallAfterLegacyRemoveSessionKey);
            InstallOrRepairLocalMediaPipeAsync();
        }

        internal static bool AreIntegrationScriptsInstalled()
        {
            return File.Exists(Path.Combine(IntegrationDestinationDirectory, MediaPipeAssemblyFileName)) &&
                File.Exists(Path.Combine(IntegrationDestinationDirectory, LocalLandmarkProviderFileName));
        }

        private static bool AreLocalSetupFilesReady()
        {
            return Directory.Exists(EmbeddedPackageDirectory) &&
                File.Exists(Path.Combine(EmbeddedPackageDirectory, "package.json")) &&
                AreIntegrationScriptsInstalled() &&
                MediaPipeSetupValidator.AreRequiredPoseModelsAvailable();
        }

        private static void RepairLocalSetupFiles()
        {
            if (!Directory.Exists(EmbeddedPackageDirectory))
            {
                return;
            }

            StripEmbeddedPackage(EmbeddedPackageDirectory);
            CopyIntegrationScriptsToEmbeddedPackage();
            MediaPipeSetupValidator.DeletePoseModelsFromStreamingAssets();
        }

        private static string GetMissingLocalSetupArtifactSummary()
        {
            if (!Directory.Exists(EmbeddedPackageDirectory))
            {
                return "embedded package folder";
            }

            var missing = new System.Collections.Generic.List<string>();
            if (!File.Exists(Path.Combine(EmbeddedPackageDirectory, "package.json")))
            {
                missing.Add("package manifest");
            }

            if (!AreIntegrationScriptsInstalled())
            {
                missing.Add("generated provider integration");
            }

            if (!MediaPipeSetupValidator.AreRequiredPoseModelsAvailable())
            {
                missing.Add("package pose landmarker model resources");
            }

            if (!MediaPipeQuickstartFactory.IsProviderAvailable())
            {
                missing.Add("compiled LocalLandmarkProvider assembly");
            }

            return missing.Count == 0 ? "none" : string.Join(", ", missing);
        }

        private static bool IsPostInstallReady()
        {
            return AreLocalSetupFilesReady() &&
                MediaPipeQuickstartFactory.IsProviderAvailable();
        }

        private static void CopyIntegrationScriptsToEmbeddedPackage()
        {
            if (!Directory.Exists(EmbeddedPackageDirectory))
            {
                throw new DirectoryNotFoundException(
                    "Cannot copy the HEX Tracking Connector MediaPipe integration before the embedded package exists: " +
                    EmbeddedPackageDirectory);
            }

            Directory.CreateDirectory(IntegrationDestinationDirectory);
            CopyIntegrationTemplate(MediaPipeAssemblyTemplateName, MediaPipeAssemblyFileName);
            CopyIntegrationTemplate(LocalLandmarkProviderTemplateName, LocalLandmarkProviderFileName);
        }

        private static void CopyIntegrationTemplate(string templateName, string outputName)
        {
            var templatePath = Path.Combine(GetIntegrationTemplateDirectory(), templateName);
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    "Missing HEX Tracking Connector MediaPipe integration template.",
                    templatePath);
            }

            File.Copy(
                templatePath,
                Path.Combine(IntegrationDestinationDirectory, outputName),
                overwrite: true);
        }

        private static MediaPipeModelRetentionOption FindModelRetentionOption(string fileName)
        {
            foreach (var option in modelRetentionOptions)
            {
                if (string.Equals(option.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return null;
        }

        private static string GetModelRetentionEditorPrefsKey(string fileName)
        {
            return ModelRetentionEditorPrefsPrefix + PackageVersion + "." + fileName;
        }

        private static HashSet<string> GetSelectedModelFileSet()
        {
            var selectedModelFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in modelRetentionOptions)
            {
                if (IsModelSelectedForRetention(option.FileName))
                {
                    selectedModelFiles.Add(option.FileName);
                }
            }

            return selectedModelFiles;
        }

        private static void StripEmbeddedPackage(string packageRoot)
        {
            DeleteDirectory(Path.Combine(packageRoot, "Samples~"));
            TryDeleteFile(Path.Combine(packageRoot, "Samples~.meta"));
            DeleteDirectory(Path.Combine(packageRoot, "Tests"));
            TryDeleteFile(Path.Combine(packageRoot, "Tests.meta"));
            RewritePackageManifest(packageRoot);
            PruneMediaPipeModels(packageRoot);
        }

        private static void RewritePackageManifest(string packageRoot)
        {
            var manifestPath = Path.Combine(packageRoot, "package.json");
            var manifest =
                "{\n" +
                "  \"name\": \"" + PackageName + "\",\n" +
                "  \"version\": \"" + PackageVersion + "\",\n" +
                "  \"displayName\": \"MediaPipe Unity Plugin\",\n" +
                "  \"description\": \"Stripped MediaPipe Unity Plugin package generated by HEX Tracking Connector for local pose tracking.\",\n" +
                "  \"unity\": \"2022.3\",\n" +
                "  \"author\": {\n" +
                "    \"name\": \"homuler\"\n" +
                "  },\n" +
                "  \"changelogUrl\": \"https://github.com/homuler/MediaPipeUnityPlugin/blob/master/CHANGELOG.md\",\n" +
                "  \"documentationUrl\": \"https://github.com/homuler/MediaPipeUnityPlugin/wiki\",\n" +
                "  \"keywords\": [\n" +
                "    \"mediapipe\",\n" +
                "    \"MediaPipe\"\n" +
                "  ],\n" +
                "  \"dependencies\": {\n" +
                "    \"com.unity.ugui\": \"1.0.0\"\n" +
                "  },\n" +
                "  \"license\": \"MIT\",\n" +
                "  \"licenseUrl\": \"https://github.com/homuler/MediaPipeUnityPlugin/blob/master/LICENSE\"\n" +
                "}\n";

            File.WriteAllText(manifestPath, manifest);
        }

        private static void PruneMediaPipeModels(string packageRoot)
        {
            var legacyModelDirectory = GetLegacyPackageModelDirectory(packageRoot);
            var resourceModelDirectory = GetPackageResourceModelDirectory(packageRoot);
            Directory.CreateDirectory(resourceModelDirectory);

            var selectedModelFiles = GetSelectedModelFileSet();
            foreach (var option in modelRetentionOptions)
            {
                var legacyPath = Path.Combine(legacyModelDirectory, option.FileName);
                var resourcePath = Path.Combine(resourceModelDirectory, option.FileName);
                if (selectedModelFiles.Contains(option.FileName))
                {
                    MoveModelToResourcesIfPresent(legacyPath, resourcePath);
                    continue;
                }

                TryDeleteFile(legacyPath);
                TryDeleteFile(legacyPath + ".meta");
                TryDeleteFile(resourcePath);
                TryDeleteFile(resourcePath + ".meta");
            }

            DeleteUnknownModelFiles(legacyModelDirectory, selectedModelFiles);
            DeleteUnknownModelFiles(resourceModelDirectory, selectedModelFiles);
        }

        private static void MoveModelToResourcesIfPresent(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            TryDeleteFile(destinationPath);
            File.Move(sourcePath, destinationPath);

            var sourceMetaPath = sourcePath + ".meta";
            if (!File.Exists(sourceMetaPath))
            {
                return;
            }

            var destinationMetaPath = destinationPath + ".meta";
            TryDeleteFile(destinationMetaPath);
            File.Move(sourceMetaPath, destinationMetaPath);
        }

        private static void DeleteUnknownModelFiles(string modelDirectory, ISet<string> selectedModelFiles)
        {
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            foreach (var modelPath in Directory.GetFiles(modelDirectory, "*.bytes"))
            {
                if (selectedModelFiles.Contains(Path.GetFileName(modelPath)))
                {
                    continue;
                }

                TryDeleteFile(modelPath);
                TryDeleteFile(modelPath + ".meta");
            }
        }

        private static string GetIntegrationTemplateDirectory()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(MediaPipeInstaller).Assembly);
            if (packageInfo != null && Directory.Exists(packageInfo.resolvedPath))
            {
                return Path.Combine(packageInfo.resolvedPath, "Runtime", "MediaPipe");
            }

            return Path.Combine(ProjectRoot, "Packages", "io.hex-lab.hextrackingconnector", "Runtime", "MediaPipe");
        }

        private static void ExtractTarballToEmbeddedPackage(string tarballPath)
        {
            var stagingDirectory = EmbeddedPackageDirectory + ".installing";
            DeleteDirectory(stagingDirectory);
            Directory.CreateDirectory(stagingDirectory);

            using (var fileStream = File.OpenRead(tarballPath))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            {
                ExtractPackageTar(gzipStream, stagingDirectory);
            }

            DeleteDirectory(EmbeddedPackageDirectory);
            StripEmbeddedPackage(stagingDirectory);
            Directory.Move(stagingDirectory, EmbeddedPackageDirectory);
        }

        private static void ExtractPackageTar(Stream stream, string destinationDirectory)
        {
            var header = new byte[TarBlockSize];
            while (ReadBlock(stream, header))
            {
                if (IsEmptyBlock(header))
                {
                    break;
                }

                var entryName = ReadTarString(header, 0, 100);
                var prefix = ReadTarString(header, 345, 155);
                if (!string.IsNullOrEmpty(prefix))
                {
                    entryName = prefix + "/" + entryName;
                }

                var entrySize = ReadTarOctal(header, 124, 12);
                var typeFlag = (char)header[156];
                var relativePath = StripPackagePrefix(entryName);
                if (string.IsNullOrEmpty(relativePath))
                {
                    SkipTarEntry(stream, entrySize);
                    continue;
                }

                var outputPath = GetSafeOutputPath(destinationDirectory, relativePath);
                if (typeFlag == '5')
                {
                    Directory.CreateDirectory(outputPath);
                    SkipTarEntry(stream, entrySize);
                    continue;
                }

                if (typeFlag == '0' || typeFlag == '\0')
                {
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var outputStream = File.Create(outputPath))
                    {
                        CopyExactly(stream, outputStream, entrySize);
                    }

                    SkipPadding(stream, entrySize);
                    continue;
                }

                SkipTarEntry(stream, entrySize);
            }
        }

        private static bool ReadBlock(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                {
                    return offset > 0;
                }

                offset += read;
            }

            return true;
        }

        private static bool IsEmptyBlock(byte[] block)
        {
            for (var i = 0; i < block.Length; i++)
            {
                if (block[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadTarString(byte[] buffer, int offset, int count)
        {
            var value = Encoding.ASCII.GetString(buffer, offset, count);
            return value.Trim('\0', ' ');
        }

        private static long ReadTarOctal(byte[] buffer, int offset, int count)
        {
            var value = ReadTarString(buffer, offset, count);
            return string.IsNullOrEmpty(value)
                ? 0L
                : Convert.ToInt64(value, 8);
        }

        private static string StripPackagePrefix(string entryName)
        {
            entryName = entryName.Replace('\\', '/').TrimStart('/');
            const string packagePrefix = "package/";
            return entryName.StartsWith(packagePrefix, StringComparison.Ordinal)
                ? entryName.Substring(packagePrefix.Length)
                : string.Empty;
        }

        private static string GetSafeOutputPath(string destinationDirectory, string relativePath)
        {
            var destinationRoot = Path.GetFullPath(destinationDirectory);
            var outputPath = Path.GetFullPath(Path.Combine(
                destinationRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!outputPath.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !PathsEqual(outputPath, destinationRoot))
            {
                throw new InvalidDataException("Archive entry escapes package directory: " + relativePath);
            }

            return outputPath;
        }

        private static void CopyExactly(Stream source, Stream destination, long byteCount)
        {
            var buffer = new byte[81920];
            var remaining = byteCount;
            while (remaining > 0)
            {
                var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of tar archive.");
                }

                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void SkipTarEntry(Stream stream, long entrySize)
        {
            SkipBytes(stream, entrySize);
            SkipPadding(stream, entrySize);
        }

        private static void SkipPadding(Stream stream, long entrySize)
        {
            var padding = (TarBlockSize - (entrySize % TarBlockSize)) % TarBlockSize;
            SkipBytes(stream, padding);
        }

        private static void SkipBytes(Stream stream, long byteCount)
        {
            var buffer = new byte[8192];
            var remaining = byteCount;
            while (remaining > 0)
            {
                var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of tar archive.");
                }

                remaining -= read;
            }
        }

        private static void DeleteEmbeddedPackage()
        {
            DeleteDirectory(EmbeddedPackageDirectory);
            DeleteDirectory(EmbeddedPackageDirectory + ".installing");
        }

        private static bool TryDeleteEmbeddedPackage(out string deleteError)
        {
            if (!TryDeleteDirectory(EmbeddedPackageDirectory, out deleteError))
            {
                return false;
            }

            return TryDeleteDirectory(EmbeddedPackageDirectory + ".installing", out deleteError);
        }

        private static void DeleteIntegrationScripts()
        {
            TryDeleteDirectory(IntegrationDestinationDirectory, out _);
        }

        private static void DeleteCachedArchive()
        {
            TryDeleteFile(CachedTarballPath);
            TryDeleteDirectoryIfEmpty(CacheDirectory);
            TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(CacheDirectory));
        }

        private static void MarkPendingUninstallCleanup(string deleteError)
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllText(
                PendingUninstallMarkerPath,
                "Local MediaPipe uninstall cleanup is pending because Unity could not delete the embedded package folder.\n" +
                "Close and reopen this Unity project, then the installer will retry cleanup automatically.\n\n" +
                deleteError);
        }

        private static void ClearPendingUninstallCleanup()
        {
            TryDeleteFile(PendingUninstallMarkerPath);
        }

        private static void SetPostInstallPending()
        {
            SessionState.SetBool(PendingPostInstallSessionKey, true);
            SessionState.SetInt(PostInstallPollCountSessionKey, 0);
        }

        private static void ClearPostInstallPending()
        {
            SessionState.EraseBool(PendingPostInstallSessionKey);
            SessionState.EraseInt(PostInstallPollCountSessionKey);
        }

        private static void RequestCleanScriptCompilation()
        {
            var optionsType =
                Type.GetType("UnityEditor.Compilation.RequestScriptCompilationOptions, UnityEditor.CoreModule") ??
                Type.GetType("UnityEditor.Compilation.RequestScriptCompilationOptions, UnityEditor");
            var cleanCompileMethod = optionsType == null
                ? null
                : typeof(CompilationPipeline).GetMethod("RequestScriptCompilation", new[] { optionsType });

            if (cleanCompileMethod != null)
            {
                var cleanBuildCache = Enum.Parse(optionsType, "CleanBuildCache");
                cleanCompileMethod.Invoke(null, new[] { cleanBuildCache });
                return;
            }

            CompilationPipeline.RequestScriptCompilation();
        }

        private static void NotifyLocalSetupChanged()
        {
            LocalSetupChanged?.Invoke();
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            File.Delete(path);
        }

        private static void TryDeleteDirectoryIfEmpty(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !Directory.Exists(path) ||
                Directory.GetFileSystemEntries(path).Length > 0)
            {
                return;
            }

            Directory.Delete(path);
        }

        private static void DeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
        }

        private static bool TryDeleteDirectory(string path, out string deleteError)
        {
            deleteError = string.Empty;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return true;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (UnauthorizedAccessException exception)
            {
                deleteError = exception.Message;
                return false;
            }
            catch (IOException exception)
            {
                deleteError = exception.Message;
                return false;
            }
        }

        private static bool PathsEqual(string first, string second)
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ToForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
