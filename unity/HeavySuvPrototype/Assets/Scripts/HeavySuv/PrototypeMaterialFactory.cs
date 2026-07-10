using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeavySuvPrototype
{
    public static class PrototypeMaterialFactory
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        private static Texture2D asphaltTexture;

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

        public static Material CreateAsphalt()
        {
            Material material = CreateLit(Color.white);
            material.name = "Procedural Asphalt";
            Texture2D texture = GetAsphaltTexture();
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(45f, 45f);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", material.mainTextureScale);
            }
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.08f);
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

        private static Texture2D GetAsphaltTexture()
        {
            if (asphaltTexture != null)
            {
                return asphaltTexture;
            }

            const int size = 128;
            Color32[] pixels = new Color32[size * size];
            System.Random random = new System.Random(481516);
            for (int y = 0; y < size; y += 1)
            {
                for (int x = 0; x < size; x += 1)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float aggregate =
                        Mathf.Sin((u * 13f + v * 17f) * Mathf.PI * 2f) * 0.5f +
                        Mathf.Sin((u * 31f - v * 23f) * Mathf.PI * 2f) * 0.28f +
                        Mathf.Sin((u * 7f + v * 5f) * Mathf.PI * 2f) * 0.22f;
                    float speck = (float)random.NextDouble();
                    float value = 0.105f + aggregate * 0.022f;
                    if (speck > 0.985f)
                    {
                        value += 0.1f;
                    }
                    else if (speck < 0.018f)
                    {
                        value -= 0.045f;
                    }

                    byte red = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
                    byte green = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 0.98f * 255f), 0, 255);
                    byte blue = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 0.94f * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(red, green, blue, 255);
                }
            }

            asphaltTexture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Procedural Asphalt Aggregate",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            asphaltTexture.SetPixels32(pixels);
            asphaltTexture.Apply(true, true);
            return asphaltTexture;
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
