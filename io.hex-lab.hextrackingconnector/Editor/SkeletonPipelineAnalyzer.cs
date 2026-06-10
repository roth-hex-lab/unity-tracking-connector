using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HEXLab.Hextrackingconnector.Editor
{
    internal static class SkeletonPipelineAnalyzer
    {
        private const int MaxDepth = 32;

        public static SkeletonPipelineAnalysis Analyze(
            MonoBehaviour provider,
            MonoBehaviour owner)
        {
            if (provider == null)
            {
                return SkeletonPipelineAnalysis.Empty;
            }

            var nodes = new List<SkeletonPipelineNode>();
            var warnings = new List<string>();
            var visitedInstanceIds = new HashSet<int>();
            var current = provider;

            for (var depth = 0; current != null && depth < MaxDepth; depth++)
            {
                var currentInstanceId = current.GetInstanceID();
                if (!visitedInstanceIds.Add(currentInstanceId))
                {
                    warnings.Add($"Warning: Pipeline chain loops back to {GetNodeName(current)}.");
                    break;
                }

                nodes.Add(new SkeletonPipelineNode(
                    GetNodeName(current),
                    GetGameObjectPath(current)));

                if (!TryGetUpstreamProvider(current, out var upstreamProvider))
                {
                    break;
                }

                current = upstreamProvider;
            }

            if (current != null && nodes.Count >= MaxDepth)
            {
                warnings.Add("Warning: Pipeline chain is too deep to display completely.");
            }

            nodes.Reverse();
            AppendOwnerNodeIfNeeded(nodes, visitedInstanceIds, owner);
            AddDuplicateWarnings(nodes, warnings);

            return new SkeletonPipelineAnalysis(
                nodes.Count == 0 ? null : "Flow:\n" + string.Join("\n-> ", nodes.Select(node => node.DisplayName)),
                warnings.ToArray());
        }

        private static void AppendOwnerNodeIfNeeded(
            ICollection<SkeletonPipelineNode> nodes,
            ISet<int> visitedInstanceIds,
            MonoBehaviour owner)
        {
            if (owner == null)
            {
                return;
            }

            var ownerInstanceId = owner.GetInstanceID();
            if (visitedInstanceIds.Contains(ownerInstanceId))
            {
                return;
            }

            nodes.Add(new SkeletonPipelineNode(
                GetNodeName(owner),
                GetGameObjectPath(owner)));
        }

        private static bool TryGetUpstreamProvider(
            MonoBehaviour component,
            out MonoBehaviour upstreamProvider)
        {
            upstreamProvider = null;
            if (component is SkeletonProviderSwitcher switcher)
            {
                if (!switcher.TryGetActiveProviderComponent(out var activeProvider) ||
                    !IsValidProviderField(
                        activeProvider,
                        component,
                        allowSelf: false))
                {
                    return false;
                }

                upstreamProvider = activeProvider;
                return true;
            }

            foreach (var field in GetProviderFields(component.GetType()))
            {
                var candidate = field.GetValue(component) as MonoBehaviour;
                if (candidate == null)
                {
                    continue;
                }

                var providerAttribute = (SkeletonProviderAttribute)Attribute.GetCustomAttribute(
                    field,
                    typeof(SkeletonProviderAttribute));
                if (!IsValidProviderField(
                        candidate,
                        component,
                        providerAttribute != null && providerAttribute.AllowSelf))
                {
                    continue;
                }

                upstreamProvider = candidate;
                return true;
            }

            return false;
        }

        private static bool IsValidProviderField(
            MonoBehaviour candidate,
            MonoBehaviour owner,
            bool allowSelf)
        {
            return SkeletonProviderUtility.IsValidProvider(candidate, owner, allowSelf);
        }

        private static IEnumerable<FieldInfo> GetProviderFields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (var currentType = type;
                 currentType != null && currentType != typeof(MonoBehaviour);
                 currentType = currentType.BaseType)
            {
                fields.AddRange(currentType
                    .GetFields(
                        BindingFlags.DeclaredOnly |
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Where(field =>
                        typeof(MonoBehaviour).IsAssignableFrom(field.FieldType) &&
                        Attribute.IsDefined(field, typeof(SkeletonProviderAttribute))));
            }

            return fields.OrderBy(field => field.DeclaringType == type ? 0 : 1)
                .ThenBy(field => field.MetadataToken);
        }

        private static string GetNodeName(MonoBehaviour component)
        {
            return GetNodeName(component.GetType());
        }

        private static string GetGameObjectPath(MonoBehaviour component)
        {
            if (component == null || component.gameObject == null)
            {
                return "Unknown GameObject";
            }

            var names = new Stack<string>();
            var current = component.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            var path = string.Join("/", names);
            var scene = component.gameObject.scene;
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name))
            {
                return scene.name + "/" + path;
            }

            return path;
        }

        private static string GetNodeName(Type type)
        {
            var nodeAttribute = (SkeletonPipelineNodeAttribute)Attribute.GetCustomAttribute(
                type,
                typeof(SkeletonPipelineNodeAttribute),
                inherit: true);

            if (nodeAttribute != null && !string.IsNullOrWhiteSpace(nodeAttribute.Name))
            {
                return nodeAttribute.Name;
            }

            return type.Name;
        }

        private static void AddDuplicateWarnings(
            IEnumerable<SkeletonPipelineNode> nodes,
            ICollection<string> warnings)
        {
            foreach (var duplicate in nodes
                         .GroupBy(node => node.Name)
                         .Where(group => group.Count() > 1))
            {
                warnings.Add(
                    $"Warning: {duplicate.Key} appears {duplicate.Count()} times in this chain: " +
                    string.Join(", ", duplicate.Select(node => node.GameObjectPath)));
            }
        }

        private readonly struct SkeletonPipelineNode
        {
            public SkeletonPipelineNode(string name, string gameObjectPath)
            {
                Name = name;
                GameObjectPath = gameObjectPath;
            }

            public string Name { get; }
            public string GameObjectPath { get; }
            public string DisplayName => Name + " @ " + GameObjectPath;
        }
    }

    internal sealed class SkeletonPipelineAnalysis
    {
        public static readonly SkeletonPipelineAnalysis Empty =
            new SkeletonPipelineAnalysis(null, new string[0]);

        public SkeletonPipelineAnalysis(string flowMessage, string[] warnings)
        {
            FlowMessage = flowMessage;
            Warnings = warnings ?? new string[0];
        }

        public string FlowMessage { get; }
        public string[] Warnings { get; }
        public bool HasFlow => !string.IsNullOrEmpty(FlowMessage);
        public bool HasWarnings => Warnings.Length > 0;
        public string WarningMessage => string.Join("\n", Warnings);
    }
}
