// Wire DTOs that mirror the Python messages.py data structures.
// PoseFrame is the top-level JSON object received from Python.
// Field names must exactly match the JSON keys produced by Python's PoseFrame.to_json().
//
// Expected JSON shape:
// {
//   "skeleton_id": "mediapipe.pose.33",
//   "free":     [{"index": 0, "x": 0.1, "y": 0.2, "z": 0.3}, ...],
//   "anchored": [{"index": 0, "x": 0.1, "y": 0.2, "z": 0.3}, ...]
// }

using System;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
#pragma warning disable 0649
    [Serializable]
    internal class WireLandmarkData
    {
        public int index;
        public float x;
        public float y;
        public float z;
        public bool has_rotation;
        public float qx;
        public float qy;
        public float qz;
        public float qw;
        public bool has_confidence;
        public float confidence;
        public string provenance;
        public string source;

        public Vector3 ToVector3() => new Vector3(x, y, z);

        public SkeletonJointPose ToJointPose()
        {
            var channels = SkeletonJointChannels.Position | SkeletonJointChannels.Confidence;
            var rotation = Quaternion.identity;
            if (has_rotation)
            {
                channels |= SkeletonJointChannels.Rotation;
                rotation = new Quaternion(qx, qy, qz, qw);
            }

            return new SkeletonJointPose(
                channels,
                ToVector3(),
                rotation,
                has_confidence ? confidence : 1f,
                ParseProvenance(provenance),
                source);
        }

        private static SkeletonDataProvenance ParseProvenance(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SkeletonDataProvenance.Direct;
            }

            if (string.Equals(value, "direct", StringComparison.OrdinalIgnoreCase))
            {
                return SkeletonDataProvenance.Direct;
            }

            if (string.Equals(value, "inferred", StringComparison.OrdinalIgnoreCase))
            {
                return SkeletonDataProvenance.Inferred;
            }

            if (string.Equals(value, "held", StringComparison.OrdinalIgnoreCase))
            {
                return SkeletonDataProvenance.Held;
            }

            if (string.Equals(value, "rest", StringComparison.OrdinalIgnoreCase))
            {
                return SkeletonDataProvenance.Rest;
            }

            return SkeletonDataProvenance.Unknown;
        }
    }

    [Serializable]
    internal class PoseFrame
    {
        // Optional. Older Python senders omit it and are treated as MediaPipe Pose 33 in Auto mode.
        public string skeleton_id;

        // FREE: real-world coordinates computed via PnP on the Python side
        public WireLandmarkData[] free;

        // ANCHORED: MediaPipe world coordinates, origin anchored to body centre
        public WireLandmarkData[] anchored;

        /// Returns the landmark array matching the requested mode.
        public WireLandmarkData[] GetLandmarks(PoseCoordinateSource coordinateSource)
            => coordinateSource == PoseCoordinateSource.Anchored ? anchored : free;
    }
#pragma warning restore 0649
}
