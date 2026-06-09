using System;

namespace HEXLab.Hextrackingconnector
{
    public readonly struct SkeletonJointId : IEquatable<SkeletonJointId>
    {
        private readonly string name;

        public SkeletonJointId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A skeleton joint id needs a name.", nameof(name));
            }

            this.name = name;
        }

        public string Name => name ?? string.Empty;

        public bool Equals(SkeletonJointId other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SkeletonJointId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name;
        }

        public static bool operator ==(SkeletonJointId left, SkeletonJointId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SkeletonJointId left, SkeletonJointId right)
        {
            return !left.Equals(right);
        }
    }
}
