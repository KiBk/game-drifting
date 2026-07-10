using System;
using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class VehicleAppearanceController : MonoBehaviour
    {
        public const string AlternateModelResourcePath = "Vehicles/KenneyCarKit/hatchback-sports";
        public const string AlternatePaletteResourcePath = "Vehicles/KenneyCarKit/Textures/colormap";

        [SerializeField] private GameObject[] proceduralVisuals;
        [SerializeField] private VehicleBodyStyle bodyStyle = VehicleBodyStyle.RallyHatch;
        private GameObject alternateModelRoot;

        public VehicleBodyStyle BodyStyle => bodyStyle;
        public bool AlternateModelLoaded => alternateModelRoot != null;
        public bool HasAlternateModelAsset => Resources.Load<GameObject>(AlternateModelResourcePath) != null;

        public void Bind(GameObject[] rallyVisuals)
        {
            proceduralVisuals = rallyVisuals;
            ApplyBodyStyle();
        }

        public bool SetBodyStyle(VehicleBodyStyle style)
        {
            if (style == VehicleBodyStyle.KenneySport && !EnsureAlternateModel())
            {
                return false;
            }

            bodyStyle = style;
            ApplyBodyStyle();
            return true;
        }

        private void Awake()
        {
            ApplyBodyStyle();
        }

        private void ApplyBodyStyle()
        {
            bool showRallyBody = bodyStyle == VehicleBodyStyle.RallyHatch;
            if (proceduralVisuals != null)
            {
                foreach (GameObject visual in proceduralVisuals)
                {
                    if (visual != null)
                    {
                        visual.SetActive(showRallyBody);
                    }
                }
            }

            if (!showRallyBody && alternateModelRoot == null && !EnsureAlternateModel())
            {
                bodyStyle = VehicleBodyStyle.RallyHatch;
                showRallyBody = true;
            }

            if (alternateModelRoot != null)
            {
                alternateModelRoot.SetActive(!showRallyBody);
            }
        }

        private bool EnsureAlternateModel()
        {
            if (alternateModelRoot != null)
            {
                return true;
            }

            GameObject modelPrefab = Resources.Load<GameObject>(AlternateModelResourcePath);
            if (modelPrefab == null)
            {
                return false;
            }

            alternateModelRoot = Instantiate(modelPrefab, transform, false);
            alternateModelRoot.name = "Kenney Sport Body";
            alternateModelRoot.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            alternateModelRoot.transform.localRotation = Quaternion.identity;
            alternateModelRoot.transform.localScale = Vector3.one * 1.32f;

            foreach (Transform descendant in alternateModelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (descendant != alternateModelRoot.transform &&
                    descendant.name.StartsWith("wheel-", StringComparison.OrdinalIgnoreCase))
                {
                    descendant.gameObject.SetActive(false);
                }
            }

            Texture2D palette = Resources.Load<Texture2D>(AlternatePaletteResourcePath);
            Material material = PrototypeMaterialFactory.CreateLit(Color.white);
            material.name = "Kenney Car Kit Palette";
            material.mainTexture = palette;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", palette);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.22f);
            }

            foreach (Renderer renderer in alternateModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex += 1)
                {
                    materials[materialIndex] = material;
                }
                renderer.sharedMaterials = materials;
            }

            foreach (Collider modelCollider in alternateModelRoot.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }

            alternateModelRoot.SetActive(false);
            return true;
        }
    }
}
