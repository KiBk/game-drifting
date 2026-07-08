using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class VehicleLights : MonoBehaviour
    {
        public HeavySuvVehicleController controller;
        public Renderer[] brakeRenderers = new Renderer[0];
        public Light[] brakeLights = new Light[0];
        public Color brakeOffColor = new Color(0.18f, 0.01f, 0.01f);
        public Color brakeOnColor = new Color(1f, 0.04f, 0.02f);

        private MaterialPropertyBlock propertyBlock;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
        }

        private void LateUpdate()
        {
            bool active = controller != null && controller.BrakeLightsActive;
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetColor("_Color", active ? brakeOnColor : brakeOffColor);
            propertyBlock.SetColor("_EmissionColor", active ? brakeOnColor * 2.4f : Color.black);

            foreach (Renderer brakeRenderer in brakeRenderers)
            {
                if (brakeRenderer != null)
                {
                    brakeRenderer.SetPropertyBlock(propertyBlock);
                }
            }

            foreach (Light brakeLight in brakeLights)
            {
                if (brakeLight != null)
                {
                    brakeLight.enabled = active;
                    brakeLight.intensity = active ? 1.8f : 0f;
                }
            }
        }
    }
}
