using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class TireMarkController : MonoBehaviour
    {
        private sealed class Mark
        {
            public Transform marker;
            public TrailRenderer trail;
            public bool wasEmitting;
        }

        public HeavySuvVehicleController controller;
        public float minSpeedMetersPerSecond = 1.6f;
        public float forwardSlipThreshold = 0.55f;
        public float sidewaysSlipThreshold = 0.42f;
        public float wheelSpeedDeltaThreshold = 4.2f;
        public float trailLifetime = 9f;
        public float trailWidth = 0.18f;

        private Mark[] marks;
        private Material markMaterial;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
            EnsureMarks();
        }

        private void Start()
        {
            EnsureMarks();
        }

        private void LateUpdate()
        {
            if (controller == null || controller.wheels == null)
            {
                return;
            }

            EnsureMarks();
            Rigidbody body = controller.Body;
            for (int i = 0; i < controller.wheels.Length; i += 1)
            {
                HeavySuvVehicleController.Wheel wheel = controller.wheels[i];
                Mark mark = marks[i];
                if (wheel?.collider == null || mark?.trail == null)
                {
                    continue;
                }

                bool grounded = wheel.collider.GetGroundHit(out WheelHit hit);
                float speed = body != null ? body.GetPointVelocity(hit.point).magnitude : 0f;
                bool shouldEmit = grounded && speed > minSpeedMetersPerSecond && IsSlipping(wheel, hit, speed, body);
                if (grounded)
                {
                    mark.marker.position = hit.point + Vector3.up * 0.026f;
                    Vector3 forward = Vector3.ProjectOnPlane(wheel.collider.transform.forward, Vector3.up);
                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        forward = transform.forward;
                    }

                    mark.marker.rotation = Quaternion.LookRotation(
                        forward.normalized,
                        Vector3.up);
                }

                if (shouldEmit && !mark.wasEmitting)
                {
                    mark.trail.Clear();
                }

                mark.trail.emitting = shouldEmit;
                mark.wasEmitting = shouldEmit;
            }
        }

        private bool IsSlipping(
            HeavySuvVehicleController.Wheel wheel,
            WheelHit hit,
            float speed,
            Rigidbody body)
        {
            bool driven = controller.driveMode == DriveMode.Awd || !wheel.isFront;
            float wheelSurfaceSpeed = Mathf.Abs(wheel.collider.rpm) * Mathf.PI * 2f * wheel.collider.radius / 60f;
            float contactForwardSpeed = 0f;
            if (body != null)
            {
                contactForwardSpeed = Mathf.Abs(Vector3.Dot(
                    body.GetPointVelocity(hit.point),
                    wheel.collider.transform.forward));
            }

            bool longitudinalSlip = Mathf.Abs(hit.forwardSlip) > forwardSlipThreshold;
            bool lateralSlip = Mathf.Abs(hit.sidewaysSlip) > sidewaysSlipThreshold;
            bool wheelSpin = driven && Mathf.Abs(wheelSurfaceSpeed - contactForwardSpeed) > wheelSpeedDeltaThreshold;
            bool lockedBrake = wheel.collider.brakeTorque > 1000f && wheelSurfaceSpeed < 1.2f && speed > 3.2f;
            bool handbrake = controller.HandbrakeActive && !wheel.isFront;
            return longitudinalSlip || lateralSlip || wheelSpin || lockedBrake || handbrake;
        }

        private void EnsureMarks()
        {
            if (controller == null || controller.wheels == null)
            {
                return;
            }

            if (marks != null && marks.Length == controller.wheels.Length)
            {
                return;
            }

            marks = new Mark[controller.wheels.Length];
            markMaterial ??= CreateMarkMaterial();

            for (int i = 0; i < marks.Length; i += 1)
            {
                GameObject markerObject = new GameObject($"{controller.wheels[i]?.name ?? "Wheel"} Tire Mark Emitter");
                markerObject.transform.SetParent(transform, false);

                TrailRenderer trail = markerObject.AddComponent<TrailRenderer>();
                trail.alignment = LineAlignment.View;
                trail.autodestruct = false;
                trail.emitting = false;
                trail.minVertexDistance = 0.12f;
                trail.numCapVertices = 0;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.startColor = new Color(0.02f, 0.018f, 0.014f, 0.72f);
                trail.endColor = new Color(0.02f, 0.018f, 0.014f, 0f);
                trail.time = trailLifetime;
                trail.widthMultiplier = trailWidth;
                trail.material = markMaterial;

                marks[i] = new Mark
                {
                    marker = markerObject.transform,
                    trail = trail
                };
            }
        }

        private static Material CreateMarkMaterial()
        {
            return PrototypeMaterialFactory.CreateTransparentLit(
                new Color(0.02f, 0.018f, 0.014f, 0.72f));
        }
    }
}
