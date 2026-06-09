# HEX Tracking Connector

The HEX Tracking Connector receives body pose frames from an external Python tracker and exposes them inside Unity as `SkeletonFrame` data.

The package is built around `ISkeletonProvider`. Provider components can be connected in a scene so each block receives pose frames, processes them, and republishes the result.

Recommended chain:

```text
CommServer -> SkeletonSmoothing -> SkeletonConverter if needed -> BodyCalibration -> visualization or avatar control
```

## Installation

Install the package through Unity Package Manager with this Git URL:

```text
https://github.com/roth-hex-lab/unity-tracking-connector.git?path=/io.hex-lab.hextrackingconnector
```

## Main Components

| Component | Description |
|---|---|
| `CommServer` | Receives pose JSON over named pipe or UDP and maps wire landmarks into a Unity skeleton definition. |
| `SkeletonSmoothing` | Smooths incoming joint positions over a moving window. |
| `SkeletonConverter` | Converts frames to supported output definitions such as COCO 17 or Unity humanoid control. |
| `BodyCalibration` | Applies a shared additive calibration offset for centering and grounding. |
| `BodyDebugVis` | Draws a line-art skeleton for inspection. |
| `DirectHumanoidBoneDriver` | Drives a humanoid `Animator` from humanoid-control skeleton frames. |

## Samples

The `Samples/Demo` scene shows a typical setup:

- `Communication` prefab receives and smooths pose data.
- `Visuals` prefab converts, calibrates, and draws the skeleton.
- `RobotKyle` demonstrates humanoid avatar control.

Provider fields are assigned in the sample scene. When using the prefabs in a new scene, assign their source/provider fields to the pipeline stage you want them to consume.

For detailed usage, extension examples, and runtime model notes, see the package `README.md`.
