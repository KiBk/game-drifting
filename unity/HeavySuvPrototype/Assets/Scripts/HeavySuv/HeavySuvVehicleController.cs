using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HeavySuvVehicleController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Wheel
        {
            public string name;
            public bool isFront;
            public bool isLeft;
            public WheelCollider collider;
            public Transform visual;
        }

        [Header("Input")]
        public bool useKeyboardInput = true;

        [Header("Mass")]
        public float massKg = 3250f;
        public Vector3 centerOfMass = new Vector3(0f, 0.32f, 0.08f);

        [Header("Drivetrain")]
        public DriveMode driveMode = DriveMode.Awd;
        public GearboxMode gearboxMode = GearboxMode.Auto;
        public float engineTorque = 12000f;
        public float reverseTorque = 3200f;
        public float brakeTorque = 3600f;
        public float handbrakeTorque = 7200f;
        public float idleWheelDamping = 0.5f;
        public float reverseStartSpeed = 0.18f;
        public float firstGearMaxKmh = 48f;
        public float secondGearMaxKmh = 105f;
        public float firstGearTorqueMultiplier = 1.12f;
        public float secondGearTorqueMultiplier = 0.72f;
        public float autoShiftUpKmh = 36f;
        public float autoShiftDownKmh = 24f;

        [Header("Steering")]
        public float maxSteerAngle = 21.5f;
        public float steerFadeStartKmh = 9f;
        public float steerFadeEndKmh = 48f;
        public float highSpeedSteerFactor = 0.18f;

        [Header("Wheels")]
        public Wheel[] wheels = new Wheel[4];

        private Rigidbody body;
        private VehicleInputState scriptedInput;
        private VehicleInputState lastInput;
        private bool previousDriveToggle;
        private int automaticGear = 1;

        public Rigidbody Body
        {
            get
            {
                EnsureInitialized();
                return body;
            }
        }

        public VehicleInputState LastInput => lastInput;
        public string ActiveGearLabel { get; private set; } = "A1";
        public bool BrakeLightsActive { get; private set; }
        public bool ReverseDriveActive { get; private set; }
        public bool HandbrakeActive { get; private set; }
        public float SignedSpeedMetersPerSecond => body == null ? 0f : Vector3.Dot(transform.forward, body.linearVelocity);

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body == null)
            {
                return;
            }

            body.mass = massKg;
            body.centerOfMass = centerOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.03f;
            body.angularDamping = 0.12f;

            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider != null)
                {
                    wheel.collider.wheelDampingRate = idleWheelDamping;
                }
            }
        }

        private void Update()
        {
            VehicleInputState input = GetInput();
            if (input.toggleDriveMode && !previousDriveToggle)
            {
                ToggleDriveMode();
            }

            previousDriveToggle = input.toggleDriveMode;
        }

        private void FixedUpdate()
        {
            EnsureInitialized();
            if (body == null)
            {
                return;
            }

            ApplyInput(GetInput());
            SyncWheelVisuals();
        }

        public void SetScriptedInput(VehicleInputState input)
        {
            scriptedInput = input;
        }

        public void SetDriveMode(DriveMode mode)
        {
            driveMode = mode;
        }

        public void SetGearboxMode(GearboxMode mode)
        {
            gearboxMode = mode;
        }

        public void ToggleDriveMode()
        {
            driveMode = driveMode == DriveMode.Awd ? DriveMode.Rwd : DriveMode.Awd;
        }

        public VehicleTelemetrySample CaptureTelemetry()
        {
            EnsureInitialized();
            if (body == null)
            {
                return default;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            WheelTelemetry[] wheelTelemetry = new WheelTelemetry[wheels.Length];
            for (int i = 0; i < wheels.Length; i += 1)
            {
                wheelTelemetry[i] = CaptureWheelTelemetry(wheels[i]);
            }

            Vector3 euler = NormalizeEuler(transform.eulerAngles);
            return new VehicleTelemetrySample
            {
                position = transform.position,
                signedSpeedMetersPerSecond = localVelocity.z,
                speedKmh = Mathf.Abs(localVelocity.z) * 3.6f,
                headingDegrees = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg,
                pitchDegrees = euler.x,
                rollDegrees = euler.z,
                driveMode = driveMode,
                gearboxMode = gearboxMode,
                activeGearLabel = ActiveGearLabel,
                engineTorque = engineTorque,
                wheels = wheelTelemetry
            };
        }

        private VehicleInputState GetInput()
        {
            if (!useKeyboardInput)
            {
                return scriptedInput;
            }

            return new VehicleInputState
            {
                throttle = Input.GetKey(KeyCode.UpArrow),
                brake = Input.GetKey(KeyCode.DownArrow),
                steerLeft = Input.GetKey(KeyCode.LeftArrow),
                steerRight = Input.GetKey(KeyCode.RightArrow),
                handbrake = Input.GetKey(KeyCode.Space),
                toggleDriveMode = Input.GetKey(KeyCode.D)
            };
        }

        private void ApplyInput(VehicleInputState input)
        {
            lastInput = input;
            float signedSpeed = Vector3.Dot(transform.forward, body.linearVelocity);
            float speedKmh = Mathf.Abs(signedSpeed) * 3.6f;
            float steerInput = (input.steerRight ? 1f : 0f) - (input.steerLeft ? 1f : 0f);
            float steeringFade = Mathf.Lerp(
                1f,
                highSpeedSteerFactor,
                Mathf.InverseLerp(steerFadeStartKmh, steerFadeEndKmh, speedKmh));
            float steering = steerInput * maxSteerAngle * steeringFade;

            DriveCommand drive = ComputeDriveCommand(input, signedSpeed, speedKmh);
            BrakeLightsActive = drive.serviceBraking || input.handbrake;
            ReverseDriveActive = drive.reverseDriving;
            HandbrakeActive = input.handbrake;
            ActiveGearLabel = drive.activeGearLabel;

            int drivenWheelCount = CountDrivenWheels();
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null)
                {
                    continue;
                }

                wheel.collider.steerAngle = wheel.isFront ? steering : 0f;
                wheel.collider.motorTorque =
                    IsDriven(wheel) && drivenWheelCount > 0 ? drive.motorTorque / drivenWheelCount : 0f;
                wheel.collider.brakeTorque = ComputeBrakeTorque(wheel, drive.serviceBraking, input.handbrake);
            }
        }

        private DriveCommand ComputeDriveCommand(VehicleInputState input, float signedSpeed, float speedKmh)
        {
            DriveCommand command = new DriveCommand
            {
                activeGearLabel = GearboxModeLabel(gearboxMode)
            };

            switch (gearboxMode)
            {
                case GearboxMode.Reverse:
                    command.serviceBraking = input.brake;
                    command.reverseDriving = input.throttle;
                    command.motorTorque = input.throttle ? -reverseTorque : 0f;
                    command.activeGearLabel = "R";
                    return command;

                case GearboxMode.Neutral:
                    command.serviceBraking = input.brake;
                    command.activeGearLabel = "N";
                    return command;

                case GearboxMode.First:
                    command.serviceBraking = input.brake;
                    command.motorTorque = input.throttle ? ComputeForwardGearTorque(1, speedKmh) : 0f;
                    command.activeGearLabel = "1";
                    return command;

                case GearboxMode.Second:
                    command.serviceBraking = input.brake;
                    command.motorTorque = input.throttle ? ComputeForwardGearTorque(2, speedKmh) : 0f;
                    command.activeGearLabel = "2";
                    return command;

                case GearboxMode.Auto:
                default:
                    if (speedKmh > autoShiftUpKmh)
                    {
                        automaticGear = 2;
                    }
                    else if (speedKmh < autoShiftDownKmh)
                    {
                        automaticGear = 1;
                    }

                    bool reversing = input.brake && signedSpeed < reverseStartSpeed;
                    command.serviceBraking = input.brake && !reversing;
                    command.reverseDriving = reversing;
                    if (input.throttle)
                    {
                        command.motorTorque = ComputeForwardGearTorque(automaticGear, speedKmh);
                        command.activeGearLabel = automaticGear == 1 ? "A1" : "A2";
                    }
                    else if (reversing)
                    {
                        command.motorTorque = -reverseTorque;
                        command.activeGearLabel = "AR";
                    }
                    else
                    {
                        command.activeGearLabel = automaticGear == 1 ? "A1" : "A2";
                    }

                    return command;
            }
        }

        private float ComputeForwardGearTorque(int gear, float speedKmh)
        {
            float maxSpeed = gear == 1 ? firstGearMaxKmh : secondGearMaxKmh;
            float multiplier = gear == 1 ? firstGearTorqueMultiplier : secondGearTorqueMultiplier;
            float fadeStart = maxSpeed * 0.82f;
            float limiter = speedKmh >= maxSpeed ? 0f : 1f - Mathf.InverseLerp(fadeStart, maxSpeed, speedKmh);
            return engineTorque * multiplier * Mathf.Clamp01(limiter);
        }

        private static string GearboxModeLabel(GearboxMode mode)
        {
            switch (mode)
            {
                case GearboxMode.Reverse:
                    return "R";
                case GearboxMode.Neutral:
                    return "N";
                case GearboxMode.First:
                    return "1";
                case GearboxMode.Second:
                    return "2";
                case GearboxMode.Auto:
                default:
                    return "A";
            }
        }

        private float ComputeBrakeTorque(Wheel wheel, bool serviceBraking, bool handbrake)
        {
            float torque = serviceBraking ? brakeTorque : 0f;
            if (handbrake && !wheel.isFront)
            {
                torque = Mathf.Max(torque, handbrakeTorque);
            }

            return torque;
        }

        private int CountDrivenWheels()
        {
            int count = 0;
            foreach (Wheel wheel in wheels)
            {
                if (wheel != null && IsDriven(wheel))
                {
                    count += 1;
                }
            }

            return count;
        }

        private bool IsDriven(Wheel wheel)
        {
            return driveMode == DriveMode.Awd || !wheel.isFront;
        }

        private void SyncWheelVisuals()
        {
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null || wheel.visual == null)
                {
                    continue;
                }

                wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheel.visual.SetPositionAndRotation(position, rotation);
            }
        }

        private WheelTelemetry CaptureWheelTelemetry(Wheel wheel)
        {
            WheelTelemetry telemetry = new WheelTelemetry
            {
                name = wheel?.name ?? string.Empty,
                isFront = wheel?.isFront ?? false,
                isLeft = wheel?.isLeft ?? false,
                driven = wheel != null && IsDriven(wheel)
            };

            if (wheel?.collider == null)
            {
                return telemetry;
            }

            telemetry.rpm = wheel.collider.rpm;
            telemetry.grounded = wheel.collider.GetGroundHit(out WheelHit hit);
            if (telemetry.grounded)
            {
                telemetry.forwardSlip = hit.forwardSlip;
                telemetry.sidewaysSlip = hit.sidewaysSlip;
                telemetry.contactPoint = hit.point;
                float distance = Vector3.Distance(wheel.collider.transform.position, hit.point);
                float suspensionLength = Mathf.Max(0f, distance - wheel.collider.radius);
                telemetry.suspensionCompression = Mathf.Clamp01(
                    1f - suspensionLength / Mathf.Max(wheel.collider.suspensionDistance, 0.001f));
            }

            return telemetry;
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private struct DriveCommand
        {
            public float motorTorque;
            public bool serviceBraking;
            public bool reverseDriving;
            public string activeGearLabel;
        }
    }
}
