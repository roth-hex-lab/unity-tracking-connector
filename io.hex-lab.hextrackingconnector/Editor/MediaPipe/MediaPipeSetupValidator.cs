using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal enum MediaPipeValidationStatus
    {
        Pass,
        Info,
        Warning,
        Error,
    }

    internal sealed class MediaPipeValidationItem
    {
        public MediaPipeValidationItem(
            string group,
            string name,
            MediaPipeValidationStatus status,
            string detail)
        {
            Group = group;
            Name = name;
            Status = status;
            Detail = detail;
        }

        public string Group { get; }
        public string Name { get; }
        public MediaPipeValidationStatus Status { get; }
        public string Detail { get; }
    }

    internal sealed class MediaPipeValidationReport
    {
        public MediaPipeValidationReport(IReadOnlyList<MediaPipeValidationItem> items)
        {
            Items = items;
        }

        public IReadOnlyList<MediaPipeValidationItem> Items { get; }

        public int ErrorCount
        {
            get
            {
                var count = 0;
                foreach (var item in Items)
                {
                    if (item.Status == MediaPipeValidationStatus.Error)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int WarningCount
        {
            get
            {
                var count = 0;
                foreach (var item in Items)
                {
                    if (item.Status == MediaPipeValidationStatus.Warning)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsReady => ErrorCount == 0;
    }

    internal static class MediaPipeSetupValidator
    {
        private const string MenuRoot = "Tools/HEX Tracking Connector/";
        private const string StreamingAssetsModelFolder = "MediaPipe";
        private const string StreamingAssetsModelAssetFolder = "Assets/StreamingAssets/MediaPipe";

        internal static readonly string[] PoseModelFiles =
        {
            "pose_landmarker_lite.bytes",
            "pose_landmarker_full.bytes",
            "pose_landmarker_heavy.bytes",
        };

        [MenuItem(MenuRoot + "Validate Local MediaPipe Setup")]
        private static void ValidateSetupMenu()
        {
            MediaPipeValidationWindow.ShowWindow();
        }

        internal static MediaPipeValidationReport EvaluateSetup(bool includeBuildSettings)
        {
            var items = new List<MediaPipeValidationItem>();

            if (!MediaPipeInstaller.TryGetLocalPackageRoot(out var packageRoot))
            {
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Local MediaPipe package",
                    MediaPipeValidationStatus.Error,
                    "Not installed. Use the install window to add local MediaPipe pose tracking."));
            }
            else
            {
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Local MediaPipe package",
                    MediaPipeValidationStatus.Pass,
                    packageRoot));
                ValidateInstalledPackage(packageRoot, items);
            }

            ValidatePackageSource(items);
            ValidateIntegrationScripts(items);
            ValidateRuntimeModels(items);
            if (includeBuildSettings)
            {
                ValidatePlayerSettings(items);
            }

            items.Add(new MediaPipeValidationItem(
                "Build Notes",
                "Android native libraries",
                MediaPipeValidationStatus.Info,
                "If another plugin also ships libc++_shared.so, resolve the duplicate-library conflict during Android builds."));
            items.Add(new MediaPipeValidationItem(
                "Build Notes",
                "GPU delegate",
                MediaPipeValidationStatus.Info,
                "Start with CPU in LocalLandmarkProvider. Enable GPU per target after verifying the Homuler native plugin on that device."));
            return new MediaPipeValidationReport(items);
        }

        internal static bool ValidateSetup(bool showDialog)
        {
            return ValidateSetup(showDialog, includeBuildSettings: true);
        }

        internal static bool ValidateSetup(bool showDialog, bool includeBuildSettings)
        {
            var report = EvaluateSetup(includeBuildSettings);
            var summary = BuildSummary(report);
            if (report.ErrorCount > 0)
            {
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log(summary);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    report.ErrorCount > 0 ? "MediaPipe Setup Needs Attention" : "MediaPipe Setup",
                    summary,
                    "OK");
            }

            return report.ErrorCount == 0;
        }

        internal static bool AreRequiredPoseModelsAvailable()
        {
            foreach (var modelFile in PoseModelFiles)
            {
                if (!TryFindPackageModelFile(modelFile, out _))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool HasAnyPoseModelCopy()
        {
            var modelDirectory = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                StreamingAssetsModelFolder);

            foreach (var modelFile in PoseModelFiles)
            {
                if (File.Exists(Path.Combine(modelDirectory, modelFile)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int CopyPoseModelsToStreamingAssets(bool overwriteExisting)
        {
            if (!MediaPipeInstaller.TryGetLocalPackageRoot(out var packageRoot))
            {
                Debug.LogWarning(
                    "Cannot copy MediaPipe pose models because local MediaPipe pose tracking is not installed.");
                return 0;
            }

            var destinationDirectory = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                StreamingAssetsModelFolder);
            Directory.CreateDirectory(destinationDirectory);

            var copiedCount = 0;
            foreach (var modelFile in PoseModelFiles)
            {
                if (!TryFindPackageModelFile(packageRoot, modelFile, out var sourcePath))
                {
                    Debug.LogWarning("Missing MediaPipe package model: " + modelFile);
                    continue;
                }

                var destinationPath = Path.Combine(destinationDirectory, modelFile);
                if (File.Exists(destinationPath) && !overwriteExisting)
                {
                    continue;
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                copiedCount++;
            }

            if (copiedCount > 0)
            {
                AssetDatabase.Refresh();
            }

            return copiedCount;
        }

        internal static void DeletePoseModelsFromStreamingAssets()
        {
            var deletedAny = false;
            foreach (var modelFile in PoseModelFiles)
            {
                deletedAny |= AssetDatabase.DeleteAsset(StreamingAssetsModelAssetFolder + "/" + modelFile);
            }

            var modelDirectory = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                StreamingAssetsModelFolder);
            if (Directory.Exists(modelDirectory) &&
                Directory.GetFileSystemEntries(modelDirectory).Length == 0)
            {
                deletedAny |= AssetDatabase.DeleteAsset(StreamingAssetsModelAssetFolder);
            }

            if (deletedAny)
            {
                AssetDatabase.Refresh();
            }
        }

        private static void ValidateInstalledPackage(
            string packageRoot,
            ICollection<MediaPipeValidationItem> items)
        {
            var packageJsonPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Package manifest",
                    MediaPipeValidationStatus.Warning,
                    "Missing package.json at " + packageJsonPath + "."));
            }
            else
            {
                var packageJson = File.ReadAllText(packageJsonPath);
                var hasExpectedVersion = packageJson.Contains("\"version\": \"" + MediaPipeInstaller.PackageVersion + "\"");
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Package version",
                    hasExpectedVersion ? MediaPipeValidationStatus.Pass : MediaPipeValidationStatus.Warning,
                    hasExpectedVersion
                        ? MediaPipeInstaller.PackageVersion
                        : "Installed package does not appear to be " + MediaPipeInstaller.PackageVersion + "."));
            }

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Mediapipe.Runtime.asmdef"),
                "Package",
                "Mediapipe.Runtime assembly",
                items);

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Plugins", "mediapipe_c.dll"),
                "Native Plugins",
                "Windows native plugin",
                items);

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Plugins", "libmediapipe_c.dylib"),
                "Native Plugins",
                "macOS native plugin",
                items);

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Plugins", "libmediapipe_c.so"),
                "Native Plugins",
                "Linux native plugin",
                items);

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Plugins", "Android", "mediapipe_android.aar"),
                "Native Plugins",
                "Android native plugin",
                items);

            AddPackageFileCheck(
                packageRoot,
                Path.Combine("Runtime", "Plugins", "iOS", "MediaPipeUnity.framework", "MediaPipeUnity"),
                "Native Plugins",
                "iOS native framework",
                items);

            AddPackageDirectoryAbsentCheck(
                packageRoot,
                "Samples~",
                "Package",
                "Samples stripped",
                items);

            AddPackageDirectoryAbsentCheck(
                packageRoot,
                "Tests",
                "Package",
                "Tests stripped",
                items);

            foreach (var modelFile in PoseModelFiles)
            {
                AddPackageFileCheck(
                    packageRoot,
                    Path.Combine("PackageResources", "Resources", "MediaPipe", modelFile),
                    "Package Models",
                    modelFile,
                    items);
            }
        }

        private static void ValidatePackageSource(ICollection<MediaPipeValidationItem> items)
        {
            if (Directory.Exists(MediaPipeInstaller.EmbeddedPackageDirectory))
            {
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Package source",
                    MediaPipeValidationStatus.Pass,
                    "Embedded package at " + MediaPipeInstaller.EmbeddedPackageDirectory + ". Commit this folder with Packages/packages-lock.json for a clone-ready local MediaPipe setup."));
                return;
            }

            if (MediaPipeInstaller.IsPackageInstalled())
            {
                items.Add(new MediaPipeValidationItem(
                    "Package",
                    "Package source",
                    MediaPipeValidationStatus.Warning,
                    "Installed through Unity Package Manager rather than as an embedded package. Click Install in the setup window to migrate to the stripped embedded layout."));
                return;
            }

            items.Add(new MediaPipeValidationItem(
                "Package",
                "Package source",
                MediaPipeValidationStatus.Info,
                "Not installed yet."));
        }

        private static void ValidateRuntimeModels(ICollection<MediaPipeValidationItem> items)
        {
            foreach (var modelFile in PoseModelFiles)
            {
                var found = TryFindPackageModelFile(modelFile, out var path);
                items.Add(new MediaPipeValidationItem(
                    "Runtime Models",
                    modelFile,
                    found ? MediaPipeValidationStatus.Pass : MediaPipeValidationStatus.Error,
                    found
                        ? path
                        : "Missing. Use Install Or Uninstall Local MediaPipe to repair the local setup."));
            }
        }

        private static void ValidateIntegrationScripts(ICollection<MediaPipeValidationItem> items)
        {
            if (!Directory.Exists(MediaPipeInstaller.EmbeddedPackageDirectory))
            {
                return;
            }

            AddPackageFileCheck(
                MediaPipeInstaller.EmbeddedPackageDirectory,
                Path.Combine(
                    "HEXTrackingConnector",
                    "HEXLab.Hextrackingconnector.MediaPipe.asmdef"),
                "Integration",
                "LocalLandmarkProvider assembly",
                items);

            AddPackageFileCheck(
                MediaPipeInstaller.EmbeddedPackageDirectory,
                Path.Combine("HEXTrackingConnector", "LocalLandmarkProvider.cs"),
                "Integration",
                "LocalLandmarkProvider source",
                items);
        }

        private static void ValidatePlayerSettings(ICollection<MediaPipeValidationItem> items)
        {
            var hasCameraDescription = !string.IsNullOrWhiteSpace(PlayerSettings.iOS.cameraUsageDescription);
            items.Add(new MediaPipeValidationItem(
                "Build Settings",
                "iOS camera usage description",
                hasCameraDescription ? MediaPipeValidationStatus.Pass : MediaPipeValidationStatus.Warning,
                hasCameraDescription
                    ? PlayerSettings.iOS.cameraUsageDescription
                    : "Set Player Settings > iOS > Other Settings > Camera Usage Description before building for iOS."));
        }

        private static void AddPackageFileCheck(
            string packageRoot,
            string relativePath,
            string group,
            string label,
            ICollection<MediaPipeValidationItem> items)
        {
            var path = Path.Combine(packageRoot, relativePath);
            items.Add(new MediaPipeValidationItem(
                group,
                label,
                File.Exists(path) ? MediaPipeValidationStatus.Pass : MediaPipeValidationStatus.Error,
                File.Exists(path) ? relativePath : "Missing: " + path));
        }

        private static void AddPackageDirectoryAbsentCheck(
            string packageRoot,
            string relativePath,
            string group,
            string label,
            ICollection<MediaPipeValidationItem> items)
        {
            var path = Path.Combine(packageRoot, relativePath);
            items.Add(new MediaPipeValidationItem(
                group,
                label,
                Directory.Exists(path) ? MediaPipeValidationStatus.Warning : MediaPipeValidationStatus.Pass,
                Directory.Exists(path)
                    ? "Present. Reinstall to generate the stripped embedded package."
                    : "Removed from stripped embedded package."));
        }

        private static bool TryFindPackageModelFile(string modelFile, out string path)
        {
            if (MediaPipeInstaller.TryGetLocalPackageRoot(out var packageRoot))
            {
                return TryFindPackageModelFile(packageRoot, modelFile, out path);
            }

            path = string.Empty;
            return false;
        }

        private static bool TryFindPackageModelFile(
            string packageRoot,
            string modelFile,
            out string path)
        {
            var resourcePath = Path.Combine(
                MediaPipeInstaller.GetPackageResourceModelDirectory(packageRoot),
                modelFile);
            if (File.Exists(resourcePath))
            {
                path = resourcePath;
                return true;
            }

            var legacyPath = Path.Combine(
                MediaPipeInstaller.GetLegacyPackageModelDirectory(packageRoot),
                modelFile);
            if (File.Exists(legacyPath))
            {
                path = legacyPath;
                return true;
            }

            path = string.Empty;
            return false;
        }

        private static string BuildSummary(MediaPipeValidationReport report)
        {
            var lines = new List<string>();
            lines.Add("HEX Tracking Connector MediaPipe setup");

            AppendSection(lines, "Errors", report, MediaPipeValidationStatus.Error);
            AppendSection(lines, "Warnings", report, MediaPipeValidationStatus.Warning);
            AppendSection(lines, "Notes", report, MediaPipeValidationStatus.Info);

            if (report.ErrorCount == 0)
            {
                lines.Add("Ready for LocalLandmarkProvider.");
            }

            return string.Join("\n", lines);
        }

        private static void AppendSection(
            ICollection<string> lines,
            string title,
            MediaPipeValidationReport report,
            MediaPipeValidationStatus status)
        {
            var addedHeader = false;
            foreach (var item in report.Items)
            {
                if (item.Status != status)
                {
                    continue;
                }

                if (!addedHeader)
                {
                    lines.Add(string.Empty);
                    lines.Add(title + ":");
                    addedHeader = true;
                }

                lines.Add("- " + item.Name + ": " + item.Detail);
            }
        }
    }
}
