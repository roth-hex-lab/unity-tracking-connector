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

    public static class SkeletonProviderUtility
    {
        public static bool IsValidProvider(
            MonoBehaviour component,
            MonoBehaviour owner = null,
            bool allowSelf = true)
        {
            return GetValidationMessage(component, owner, allowSelf) == null;
        }

        public static string GetValidationMessage(
            MonoBehaviour component,
            MonoBehaviour owner,
            bool allowSelf)
        {
            if (component == null)
            {
                return null;
            }

            if (!allowSelf && owner != null && ReferenceEquals(component, owner))
            {
                return "The assigned skeleton provider cannot reference itself.";
            }

            if (!(component is ISkeletonProvider))
            {
                return $"'{component.name}' does not implement {nameof(ISkeletonProvider)}.";
            }

            return null;
        }

        public static bool TryResolveProvider(
            MonoBehaviour component,
            MonoBehaviour owner,
            string fieldName,
            bool allowSelf,
            out ISkeletonProvider provider)
        {
            provider = null;
            if (component == null)
            {
                return false;
            }

            var message = GetValidationMessage(component, owner, allowSelf);
            if (message != null)
            {
                Debug.LogError(FormatValidationMessage(owner, fieldName, message), owner);
                return false;
            }

            provider = (ISkeletonProvider)component;
            return true;
        }

        private static string FormatValidationMessage(
            MonoBehaviour owner,
            string fieldName,
            string validationMessage)
        {
            var ownerName = owner == null ? "Skeleton provider field" : owner.name;
            return $"{ownerName}: {fieldName} is invalid. {validationMessage}";
        }
    }
}
