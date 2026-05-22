# HEX Tracking Connector

Unity runtime package for receiving MediaPipe body pose frames from the HEX Python tracker.

## Quick Start

1. Add the `Communication` prefab to the scene.
2. Choose the transport, coordinate source, mirror mode, and smoothing algorithm on the `CommServer` component.
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

For a simple line-art visualization, add the `DebugBody` prefab to the scene. Its `BodyDebugVis` component subscribes to the `CommServer` and owns its own drawing, calibration, scaling, and head visualization settings.

## Runtime Model

`PoseFrame` and `WireLandmarkData` are internal wire DTOs that match the Python JSON payload. User code should consume `SkeletonFrame`, which is the public Unity-facing pose model. The default published skeleton is `HumanPoseSkeleton33`; access named joints through properties such as `frame.Nose`, `frame.LeftWrist`, or `frame.TryGetJoint(SkeletonJoint.LeftWrist, out var wrist)`.

`CommServer` publishes at most one pose event per Unity frame. If several transport packets arrive before `Update`, the smoother sees all of them and the latest smoothed pose is published.
