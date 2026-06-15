using System;
using System.Collections;
using System.IO;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace HEXLab.Hextrackingconnector
{
    public enum LocalLandmarkModel
    {
        Lite,
        Full,
        Heavy,
        Custom,
    }

    public enum LocalLandmarkInferenceDelegate
    {
        Cpu,
        Gpu,
    }

    [SkeletonPipelineNode("LocalLandmarkProvider")]
    public class LocalLandmarkProvider : MonoBehaviour, ISkeletonProvider, ISkeletonCaptureSource
    {
        private const string SourceId = "mediapipe.embedded.pose.33";
        private const string StreamingAssetsModelFolder = "MediaPipe";
        private const float NoPoseLogIntervalSeconds = 2f;

        [Header("Camera")]
        [Tooltip("Optional exact name from WebCamTexture.devices. Leave empty to automatically pick a camera.")]
        [SerializeField] private string cameraName;
        [SerializeField] private bool preferFrontFacingCamera;
        [Tooltip("Flip front-facing camera input before MediaPipe sees it. Disable if a device already supplies unmirrored front-camera frames.")]
        [SerializeField] private bool mirrorFrontFacingCameraInput = true;
        [SerializeField, Min(16)] private int requestedWidth = 640;
        [SerializeField, Min(16)] private int requestedHeight = 480;
        [SerializeField, Min(1)] private int requestedFps = 30;
        [SerializeField, Min(0f)] private float cameraStartupTimeoutSeconds = 10f;
        [SerializeField, Min(0)] private int maxInferenceFps = 30;

        [Header("MediaPipe")]
        [SerializeField] private LocalLandmarkModel model = LocalLandmarkModel.Full;
        [SerializeField] private TextAsset customModelAsset;
        [SerializeField] private LocalLandmarkInferenceDelegate inferenceDelegate = LocalLandmarkInferenceDelegate.Cpu;
        [SerializeField, Range(0f, 1f)] private float minPoseDetectionConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minPosePresenceConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minTrackingConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minJointConfidence;

        [Header("Skeleton Output")]
        [Tooltip("Invert MediaPipe world X so semantic left/right matches the external Python sender and Unity humanoid retargeting convention.")]
        [SerializeField] private bool invertWorldX = true;
        [Tooltip("Invert MediaPipe world Y so up is positive in Unity.")]
        [SerializeField] private bool invertWorldY = true;
        [Tooltip("Invert MediaPipe world Z so depth matches the external Python sender convention.")]
        [SerializeField] private bool invertWorldZ = true;
        [SerializeField, Min(0.0001f)] private float worldScale = 1f;
        [Tooltip("Use only when the landmark labels themselves are reversed. Normal coordinate conversion should use the X/Y/Z inversion fields.")]
        [SerializeField] private PoseMirrorMode mirrorMode = PoseMirrorMode.None;
        [SerializeField] private bool logStartupEvents = true;

        private readonly object capturedPoseLock = new object();

        private Coroutine runRoutine;
        private WebCamTexture webCamTexture;
        private WebCamDevice activeDevice;
        private TextureFrame textureFrame;
        private PoseLandmarker poseLandmarker;
        private PoseLandmarkerResult reusableResult;
        private TextAsset loadedDefaultModelAsset;
        private int sequenceNumber;
        private float lastInferenceTime;
        private float lastNoPoseLogTime = float.NegativeInfinity;
        private bool loggedFirstPose;

        private SkeletonFrame latestPose;
        private bool hasLatestPose;
        private SkeletonFrame latestCapturedPose;
        private bool hasLatestCapturedPose;

        public event Action<SkeletonFrame> PoseReceived;
        public event Action<SkeletonFrame> FrameCaptured;

        public bool IsRunning => runRoutine != null;
        public string ActiveCameraName => webCamTexture == null ? string.Empty : webCamTexture.deviceName;

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public bool TryGetLatestCapturedFrame(out SkeletonFrame frame)
        {
            lock (capturedPoseLock)
            {
                frame = latestCapturedPose;
                return hasLatestCapturedPose;
            }
        }

        private void OnEnable()
        {
            if (runRoutine != null)
            {
                return;
            }

            runRoutine = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            StopProvider();
        }

        private void Reset()
        {
            ApplyExternalPythonCoordinateDefaults();
        }

        [ContextMenu("Apply External Python Coordinate Defaults")]
        private void ApplyExternalPythonCoordinateDefaults()
        {
            invertWorldX = true;
            invertWorldY = true;
            invertWorldZ = true;
            mirrorMode = PoseMirrorMode.None;
        }

        private void OnValidate()
        {
            requestedWidth = Mathf.Max(16, requestedWidth);
            requestedHeight = Mathf.Max(16, requestedHeight);
            requestedFps = Mathf.Max(1, requestedFps);
            cameraStartupTimeoutSeconds = Mathf.Max(0f, cameraStartupTimeoutSeconds);
            maxInferenceFps = Mathf.Max(0, maxInferenceFps);
            worldScale = Mathf.Max(0.0001f, worldScale);
        }

        private IEnumerator Run()
        {
            yield return EnsureCameraPermission();

            if (!TryStartCamera())
            {
                runRoutine = null;
                yield break;
            }

            var cameraStartupStartTime = Time.realtimeSinceStartup;
            while (webCamTexture != null && webCamTexture.width <= 16)
            {
                if (cameraStartupTimeoutSeconds > 0f &&
                    Time.realtimeSinceStartup - cameraStartupStartTime >= cameraStartupTimeoutSeconds)
                {
                    Debug.LogWarning(
                        $"LocalLandmarkProvider camera '{ActiveCameraName}' did not produce frames within {cameraStartupTimeoutSeconds:0.0} seconds. Current WebCamTexture size is {webCamTexture.width}x{webCamTexture.height}, isPlaying={webCamTexture.isPlaying}. Check whether another application is holding the camera, try a different Camera Name, or lower the requested resolution/FPS.",
                        this);
                    StopAfterStartupFailure();
                    yield break;
                }

                yield return null;
            }

            if (webCamTexture == null)
            {
                Debug.LogWarning("LocalLandmarkProvider camera stopped before it produced a readable frame.", this);
                StopAfterStartupFailure();
                yield break;
            }

            textureFrame = new TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);
            if (logStartupEvents)
            {
                Debug.Log(
                    $"LocalLandmarkProvider camera '{ActiveCameraName}' ready at {webCamTexture.width}x{webCamTexture.height}.",
                    this);
            }

            var preparedModelPath = string.Empty;
            yield return PrepareModelPath(path => preparedModelPath = path);

            if (customModelAsset == null &&
                loadedDefaultModelAsset == null &&
                string.IsNullOrEmpty(preparedModelPath))
            {
                Debug.LogWarning(
                    "LocalLandmarkProvider could not prepare a MediaPipe pose model. Tracking will not start.",
                    this);
                StopAfterStartupFailure();
                yield break;
            }

            if (!TryCreatePoseLandmarker(preparedModelPath))
            {
                StopAfterStartupFailure();
                yield break;
            }

            if (logStartupEvents)
            {
                Debug.Log($"LocalLandmarkProvider started MediaPipe pose tracking from '{ActiveCameraName}'.", this);
            }

            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (enabled)
            {
                if (webCamTexture == null ||
                    poseLandmarker == null ||
                    !webCamTexture.didUpdateThisFrame ||
                    !ShouldRunInference())
                {
                    yield return null;
                    continue;
                }

                RunInferenceFrame();
                yield return waitForEndOfFrame;
            }
        }

        private IEnumerator EnsureCameraPermission()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                yield return null;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    yield return null;
                }
            }
#elif UNITY_IOS
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }
#else
            yield break;
#endif
        }

        private bool TryStartCamera()
        {
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("LocalLandmarkProvider could not find a webcam.", this);
                return false;
            }

            activeDevice = SelectCamera(devices);
            webCamTexture = new WebCamTexture(
                activeDevice.name,
                requestedWidth,
                requestedHeight,
                requestedFps);
            webCamTexture.Play();
            if (logStartupEvents)
            {
                Debug.Log(
                    $"LocalLandmarkProvider opened camera '{activeDevice.name}' at requested {requestedWidth}x{requestedHeight}@{requestedFps}.",
                    this);
            }

            return true;
        }

        private WebCamDevice SelectCamera(WebCamDevice[] devices)
        {
            if (!string.IsNullOrWhiteSpace(cameraName))
            {
                for (var i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(devices[i].name, cameraName, StringComparison.Ordinal))
                    {
                        return devices[i];
                    }
                }

                Debug.LogWarning($"LocalLandmarkProvider could not find configured camera '{cameraName}'. Falling back to an available device.", this);
            }

            for (var i = 0; i < devices.Length; i++)
            {
                if (devices[i].isFrontFacing == preferFrontFacingCamera)
                {
                    return devices[i];
                }
            }

            return devices[0];
        }

        private IEnumerator PrepareModelPath(Action<string> setPath)
        {
            setPath(string.Empty);
            loadedDefaultModelAsset = null;
            if (customModelAsset != null)
            {
                yield break;
            }

            var modelFileName = GetModelFileName();
            if (string.IsNullOrEmpty(modelFileName))
            {
                Debug.LogWarning(
                    "LocalLandmarkProvider is set to Custom but no custom MediaPipe model asset is assigned.",
                    this);
                yield break;
            }

            loadedDefaultModelAsset = Resources.Load<TextAsset>(
                CombinePath(StreamingAssetsModelFolder, Path.GetFileNameWithoutExtension(modelFileName)));
            if (loadedDefaultModelAsset != null)
            {
                if (logStartupEvents)
                {
                    Debug.Log(
                        $"LocalLandmarkProvider using package Resource model '{modelFileName}' ({loadedDefaultModelAsset.bytes.Length} bytes).",
                        this);
                }

                yield break;
            }

            var streamingRelativePath = CombinePath(StreamingAssetsModelFolder, modelFileName);
            var persistentPath = Path.Combine(
                Application.persistentDataPath,
                "HEXTrackingConnector",
                "MediaPipe",
                modelFileName);

            if (File.Exists(persistentPath))
            {
                setPath(persistentPath);
                if (logStartupEvents)
                {
                    Debug.Log($"LocalLandmarkProvider using cached model file '{persistentPath}'.", this);
                }

                yield break;
            }

            var streamingPath = Path.Combine(Application.streamingAssetsPath, streamingRelativePath);
            yield return CopyReadableAssetToFile(streamingPath, persistentPath);

            if (File.Exists(persistentPath))
            {
                setPath(persistentPath);
                if (logStartupEvents)
                {
                    Debug.Log($"LocalLandmarkProvider copied StreamingAssets model to '{persistentPath}'.", this);
                }

                yield break;
            }

#if UNITY_EDITOR
            var packageModelPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.github.homuler.mediapipe",
                "PackageResources",
                "Resources",
                "MediaPipe",
                modelFileName));

            if (File.Exists(packageModelPath))
            {
                setPath(packageModelPath);
                if (logStartupEvents)
                {
                    Debug.Log($"LocalLandmarkProvider using editor package model file '{packageModelPath}'.", this);
                }

                yield break;
            }

            var legacyPackageModelPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.github.homuler.mediapipe",
                "PackageResources",
                "MediaPipe",
                modelFileName));

            if (File.Exists(legacyPackageModelPath))
            {
                setPath(legacyPackageModelPath);
                if (logStartupEvents)
                {
                    Debug.Log($"LocalLandmarkProvider using legacy editor package model file '{legacyPackageModelPath}'.", this);
                }

                yield break;
            }
#endif

            Debug.LogWarning(
                $"LocalLandmarkProvider could not find model '{modelFileName}'. Run Tools > HEX Tracking Connector > Install Or Uninstall Local MediaPipe, or assign a custom model asset.",
                this);
        }

        private IEnumerator CopyReadableAssetToFile(string sourcePath, string destinationPath)
        {
            var uri = ToUri(sourcePath);
            using (var request = UnityWebRequest.Get(uri))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (logStartupEvents)
                    {
                        Debug.Log(
                            $"LocalLandmarkProvider did not find a readable StreamingAssets model at '{sourcePath}' ({request.error}). Falling back to package files.",
                            this);
                    }

                    yield break;
                }

                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(destinationPath, request.downloadHandler.data);
            }
        }

        private bool TryCreatePoseLandmarker(string preparedModelPath)
        {
            try
            {
                var baseOptions = customModelAsset != null
                    ? new BaseOptions(ToMediaPipeDelegate(), modelAssetBuffer: customModelAsset.bytes)
                    : loadedDefaultModelAsset != null
                        ? new BaseOptions(ToMediaPipeDelegate(), modelAssetBuffer: loadedDefaultModelAsset.bytes)
                        : new BaseOptions(ToMediaPipeDelegate(), modelAssetPath: preparedModelPath);

                var options = new PoseLandmarkerOptions(
                    baseOptions,
                    RunningMode.VIDEO,
                    numPoses: 1,
                    minPoseDetectionConfidence: minPoseDetectionConfidence,
                    minPosePresenceConfidence: minPosePresenceConfidence,
                    minTrackingConfidence: minTrackingConfidence);

                reusableResult = PoseLandmarkerResult.Alloc(1);
                poseLandmarker = PoseLandmarker.CreateFromOptions(options);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void RunInferenceFrame()
        {
            lastInferenceTime = Time.unscaledTime;

            var imageTransform = ImageTransformationOptions.Build(
                shouldFlipHorizontally: mirrorFrontFacingCameraInput && activeDevice.isFrontFacing,
                isVerticallyFlipped: webCamTexture.videoVerticallyMirrored,
                rotation: ToRotationAngle(webCamTexture.videoRotationAngle));

            textureFrame.ReadTextureOnCPU(
                webCamTexture,
                imageTransform.flipHorizontally,
                imageTransform.flipVertically);

            using (var image = textureFrame.BuildCPUImage())
            {
                var imageProcessingOptions = new ImageProcessingOptions(
                    rotationDegrees: (int)imageTransform.rotationAngle);

                if (!poseLandmarker.TryDetectForVideo(
                        image,
                        Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f),
                        imageProcessingOptions,
                        ref reusableResult))
                {
                    LogNoPose("MediaPipe did not detect a pose in the current camera frame.");
                    return;
                }
            }

            if (TryCreateSkeletonFrame(reusableResult, out var frame))
            {
                if (!loggedFirstPose && logStartupEvents)
                {
                    Debug.Log("LocalLandmarkProvider published its first MediaPipe skeleton frame.", this);
                    loggedFirstPose = true;
                }

                CaptureFrame(frame);
                PublishFrame(frame);
                return;
            }

            LogNoPose("MediaPipe returned a result, but it did not contain usable poseWorldLandmarks.");
        }

        private bool ShouldRunInference()
        {
            if (maxInferenceFps <= 0)
            {
                return true;
            }

            return Time.unscaledTime - lastInferenceTime >= 1f / maxInferenceFps;
        }

        private bool TryCreateSkeletonFrame(PoseLandmarkerResult result, out SkeletonFrame frame)
        {
            frame = default;
            if (result.poseWorldLandmarks == null ||
                result.poseWorldLandmarks.Count == 0 ||
                result.poseWorldLandmarks[0].landmarks == null)
            {
                return false;
            }

            var landmarks = result.poseWorldLandmarks[0].landmarks;
            var jointPoses = new SkeletonJointPose[HumanPoseSkeleton33.JointCount];
            var foundAny = false;

            for (var i = 0; i < landmarks.Count && i < MediaPipePose33Landmarks.LandmarkCount; i++)
            {
                if (!MediaPipePose33Landmarks.TryGetJoint(i, out var joint))
                {
                    continue;
                }

                var mappedIndex = HumanPoseSkeleton33.Definition.IndexOf(joint);
                if (!HumanPoseSkeleton33.Definition.IsValidIndex(mappedIndex))
                {
                    continue;
                }

                var landmark = landmarks[i];
                var confidence = GetConfidence(landmark);
                if (confidence < minJointConfidence)
                {
                    jointPoses[mappedIndex] = SkeletonJointPose.Unavailable;
                    continue;
                }

                jointPoses[mappedIndex] = SkeletonJointPose.FromPosition(
                    ToUnityPosition(landmark),
                    confidence,
                    SkeletonDataProvenance.Direct,
                    $"poseWorldLandmarks[{i}]");
                foundAny = true;
            }

            if (!foundAny)
            {
                return false;
            }

            var pose = new SkeletonPose(
                HumanPoseSkeleton33.Definition,
                jointPoses,
                SkeletonCoordinateSpace.RootRelative);

            if (mirrorMode == PoseMirrorMode.SwapLeftRight)
            {
                pose = SkeletonPoseTransforms.MirrorLeftRight(pose);
            }

            frame = new SkeletonFrame(
                pose,
                new SkeletonFrameMetadata(
                    ++sequenceNumber,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                    Time.realtimeSinceStartupAsDouble,
                    SourceId));
            return true;
        }

        private Vector3 ToUnityPosition(Landmark landmark)
        {
            return new Vector3(
                (invertWorldX ? -landmark.x : landmark.x) * worldScale,
                (invertWorldY ? -landmark.y : landmark.y) * worldScale,
                (invertWorldZ ? -landmark.z : landmark.z) * worldScale);
        }

        private static float GetConfidence(Landmark landmark)
        {
            var hasVisibility = landmark.visibility.HasValue;
            var hasPresence = landmark.presence.HasValue;
            if (hasVisibility && hasPresence)
            {
                return Mathf.Min(landmark.visibility.Value, landmark.presence.Value);
            }

            if (hasVisibility)
            {
                return landmark.visibility.Value;
            }

            return hasPresence ? landmark.presence.Value : 1f;
        }

        private void CaptureFrame(SkeletonFrame frame)
        {
            lock (capturedPoseLock)
            {
                latestCapturedPose = frame;
                hasLatestCapturedPose = true;
            }

            SkeletonProviderUtility.RaisePoseReceived(FrameCaptured, frame, this);
        }

        private void PublishFrame(SkeletonFrame frame)
        {
            latestPose = frame;
            hasLatestPose = true;
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, frame, this);
        }

        private void StopProvider()
        {
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            ReleaseRuntimeResources();
        }

        private void StopAfterStartupFailure()
        {
            runRoutine = null;
            ReleaseRuntimeResources();
        }

        private void LogNoPose(string message)
        {
            if (!logStartupEvents ||
                Time.unscaledTime - lastNoPoseLogTime < NoPoseLogIntervalSeconds)
            {
                return;
            }

            lastNoPoseLogTime = Time.unscaledTime;
            Debug.Log(message, this);
        }

        private void ReleaseRuntimeResources()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                webCamTexture = null;
            }

            if (textureFrame != null)
            {
                textureFrame.Dispose();
                textureFrame = null;
            }

            if (poseLandmarker != null)
            {
                ((IDisposable)poseLandmarker).Dispose();
                poseLandmarker = null;
            }

            loadedDefaultModelAsset = null;
            loggedFirstPose = false;
            lastNoPoseLogTime = float.NegativeInfinity;
        }

        private string GetModelFileName()
        {
            switch (model)
            {
                case LocalLandmarkModel.Lite:
                    return "pose_landmarker_lite.bytes";
                case LocalLandmarkModel.Heavy:
                    return "pose_landmarker_heavy.bytes";
                case LocalLandmarkModel.Custom:
                    return customModelAsset == null ? string.Empty : customModelAsset.name;
                case LocalLandmarkModel.Full:
                default:
                    return "pose_landmarker_full.bytes";
            }
        }

        private BaseOptions.Delegate ToMediaPipeDelegate()
        {
            switch (inferenceDelegate)
            {
                case LocalLandmarkInferenceDelegate.Gpu:
                    return BaseOptions.Delegate.GPU;
                case LocalLandmarkInferenceDelegate.Cpu:
                default:
                    return BaseOptions.Delegate.CPU;
            }
        }

        private static RotationAngle ToRotationAngle(int degrees)
        {
            var normalized = ((degrees % 360) + 360) % 360;
            switch (normalized)
            {
                case 90:
                    return RotationAngle.Rotation90;
                case 180:
                    return RotationAngle.Rotation180;
                case 270:
                    return RotationAngle.Rotation270;
                default:
                    return RotationAngle.Rotation0;
            }
        }

        private static string CombinePath(string first, string second)
        {
            return string.IsNullOrEmpty(first)
                ? second
                : first.TrimEnd('/', '\\') + "/" + second.TrimStart('/', '\\');
        }

        private static string ToUri(string path)
        {
            if (path.Contains("://"))
            {
                return path;
            }

            return new Uri(path).AbsoluteUri;
        }
    }
}
