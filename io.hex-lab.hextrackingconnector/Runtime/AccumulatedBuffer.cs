using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    // ── AccumulatedBuffer ────────────────────────────────────────────────────────

    /// 
    /// Accumulates landmark positions across multiple pipe reads within a single Unity frame,
    /// so the displayed position is an average rather than the last-received value.
    /// 
    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;

        public AccumulatedBuffer(Vector3 v, int count)
        {
            value = v;
            accumulatedValuesCount = count;
        }
    }
}