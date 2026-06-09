using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public sealed class SkeletonProviderAttribute : PropertyAttribute
    {
        public SkeletonProviderAttribute(bool allowSelf = true)
        {
            AllowSelf = allowSelf;
        }

        public bool AllowSelf { get; }
    }
}
