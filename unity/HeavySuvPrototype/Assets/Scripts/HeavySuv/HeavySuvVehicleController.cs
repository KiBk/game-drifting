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
        public float massKg = 1550f;
        public Vector3 centerOfMass = new Vector3(0f, -0.08f, 0.06f);

        [Header("Drivetrain")]
        public DriveMode driveMode = DriveMode.Awd;
        public DriveSelectorMode selectorMode = DriveSelectorMode.Auto;
        public float motorTorque = 6800f;
        public float reverseMotorTorque = 3000f;
        public float brakeTorque = 3000f;
        public float handbrakeTorque = 5600f;
        public float idleWheelDamping = 0.42f;
        public float releasedThrottleWheelDamping = 18f;
        public float wheelSpinDampingStartMetersPerSecond = 0.8f;
        public float wheelSpinDampingEndMetersPerSecond = 8f;
        public float reverseStartSpeed = 0.18f;
        public float motorConstantTorqueEndKmh = 72f;
        public float motorMaximumSpeedKmh = 180f;
        [Range(0f, 1f)] public float motorTorqueAtMaximumSpeed = 0.18f;
        public float drivetrainReferenceWheelRadius = 0.34f;
        [Range(0f, 0.5f)] public float awdFrontTorqueShare = 0.4f;
        public float tractionSlipStart = 0.34f;
        public float tractionSlipEnd = 0.9f;
        [Range(0.2f, 1f)] public float minimumTractionDelivery = 0.4f;
        public float rearSidewaysStiffness = 0.98f;
        public float rwdPoweredRearSidewaysStiffness = 0.68f;
        public float handbrakeRearSidewaysStiffness = 0.35f;
        public float rwdGripReductionStartSlip = 0.08f;
        public float rwdGripReductionEndSlip = 0.55f;

        [Header("Braking")]
        public bool absEnabled = true;
        public float absMinimumSpeedKmh = 7f;
        public float absSlipStart = 0.18f;
        public float absSlipEnd = 0.55f;
        [Range(0f, 1f)] public float absMinimumBrakeDelivery = 0.18f;

        [Header("Steering")]
        public float maxSteerAngle = 23f;
        public float steerFadeStartKmh = 8f;
        public float steerFadeEndKmh = 72f;
        public float highSpeedSteerFactor = 0.22f;

        [Header("Respawn")]
        public float automaticRespawnDropMeters = 8f;

        [Header("Wheels")]
        public Wheel[] wheels = new Wheel[4];

        private Rigidbody body;
        private VehicleInputState scriptedInput;
        private VehicleInputState lastInput;
        private bool previousDriveToggle;
        private ConvoyTurboController convoyTurbo;
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;
        private bool respawnPoseSet;

        public Rigidbody Body
        {
            get
            {
                EnsureInitialized();
                return body;
            }
        }

        public VehicleInputState LastInput => lastInput;
        public string ActiveSelectorLabel { get; private set; } = "A";
        public bool BrakeLightsActive { get; private set; }
        public bool ReverseDriveActive { get; private set; }
        public bool HandbrakeActive { get; private set; }
        public float SignedSpeedMetersPerSecond => body == null ? 0f : Vector3.Dot(transform.forward, body.linearVelocity);
        public ConvoyTurboController ConvoyTurbo => convoyTurbo;
        public float LastDrivenWheelSlip { get; private set; }
        public float TractionDelivery { get; private set; } = 1f;
        public float VehicleSlipAngleDegrees { get; private set; }
        public bool AbsActive { get; private set; }

        private void Awake()
        {
            EnsureInitialized();
            SetRespawnPose(transform.position, transform.rotation);
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

            if (convoyTurbo == null)
            {
                convoyTurbo = GetComponent<ConvoyTurboController>();
            }

            body.mass = massKg;
            body.centerOfMass = centerOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.025f;
            body.angularDamping = 0.28f;
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

            if (ShouldAutomaticallyRespawn())
            {
                RespawnAtStart();
                SyncWheelVisuals();
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

        public void SetSelectorMode(DriveSelectorMode mode)
        {
            selectorMode = mode;
        }

        public void ToggleDriveMode()
        {
            driveMode = driveMode == DriveMode.Awd ? DriveMode.Rwd : DriveMode.Awd;
        }

        public void SetRespawnPose(Vector3 position, Quaternion rotation)
        {
            respawnPosition = position;
            respawnRotation = rotation;
            respawnPoseSet = true;
        }

        public void RespawnAtStart()
        {
            EnsureInitialized();
            if (body == null || !respawnPoseSet)
            {
                return;
            }

            body.position = respawnPosition;
            body.rotation = respawnRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            scriptedInput = VehicleInputState.None;
            lastInput = VehicleInputState.None;
            AbsActive = false;
            BrakeLightsActive = false;
            ReverseDriveActive = false;
            HandbrakeActive = false;
            LastDrivenWheelSlip = 0f;
            TractionDelivery = 1f;
            VehicleSlipAngleDegrees = 0f;
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null)
                {
                    continue;
                }

                wheel.collider.motorTorque = 0f;
                wheel.collider.brakeTorque = 0f;
                wheel.collider.steerAngle = 0f;
                wheel.collider.wheelDampingRate = releasedThrottleWheelDamping;
            }

            body.WakeUp();
            Physics.SyncTransforms();
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
                slipAngleDegrees = VehicleSlipAngleDegrees,
                pitchDegrees = euler.x,
                rollDegrees = euler.z,
                driveMode = driveMode,
                selectorMode = selectorMode,
                activeSelectorLabel = ActiveSelectorLabel,
                motorTorque = motorTorque,
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
                turbo = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                toggleDriveMode = Input.GetKey(KeyCode.D)
            };
        }

        private void ApplyInput(VehicleInputState input)
        {
            lastInput = input;
            float signedSpeed = Vector3.Dot(transform.forward, body.linearVelocity);
            float planarSpeedMetersPerSecond = Vector3.ProjectOnPlane(body.linearVelocity, transform.up).magnitude;
            float speedKmh = planarSpeedMetersPerSecond * 3.6f;
            float motorSpeedKmh = Mathf.Max(speedKmh, AverageDrivenWheelSurfaceSpeedKmh());
            float steerInput = (input.steerRight ? 1f : 0f) - (input.steerLeft ? 1f : 0f);
            VehicleSlipAngleDegrees = ComputeSignedSlipAngleDegrees();
            float steeringFade = Mathf.Lerp(
                1f,
                highSpeedSteerFactor,
                Mathf.InverseLerp(steerFadeStartKmh, steerFadeEndKmh, speedKmh));
            float steering = steerInput * maxSteerAngle * steeringFade;

            DriveCommand drive = ComputeDriveCommand(input, signedSpeed, planarSpeedMetersPerSecond, motorSpeedKmh);
            LastDrivenWheelSlip = AverageDrivenWheelSlip();
            TractionDelivery = ComputeTractionDelivery(LastDrivenWheelSlip);
            UpdateRearGrip(input.throttle, input.handbrake, LastDrivenWheelSlip);
            if (convoyTurbo != null)
            {
                convoyTurbo.Step(Time.fixedDeltaTime, input.turbo);
            }

            float turboMultiplier = convoyTurbo == null ? 1f : convoyTurbo.TorqueMultiplier;
            drive.motorTorque *= TractionDelivery * turboMultiplier;
            BrakeLightsActive = drive.serviceBraking || input.handbrake;
            ReverseDriveActive = drive.reverseDriving;
            HandbrakeActive = input.handbrake;
            ActiveSelectorLabel = drive.activeSelectorLabel;
            AbsActive = false;

            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null)
                {
                    continue;
                }

                wheel.collider.steerAngle = wheel.isFront ? steering : 0f;
                wheel.collider.wheelDampingRate = ComputeWheelDamping(
                    wheel,
                    input.throttle,
                    drive.serviceBraking || input.handbrake);
                float wheelGearingScale = GetWheelGearingScale(wheel);
                wheel.collider.motorTorque =
                    IsDriven(wheel) && !(input.handbrake && !wheel.isFront)
                        ? GetWheelTorque(wheel, drive.motorTorque) * wheelGearingScale
                        : 0f;
                wheel.collider.brakeTorque = ComputeBrakeTorque(
                    wheel,
                    drive.serviceBraking,
                    input.handbrake,
                    speedKmh);
            }
        }

        private float ComputeSignedSlipAngleDegrees()
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, transform.up);
            if (planarVelocity.sqrMagnitude < 0.25f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(transform.forward, planarVelocity.normalized, transform.up);
        }

        private DriveCommand ComputeDriveCommand(
            VehicleInputState input,
            float signedSpeed,
            float planarSpeedMetersPerSecond,
            float motorSpeedKmh)
        {
            DriveCommand command = new DriveCommand
            {
                activeSelectorLabel = SelectorModeLabel(selectorMode)
            };

            switch (selectorMode)
            {
                case DriveSelectorMode.Reverse:
                    command.serviceBraking = input.brake;
                    command.reverseDriving = input.throttle;
                    command.motorTorque = input.throttle
                        ? -ComputeElectricMotorTorque(motorSpeedKmh, reverseMotorTorque)
                        : 0f;
                    command.activeSelectorLabel = "R";
                    return command;

                case DriveSelectorMode.Neutral:
                    command.serviceBraking = input.brake;
                    command.activeSelectorLabel = "N";
                    return command;

                case DriveSelectorMode.Drive:
                    command.serviceBraking = input.brake;
                    command.motorTorque = input.throttle
                        ? ComputeElectricMotorTorque(motorSpeedKmh, motorTorque)
                        : 0f;
                    command.activeSelectorLabel = "D";
                    return command;

                case DriveSelectorMode.Auto:
                default:
                    bool beginReverse = planarSpeedMetersPerSecond < reverseStartSpeed;
                    bool continueReverse = ReverseDriveActive && signedSpeed < reverseStartSpeed;
                    bool reversing = input.brake && (beginReverse || continueReverse);
                    command.serviceBraking = input.brake && !reversing;
                    command.reverseDriving = reversing;
                    if (input.throttle)
                    {
                        command.motorTorque = ComputeElectricMotorTorque(motorSpeedKmh, motorTorque);
                    }
                    else if (reversing)
                    {
                        command.motorTorque = -ComputeElectricMotorTorque(motorSpeedKmh, reverseMotorTorque);
                    }

                    command.activeSelectorLabel = "A";
                    return command;
            }
        }

        private float ComputeElectricMotorTorque(float speedKmh, float peakTorque)
        {
            if (speedKmh >= motorMaximumSpeedKmh)
            {
                return 0f;
            }

            float highSpeedFade = Mathf.InverseLerp(
                motorConstantTorqueEndKmh,
                motorMaximumSpeedKmh,
                speedKmh);
            float torqueFactor = Mathf.Lerp(1f, motorTorqueAtMaximumSpeed, highSpeedFade);
            float limiter = 1f - Mathf.InverseLerp(
                motorMaximumSpeedKmh * 0.96f,
                motorMaximumSpeedKmh,
                speedKmh);
            return peakTorque * torqueFactor * Mathf.Clamp01(limiter);
        }

        private static string SelectorModeLabel(DriveSelectorMode mode)
        {
            switch (mode)
            {
                case DriveSelectorMode.Reverse:
                    return "R";
                case DriveSelectorMode.Neutral:
                    return "N";
                case DriveSelectorMode.Drive:
                    return "D";
                case DriveSelectorMode.Auto:
                default:
                    return "A";
            }
        }

        public float EvaluateAbsBrakeMultiplier(float speedKmh, float forwardSlip)
        {
            if (!absEnabled || speedKmh < absMinimumSpeedKmh)
            {
                return 1f;
            }

            float brakingSlip = Mathf.Max(0f, forwardSlip);
            float intervention = Mathf.InverseLerp(absSlipStart, absSlipEnd, brakingSlip);
            return Mathf.Lerp(1f, absMinimumBrakeDelivery, intervention);
        }

        private float ComputeBrakeTorque(Wheel wheel, bool serviceBraking, bool handbrake, float speedKmh)
        {
            float torque = serviceBraking ? brakeTorque : 0f;
            if (serviceBraking && wheel.collider.GetGroundHit(out WheelHit hit))
            {
                float absMultiplier = EvaluateAbsBrakeMultiplier(speedKmh, hit.forwardSlip);
                torque *= absMultiplier;
                AbsActive |= absMultiplier < 0.999f;
            }

            if (handbrake && !wheel.isFront)
            {
                torque = Mathf.Max(torque, handbrakeTorque);
            }

            return torque;
        }

        private float ComputeWheelDamping(Wheel wheel, bool throttle, bool braking)
        {
            if (throttle || braking || !IsDriven(wheel))
            {
                return idleWheelDamping;
            }

            float wheelSurfaceSpeed = Mathf.Abs(wheel.collider.rpm) *
                2f * Mathf.PI * wheel.collider.radius / 60f;
            float longitudinalRoadSpeed = Mathf.Abs(Vector3.Dot(
                body.GetPointVelocity(wheel.collider.transform.position),
                wheel.collider.transform.forward));
            float excessSpinSpeed = Mathf.Max(0f, wheelSurfaceSpeed - longitudinalRoadSpeed);
            float dampingFactor = Mathf.InverseLerp(
                wheelSpinDampingStartMetersPerSecond,
                Mathf.Max(wheelSpinDampingEndMetersPerSecond, wheelSpinDampingStartMetersPerSecond + 0.01f),
                excessSpinSpeed);
            return Mathf.Lerp(idleWheelDamping, releasedThrottleWheelDamping, dampingFactor);
        }

        private bool ShouldAutomaticallyRespawn()
        {
            return respawnPoseSet &&
                body.position.y < respawnPosition.y - Mathf.Max(automaticRespawnDropMeters, 0.1f);
        }

        private float GetWheelGearingScale(Wheel wheel)
        {
            float radius = wheel?.collider != null ? wheel.collider.radius : drivetrainReferenceWheelRadius;
            return radius / Mathf.Max(drivetrainReferenceWheelRadius, 0.001f);
        }

        private float GetWheelTorque(Wheel wheel, float totalTorque)
        {
            if (driveMode == DriveMode.Rwd)
            {
                int rearWheelCount = CountWheels(front: false);
                return wheel.isFront || rearWheelCount == 0 ? 0f : totalTorque / rearWheelCount;
            }

            int axleWheelCount = CountWheels(wheel.isFront);
            if (axleWheelCount == 0)
            {
                return 0f;
            }

            float axleShare = wheel.isFront ? awdFrontTorqueShare : 1f - awdFrontTorqueShare;
            return totalTorque * axleShare / axleWheelCount;
        }

        private int CountWheels(bool front)
        {
            int count = 0;
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider != null && wheel.isFront == front)
                {
                    count += 1;
                }
            }

            return count;
        }

        private float AverageDrivenWheelSlip()
        {
            float slip = 0f;
            int groundedWheelCount = 0;
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null || !IsDriven(wheel) || !wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    continue;
                }

                slip += Mathf.Sqrt(
                    hit.forwardSlip * hit.forwardSlip +
                    hit.sidewaysSlip * hit.sidewaysSlip);
                groundedWheelCount += 1;
            }

            return groundedWheelCount > 0 ? slip / groundedWheelCount : 0f;
        }

        private float AverageDrivenWheelSurfaceSpeedKmh()
        {
            float totalSpeed = 0f;
            int drivenWheelCount = 0;
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null || !IsDriven(wheel))
                {
                    continue;
                }

                totalSpeed += Mathf.Abs(wheel.collider.rpm) *
                    2f * Mathf.PI * wheel.collider.radius / 60f * 3.6f;
                drivenWheelCount += 1;
            }

            return drivenWheelCount > 0 ? totalSpeed / drivenWheelCount : 0f;
        }

        private float ComputeTractionDelivery(float drivenWheelSlip)
        {
            if (driveMode == DriveMode.Rwd)
            {
                return 1f;
            }

            float reduction = Mathf.InverseLerp(tractionSlipStart, tractionSlipEnd, Mathf.Abs(drivenWheelSlip));
            return Mathf.Lerp(1f, minimumTractionDelivery, reduction);
        }

        private void UpdateRearGrip(bool throttle, bool handbrake, float drivenWheelSlip)
        {
            float reduction = driveMode == DriveMode.Rwd && throttle
                ? Mathf.InverseLerp(rwdGripReductionStartSlip, rwdGripReductionEndSlip, drivenWheelSlip)
                : 0f;
            float targetStiffness = Mathf.Lerp(
                rearSidewaysStiffness,
                rwdPoweredRearSidewaysStiffness,
                reduction);
            if (handbrake)
            {
                targetStiffness = handbrakeRearSidewaysStiffness;
            }

            foreach (Wheel wheel in wheels)
            {
                if (wheel?.collider == null || wheel.isFront)
                {
                    continue;
                }

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness = targetStiffness;
                wheel.collider.sidewaysFriction = sideways;
            }
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
            public string activeSelectorLabel;
        }
    }
}
