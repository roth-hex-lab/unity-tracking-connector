using System;

namespace HEXLab.Hextrackingconnector
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class SkeletonPipelineNodeAttribute : Attribute
    {
        public SkeletonPipelineNodeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
