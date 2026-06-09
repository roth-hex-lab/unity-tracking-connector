using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXLab.Hextrackingconnector
{
    internal static class BodyDebugMaterials
    {
        private static readonly Dictionary<string, Material> JointMaterials =
            new Dictionary<string, Material>();

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

        public static Material GetOrCreateJointMaterial(Color color)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var pipelineTypeName = pipeline == null ? null : pipeline.GetType().FullName;
            return GetOrCreateJointMaterial(color, pipelineTypeName);
        }

        private static Material GetOrCreateJointMaterial(Color color, string pipelineAssetTypeName)
        {
            var shader = FindFirstAvailableShader(GetCandidateShaderNames(pipelineAssetTypeName));
            if (shader == null)
            {
                return null;
            }

            var key = shader.name + ":" + ColorUtility.ToHtmlStringRGBA(color);
            if (JointMaterials.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }

            material = new Material(shader)
            {
                name = "HEX Debug Joint " + ColorUtility.ToHtmlStringRGBA(color),
                hideFlags = HideFlags.DontSave,
            };
            ApplyColor(material, color);
            JointMaterials[key] = material;
            return material;
        }

        private static string[] GetCandidateShaderNames(string pipelineAssetTypeName)
        {
            if (IsUrp(pipelineAssetTypeName))
            {
                return new[]
                {
                    "Universal Render Pipeline/Simple Lit",
                    "Universal Render Pipeline/Lit",
                    "Unlit/Color",
                    "Sprites/Default",
                };
            }

            if (IsHdrp(pipelineAssetTypeName))
            {
                return new[]
                {
                    "HDRP/Lit",
                    "HDRP/Unlit",
                    "Unlit/Color",
                    "Sprites/Default",
                };
            }

            return new[]
            {
                "Standard",
                "Unlit/Color",
                "Sprites/Default",
            };
        }

        private static bool IsUrp(string pipelineAssetTypeName)
        {
            return !string.IsNullOrEmpty(pipelineAssetTypeName) &&
                   pipelineAssetTypeName.Contains("Universal");
        }

        private static bool IsHdrp(string pipelineAssetTypeName)
        {
            return !string.IsNullOrEmpty(pipelineAssetTypeName) &&
                   (pipelineAssetTypeName.Contains("HighDefinition") ||
                    pipelineAssetTypeName.Contains("HDRenderPipeline"));
        }

        private static Shader FindFirstAvailableShader(IEnumerable<string> shaderNames)
        {
            foreach (var shaderName in shaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }

        private static void ApplyColor(Material material, Color color, float metallic = 0.0f, float smoothness = 0.35f, float gloss = 0.35f)
        {
            if (material.HasProperty(BaseColor))
            {
                material.SetColor(BaseColor, color);
            }

            if (material.HasProperty(Color1))
            {
                material.SetColor(Color1, color);
            }

            if (material.HasProperty(Metallic))
            {
                material.SetFloat(Metallic, metallic);
            }

            if (material.HasProperty(Smoothness))
            {
                material.SetFloat(Smoothness, smoothness);
            }

            if (material.HasProperty(Glossiness))
            {
                material.SetFloat(Glossiness, gloss);
            }
        }
    }
}
