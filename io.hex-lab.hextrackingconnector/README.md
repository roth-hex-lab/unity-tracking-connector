# HEX Tracking Connector

Unity runtime package for receiving body pose frames from external trackers, exposing them as stable skeleton data, and using them for debug visualization or humanoid avatar control.

## Quick Start

1. Add the `Communication` prefab to the scene.
2. Choose the transport, input skeleton, coordinate source, mirror mode, and smoothing algorithm on the `CommServer` component.
3. Subscribe to pose frames from your own script:

```csharp
using HEXLab.Hextrackingconnector;
using UnityEngine;

public class PoseConsumer : MonoBehaviour
{
    [SerializeField] private CommServer commServer;

    private void OnEnable()
    {
        commServer.PoseReceived += OnPoseReceived;
    }

    private void OnDisable()
    {
        commServer.PoseReceived -= OnPoseReceived;
    }

    private void OnPoseReceived(SkeletonFrame frame)
    {
        if (frame.LeftWrist.IsTracked)
        {
            Debug.Log(frame.LeftWrist.Position);
        }
    }
}
```

For a simple line-art visualization, add the `DebugBody` prefab to the scene. Its `BodyDebugVis` component subscribes to any component that implements `ISkeletonProvider`; `CommServer` is the default provider. The visualizer owns its own drawing, scaling, and head visualization settings. It can also use a `BodyCalibration` component on the same GameObject for local one-shot visualization calibration; disable `Apply Calibration` if the incoming provider is already calibrated.

To calibrate data for any consumer, put `BodyCalibration` in the skeleton provider pipeline: assign its source provider, then point `BodyDebugVis`, `DirectHumanoidBoneDriver`, or student scripts at the calibration component. `BodyCalibration` republishes calibrated `SkeletonFrame`s, keeps frame metadata and rotation channels intact, and still exposes the older one-shot `Apply(...)` methods for local visualization code.

To drive a humanoid avatar directly, add `DirectHumanoidBoneDriver` to a GameObject with a humanoid `Animator`, then assign an `ISkeletonProvider` such as `BodyCalibration`, `CommServer`, or `SkeletonConverter`. The driver consumes `UnityHumanoidControlSkeleton` frames directly, or can retarget compatible human skeleton poses into that control skeleton as a best-effort pose.

## Runtime Model

`PoseFrame` and `WireLandmarkData` are internal wire DTOs that match incoming JSON payloads. User code should consume `SkeletonFrame`, which is the public Unity-facing wrapper around a timeless `SkeletonPose` plus frame metadata such as sequence number, receive time, source timestamp, and source id. `SkeletonPose` holds the actual joint sample data and can be stored, replayed, converted, or wrapped in a new `SkeletonFrame` without mixing pose state with transport timing.

Each pose carries a `SkeletonDefinition`; the current default input is `HumanPoseSkeleton33`, selected automatically when older senders omit `skeleton_id`. Access built-in human joints through properties such as `frame.Nose`, `frame.LeftWrist`, or `frame.TryGetJoint(SkeletonJoint.LeftWrist, out var wrist)`. For richer data, use `TryGetJointPose` to read optional position, rotation, confidence, provenance, and source labels from a `SkeletonJointPose`.

Wire landmarks may provide positions only, or optional rotations, confidence values, provenance, and source labels. Missing channels remain missing in the `SkeletonPose`; downstream components can choose whether to require positions, rotations, or a specific skeleton definition.

`CocoPoseSkeleton17.TryCreateFrom(frame, out var cocoFrame)` converts a `HumanPoseSkeleton33` frame to the COCO 17-joint layout when a smaller target skeleton is useful.

`UnityHumanoidPoseRetargeter.TryCreateFrom(frame, out var humanoidFrame)` converts compatible human pose frames to `UnityHumanoidControlSkeleton`, a skeleton definition whose joints map to Unity `HumanBodyBones`. It derives spine, limb, foot, and MediaPipe-style hand rotations from available landmarks where necessary, marks inferred values as `SkeletonDataProvenance.Inferred`, and assigns lower confidence than direct input data. Missing channels remain unavailable rather than being filled with rest rotations.

`SkeletonConverter` is a scene component that subscribes to one `ISkeletonProvider`, converts incoming frames to a selected output skeleton, and republishes them as another `ISkeletonProvider`. This lets debug visualizers, rig drivers, or student scripts choose whether they want the raw comm-server skeleton, a COCO layout, or a Unity humanoid control pose.

`CommServer` publishes at most one pose event per Unity frame. If several transport packets arrive before `Update`, the smoother sees all of them and the latest smoothed pose is published.
