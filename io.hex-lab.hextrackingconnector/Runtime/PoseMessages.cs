// Mirrors the Python messages.py data structures.
// PoseFrame is the top-level JSON object received from Python.
// Field names must exactly match the JSON keys produced by Python's PoseFrame.to_json().
//
// Expected JSON shape:
// {
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

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    internal class PoseFrame
    {
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
