using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class VehicleTelemetry : MonoBehaviour
    {
        [SerializeField] private HeavySuvVehicleController controller;

        public VehicleTelemetrySample Latest { get; private set; }

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
            Capture();
        }

        private void FixedUpdate()
        {
            Capture();
        }

        public VehicleTelemetrySample Capture()
        {
            if (controller == null)
            {
                Latest = default;
                return Latest;
            }

            Latest = controller.CaptureTelemetry();
            return Latest;
        }
    }
}
