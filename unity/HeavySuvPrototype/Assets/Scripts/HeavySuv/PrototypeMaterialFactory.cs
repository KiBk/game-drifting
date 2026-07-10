using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeavySuvPrototype
{
    public static class PrototypeMaterialFactory
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";

        public static Material CreateLit(Color color)
        {
            Material material = new Material(FindRequiredShader());
            ConfigureLit(material, color);
            return material;
        }

        public static Material CreateTransparentLit(Color color)
        {
            Material material = CreateLit(color);
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        public static void ConfigureLit(Material material, Color color)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            material.shader = FindRequiredShader();
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private static Shader FindRequiredShader()
        {
            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Required URP shader '{LitShaderName}' is unavailable. Verify the URP package and render pipeline settings.");
            }

            return shader;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
