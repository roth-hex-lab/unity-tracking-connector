using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public static class BodyMath
    {
        /// 
        /// Computes the normalised surface normal of a triangle defined by three points.
        /// Used for estimating head orientation from facial landmark positions.
        /// 
        public static Vector3 GetNormal(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 u = p2 - p1;
            Vector3 v = p3 - p1;
            Vector3 n = new Vector3(
                (u.y * v.z - u.z * v.y),
                (u.z * v.x - u.x * v.z),
                (u.x * v.y - u.y * v.x)
            );
            return n.normalized;
        }
    }
}