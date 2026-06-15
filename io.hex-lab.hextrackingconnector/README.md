# HEX Tracking Connector

Unity runtime package for receiving body pose frames from an external Python tracker, converting the wire data into Unity-side skeleton poses, and exposing those poses through a chain of simple skeleton-provider blocks.

The central idea is the `ISkeletonProvider` pipeline:

```text
Python tracker
  -> CommServer
  -> SkeletonPoseRecorder, optional raw/high-fidelity capture
  -> SkeletonSmoothing
  -> SkeletonConverter, if a different skeleton layout is needed
  -> BodyCalibration
  -> SkeletonPoseRecorder, optional processed capture
  -> BodyDebugVis, DirectHumanoidBoneDriver, or your own script
```

Each block either produces or consumes `SkeletonFrame` objects. A block that produces frames implements `ISkeletonProvider`; a consumer can subscribe to `PoseReceived` and can also ask for the latest pose with `TryGetLatestPose`.

## Quick Start

1. Add the `Communication` prefab to the scene.
2. Configure `CommServer`:
   - `Transport`: named pipe or UDP.
   - `Input Skeleton`: usually `Auto`.
   - `Coordinate Source`: `Free` for world/PnP coordinates or `Anchored` for body-relative world landmarks.
   - `Mirror Mode`: optional left/right mirroring.
3. Use the `Smoothed` child object on the `Communication` prefab as the default provider for most consumers.
4. Add `Visuals` for line-art debug output, or add `DirectHumanoidBoneDriver` to a humanoid avatar.
5. Assign provider fields in the inspector. The custom provider drawer shows the pipeline flow and warns about invalid assignments.

The demo scene wires the blocks as:

```text
Communication/CommServer
  -> Communication/Smoothed/SkeletonSmoothing
  -> Visuals/SkeletonConverter
  -> Visuals/BodyCalibration
  -> Visuals/BodyDebugVis
```

The reusable `Visuals` and robot prefabs intentionally leave some provider fields unassigned. When you drag them into a new scene, assign their source/provider fields to the provider block you want to consume.

## Standalone MediaPipe Pose Tracking

Embedded MediaPipe support is optional. The connector package does not vendor Homuler MediaPipe or make it a hard dependency, so the existing external pipe/UDP workflow continues to compile unchanged.

To enable local pose detection in a Unity project:

1. Run `Tools > HEX Tracking Connector > Install Or Uninstall Local MediaPipe`.
2. Review the source project link, release archive link, download size, and generated-package note in the setup window.
3. Optionally open `Advanced Model Selection`. The three Pose Landmarker task models used by `LocalLandmarkProvider` are required and always kept; other Homuler models are stripped unless selected.
4. Click `Install`. The installer downloads `com.github.homuler.mediapipe-0.16.3.tgz` from the Homuler GitHub release, extracts it into `Packages/com.github.homuler.mediapipe` as an embedded package, removes samples, tests, and unused model files, moves the retained models into package `Resources`, copies the connector's `LocalLandmarkProvider` integration into that embedded package, and deletes the temporary archive.
5. Run `Tools > HEX Tracking Connector > Validate Local MediaPipe Setup` to open the validator window for package, native plugin, model, and build-setting checks.
6. Add `LocalLandmarkProvider` to a scene and use it anywhere an `ISkeletonProvider` is accepted.

The generated `Packages/com.github.homuler.mediapipe` folder is intentionally a stripped embedded package, not the full Homuler release. Commit it together with `Packages/packages-lock.json` when a Unity project should open with local MediaPipe already installed after clone. If another Homuler model is needed later, uninstall and install again with that model selected in the advanced section, then commit the updated embedded package.

`LocalLandmarkProvider` reads from a Unity `WebCamTexture`, runs Homuler's `PoseLandmarker` locally, maps `poseWorldLandmarks` into `HumanPoseSkeleton33`, and publishes regular `SkeletonFrame` objects. It also implements `ISkeletonCaptureSource`, so `SkeletonPoseRecorder` can capture its raw local frames just like it can capture `CommServer` frames.

The `Camera Name` field is optional. Leave it empty to let Unity choose a matching camera from `WebCamTexture.devices`; only fill it when you need a specific device and know its exact Unity device name. The default coordinate conversion inverts MediaPipe world X, Y, and Z so local world landmarks match the same Unity-facing convention expected from the external Python sender.

Recommended local pipeline:

```text
LocalLandmarkProvider -> SkeletonSmoothing -> BodyCalibration -> consumers
```

For mobile builds, make sure:

- `Packages/com.github.homuler.mediapipe/PackageResources/Resources/MediaPipe/pose_landmarker_lite.bytes`, `pose_landmarker_full.bytes`, and `pose_landmarker_heavy.bytes` exist.
- iOS has a Camera Usage Description in Player Settings.
- Android/iOS permissions are tested on device, not only in the editor.
- Start with the CPU delegate; enable the GPU delegate only after the Homuler native plugin is verified for the target device.
- If another native plugin also ships `libc++_shared.so` on Android, resolve the duplicate-library conflict during the build.

## Reading Pose Data

Prefer depending on `ISkeletonProvider`, not directly on `CommServer`. That lets your script use raw, smoothed, converted, or calibrated data without changing code.

```csharp
using HEXLab.Hextrackingconnector;
using UnityEngine;

public class PoseConsumer : MonoBehaviour
{
    [SerializeField, SkeletonProvider] private MonoBehaviour skeletonProvider;

    private ISkeletonProvider activeProvider;

    private void OnEnable()
    {
        if (SkeletonProviderUtility.TryResolveProvider(
                skeletonProvider,
                this,
                nameof(skeletonProvider),
                allowSelf: true,
                out activeProvider))
        {
            activeProvider.PoseReceived += OnPoseReceived;
        }
    }

    private void OnDisable()
    {
        if (activeProvider != null)
        {
            activeProvider.PoseReceived -= OnPoseReceived;
        }
    }

    private void OnPoseReceived(SkeletonFrame frame)
    {
        if (frame.TryGetJoint(BodyJoints.LeftWrist, out var leftWrist))
        {
            Debug.Log(leftWrist);
        }
    }
}
```

Use `TryGetJoint(...)` for position-only code. Use `TryGetJointPose(...)` when you need optional rotation, confidence, provenance, or source labels.

## Provider Blocks

| Block | Role | Input | Output | Use When |
|---|---|---|---|---|
| `CommServer` | Receives framed JSON from Python over named pipe or UDP and maps wire landmark indices to named Unity joints. | External process. | `HumanPoseSkeleton33` by default. | You need the live tracker connection. |
| `LocalLandmarkProvider` | Runs Homuler MediaPipe Pose Landmarker in Unity from a local camera. | `WebCamTexture`. | `HumanPoseSkeleton33`. | You need standalone pose tracking for demos, mobile deployment, or offline testing. |
| `SkeletonSmoothing` | Smooths positions over recent frames. | Any `ISkeletonProvider`. | Same skeleton definition as input. | Tracking jitter should be reduced before calibration or visualization. |
| `SkeletonConverter` | Converts a frame to another supported skeleton definition. | Any `ISkeletonProvider`. | Source, `HumanPoseSkeleton33`, `CocoPose17`, or `UnityHumanoidControl`. | A consumer expects a smaller skeleton or humanoid-control pose. |
| `BodyCalibration` | Applies an additive offset so the body is centered and optionally grounded. | Any `ISkeletonProvider`. | Same skeleton definition as input. | Multiple consumers should share the same calibrated pose. |
| `SkeletonPoseRecorder` | Stores frames from a provider output or capture source. | Any `ISkeletonProvider`, or `ISkeletonCaptureSource` for high-fidelity CommServer capture. | Recording file. | You want JSONL debug captures or compact binary pose streams. |
| `SkeletonPosePlayback` | Replays stored pose recordings. | Recording file. | Recorded `SkeletonFrame` stream. | You want deterministic replay, demos, tests, or offline visualization. |
| `SkeletonProviderSwitcher` | Routes one selected provider to consumers. | Up to three `ISkeletonProvider` inputs. | Selected provider stream. | You want to switch between external live tracking, local MediaPipe tracking, and replay without reassigning consumers. |
| `BodyDebugVis` | Draws joints, debug line strips, and optional head pose. | Any `ISkeletonProvider`. | Visual output only. | You want to inspect incoming or processed tracking data. |
| `DirectHumanoidBoneDriver` | Applies humanoid-control frames to a Unity humanoid `Animator`. | `UnityHumanoidControl` frames, or compatible frames when retargeting is enabled. | Avatar motion. | You want to drive a humanoid avatar directly from tracking data. |

Recommended order:

```text
CommServer -> SkeletonSmoothing -> SkeletonConverter if needed -> BodyCalibration -> consumers
```

Smooth before calibration so the calibration block sees stable data. Convert before calibration when the consumer needs a target skeleton such as `UnityHumanoidControl`; calibrating after conversion keeps the final consumer space easy to reason about.

## Recording And Playback

Use `SkeletonPoseRecorder` to write pose streams without adding storage logic to `CommServer`.

The recorder writes to a folder, not to a fixed file path. Each recording creates a new file named with the current date/time and Unity product name, so repeated recording sessions do not overwrite earlier captures.

Recording modes:

- `Provider Output`: records frames published by any `ISkeletonProvider`. This captures exactly what downstream consumers see, including smoothing, conversion, or calibration.
- `Capture Source`: records every frame exposed by an `ISkeletonCaptureSource`. `CommServer` implements this for high-fidelity capture immediately after wire data is converted to `SkeletonFrame`.
- `Auto`: prefers capture-source recording when available, otherwise records provider output.

Supported recording formats:

- `JSONL` (`.jsonl`): one header line, one frame per line, and a footer when recording closes. This is useful for debugging, diffing, and hand-authored synthetic data.
- `Binary` (`.hexpose`): a compact versioned stream with the same schema, better for long captures.

Each frame stores a recording-relative timestamp, original frame metadata, coordinate space, joint channels, positions, rotations, confidence, provenance, and source labels. Playback defaults to recorded timing, but can also use source timestamps, received timestamps, or a fixed frame rate.

Example recordings should be shipped as Package Manager samples. After importing
them into a project, select the desired `.hexpose` or `.jsonl` file with the
regular `SkeletonPosePlayback` recording picker.

Use `SkeletonPosePlayback` as a normal provider:

```text
SkeletonPosePlayback -> SkeletonSmoothing -> SkeletonConverter -> BodyCalibration -> consumers
```

For live/local/replay switching, put `SkeletonProviderSwitcher` before consumers:

```text
External provider  -> SkeletonProviderSwitcher -> consumers
Local provider     -> SkeletonProviderSwitcher
Playback provider  -> SkeletonProviderSwitcher
```

## Runtime Model

`PoseFrame` and `WireLandmarkData` are internal wire DTOs that mirror the Python JSON payload. User code should consume `SkeletonFrame`.

`SkeletonFrame` wraps:

- `SkeletonPose`: timeless joint data.
- `SkeletonFrameMetadata`: sequence number, receive time, optional source timestamp, and source id.

`SkeletonPose` carries a `SkeletonDefinition`, which names the joint layout and debug topology. The default input definition is `HumanPoseSkeleton33`, matching MediaPipe Pose 33 landmarks. Access shared human joints through `BodyJoints`, for example `BodyJoints.Nose`, `BodyJoints.LeftWrist`, and `BodyJoints.RightAnkle`.

Each joint stores only the channels that are actually available. A joint can have position, rotation, confidence, or a subset of those values. Missing channels stay missing; downstream code decides what it requires.

`CommServer` also implements `ISkeletonCaptureSource` for tooling that needs every decoded source frame. Capture events may be raised from the transport thread, so use them only for quick, thread-safe work such as enqueueing frames in `SkeletonPoseRecorder`. Regular consumers should continue to use `ISkeletonProvider`.

## Skeleton Definitions

Current built-in definitions:

| Definition | Joint Count | Purpose |
|---|---:|---|
| `HumanPoseSkeleton33` | 33 | Default MediaPipe-style human pose layout received from Python. |
| `CocoPoseSkeleton17` | 17 | Smaller body layout for code or visualizations that do not need hands, feet detail, or face detail. |
| `UnityHumanoidControlSkeleton` | 22 | Control skeleton whose joints map to Unity `HumanBodyBones`. Used by `DirectHumanoidBoneDriver`. |

`CocoPoseSkeleton17.TryCreateFrom(frame, out var cocoFrame)` converts compatible `HumanPoseSkeleton33` frames to COCO.

`UnityHumanoidPoseRetargeter.TryCreateFrom(frame, out var humanoidFrame)` creates a best-effort humanoid-control pose. It derives body, limb, foot, and hand rotations from landmarks when direct rotations are not present, marks inferred values with `SkeletonDataProvenance.Inferred`, and leaves unavailable channels missing.

## Extending The Pipeline

### Add A Consumer

Create a `MonoBehaviour` with a `[SkeletonProvider] MonoBehaviour` field, resolve it to `ISkeletonProvider`, subscribe in `OnEnable`, and unsubscribe in `OnDisable`. This is the simplest way to build student scripts that work with any pipeline stage.

### Add A Processing Block

Implement `ISkeletonProvider` when your component receives frames from one provider and republishes processed frames.

```csharp
[SkeletonPipelineNode("MyPoseBlock")]
public class MyPoseBlock : MonoBehaviour, ISkeletonProvider
{
    [SerializeField, SkeletonProvider(allowSelf: false)] private MonoBehaviour sourceProvider;

    private ISkeletonProvider activeSource;
    private SkeletonFrame latestFrame;
    private bool hasLatestFrame;

    public event System.Action<SkeletonFrame> PoseReceived;

    public bool TryGetLatestPose(out SkeletonFrame pose)
    {
        pose = latestFrame;
        return hasLatestFrame;
    }
}
```

Use `SkeletonProviderUtility.RaisePoseReceived(...)` when republishing so one bad subscriber cannot interrupt the rest of the pipeline.

### Add A New Wire Skeleton

If Python sends a different landmark layout, create:

1. A `SkeletonDefinition` for the Unity-side joint names.
2. An `IWireSkeletonMapper` that maps incoming landmark indices to `SkeletonJointId`s.
3. A registration call:

```csharp
InputSkeletonRegistry.Register("my.tracker.skeleton", new MyWireSkeletonMapper());
```

When `CommServer.InputSkeleton` is `Auto`, incoming `skeleton_id` values are resolved through this registry. Older senders that omit `skeleton_id` are treated as MediaPipe Pose 33.

## Package Layout

| Location | Contents |
|---|---|
| `Runtime/Wire` | Transport, JSON DTOs, input skeleton selection, and wire-index mapping. |
| `Runtime/Skeleton/Core` | Public pose/frame/definition primitives and provider contract. |
| `Runtime/Skeleton/Definitions` | Built-in anatomical skeleton definitions. |
| `Runtime/Skeleton/Providers` | Pipeline blocks such as smoothing, conversion, and calibration. |
| `Runtime/Skeleton/Humanoid` | Unity humanoid control definition, retargeting, and avatar driver. |
| `Runtime/MediaPipe` | Non-compiled templates for the optional Homuler-backed local pose provider. The installer copies them into the embedded MediaPipe package. |
| `Runtime/Recording` | Pose recording, playback, JSONL and binary storage. |
| `Runtime/Visualization` | Debug body visualization and generated debug materials. |
| `Editor` | Inspector tooling for provider validation, pipeline flow display, component editors, and optional MediaPipe setup. |
| `Samples/Demo` | Example scene with communication, smoothing, calibration, visualization, and robot avatar setup. |
| `Samples~/Recordings` | Package Manager sample folder for example pose recordings. |

## Verification Notes

`CommServer` publishes at most one pose event per Unity frame. If several transport packets arrive before `Update`, the latest pending pose is published. Smoothing should be handled by `SkeletonSmoothing`, not by `CommServer`.
