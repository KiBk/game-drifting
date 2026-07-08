using System;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VehicleAudio : MonoBehaviour
    {
        public HeavySuvVehicleController controller;
        public float masterVolume = 0.72f;

        private AudioSource engineSource;
        private AudioSource tireSource;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
        }

        private void Awake()
        {
            engineSource = GetComponent<AudioSource>();
            tireSource = gameObject.AddComponent<AudioSource>();

            ConfigureSource(engineSource, CreateEngineClip(), 0.08f);
            ConfigureSource(tireSource, CreateTireNoiseClip(), 0f);
        }

        private void OnEnable()
        {
            if (engineSource != null && !engineSource.isPlaying)
            {
                engineSource.Play();
            }

            if (tireSource != null && !tireSource.isPlaying)
            {
                tireSource.Play();
            }
        }

        private void Update()
        {
            if (controller == null || engineSource == null || tireSource == null)
            {
                return;
            }

            float throttle = controller.LastInput.throttle ? 1f : 0f;
            float wheelRpm = AverageDrivenWheelRpm();
            float engineRpm = EstimateEngineRpm(wheelRpm, throttle);
            float rpmRatio = Mathf.InverseLerp(700f, 6200f, engineRpm);
            float slip = ComputeSlipLevel();

            engineSource.pitch = Mathf.Lerp(0.72f, 2.25f, rpmRatio);
            engineSource.volume = masterVolume * (0.045f + rpmRatio * 0.075f + throttle * 0.08f);

            tireSource.pitch = Mathf.Lerp(0.82f, 1.85f, slip);
            tireSource.volume = masterVolume * Mathf.Pow(slip, 1.35f) * 0.34f;
        }

        private float EstimateEngineRpm(float wheelRpm, float throttle)
        {
            float ratio;
            switch (controller.ActiveGearLabel)
            {
                case "R":
                case "AR":
                    ratio = 10f;
                    break;
                case "2":
                case "A2":
                    ratio = 7f;
                    break;
                case "N":
                    ratio = 0f;
                    break;
                case "1":
                case "A1":
                default:
                    ratio = 12f;
                    break;
            }

            float connectedRpm = wheelRpm * ratio * AverageDrivenWheelRadiusScale();
            float freeRev = throttle * 2400f;
            return Mathf.Clamp(780f + Mathf.Max(connectedRpm, freeRev), 700f, 6200f);
        }

        private float AverageDrivenWheelRpm()
        {
            if (controller.wheels == null)
            {
                return 0f;
            }

            float sum = 0f;
            int count = 0;
            foreach (HeavySuvVehicleController.Wheel wheel in controller.wheels)
            {
                if (wheel?.collider == null)
                {
                    continue;
                }

                bool driven = controller.driveMode == DriveMode.Awd || !wheel.isFront;
                if (!driven)
                {
                    continue;
                }

                sum += Mathf.Abs(wheel.collider.rpm);
                count += 1;
            }

            return count > 0 ? sum / count : 0f;
        }

        private float AverageDrivenWheelRadiusScale()
        {
            if (controller.wheels == null)
            {
                return 1f;
            }

            float sum = 0f;
            int count = 0;
            foreach (HeavySuvVehicleController.Wheel wheel in controller.wheels)
            {
                if (wheel?.collider == null)
                {
                    continue;
                }

                bool driven = controller.driveMode == DriveMode.Awd || !wheel.isFront;
                if (!driven)
                {
                    continue;
                }

                sum += wheel.collider.radius;
                count += 1;
            }

            float averageRadius = count > 0 ? sum / count : controller.drivetrainReferenceWheelRadius;
            return averageRadius / Mathf.Max(controller.drivetrainReferenceWheelRadius, 0.001f);
        }

        private float ComputeSlipLevel()
        {
            if (controller.wheels == null)
            {
                return 0f;
            }

            float roadSpeed = Mathf.Abs(controller.SignedSpeedMetersPerSecond);
            float maximum = 0f;
            foreach (HeavySuvVehicleController.Wheel wheel in controller.wheels)
            {
                if (wheel?.collider == null || !wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    continue;
                }

                float wheelSurfaceSpeed =
                    Mathf.Abs(wheel.collider.rpm) * Mathf.PI * 2f * wheel.collider.radius / 60f;
                float mismatch =
                    Mathf.Abs(wheelSurfaceSpeed - roadSpeed) / Mathf.Max(roadSpeed, wheelSurfaceSpeed, 1.4f);
                float spinSlip =
                    SmoothStep(0.34f, 1.4f, Mathf.Abs(hit.forwardSlip)) *
                    SmoothStep(1.2f, 4.5f, Mathf.Max(roadSpeed, wheelSurfaceSpeed));
                float lateralSlip = SmoothStep(0.26f, 0.86f, Mathf.Abs(hit.sidewaysSlip));
                float speedSlip =
                    SmoothStep(0.2f, 0.72f, mismatch) *
                    SmoothStep(1.2f, 4.5f, Mathf.Max(roadSpeed, wheelSurfaceSpeed));
                float handbrakeSlip =
                    controller.HandbrakeActive && !wheel.isFront ? SmoothStep(1.1f, 3.8f, roadSpeed) : 0f;
                maximum = Mathf.Max(maximum, spinSlip, lateralSlip, speedSlip, handbrakeSlip);
            }

            return Mathf.Clamp01(maximum);
        }

        private static void ConfigureSource(AudioSource source, AudioClip clip, float volume)
        {
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
        }

        private static AudioClip CreateEngineClip()
        {
            const int sampleRate = 22050;
            const int samples = sampleRate;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i += 1)
            {
                float t = i / (float)sampleRate;
                float phase = (t * 82f) % 1f;
                float saw = phase * 2f - 1f;
                float harmonic = Mathf.Sin(t * 82f * Mathf.PI * 4f) * 0.32f;
                data[i] = Mathf.Clamp((saw * 0.46f + harmonic) * 0.32f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Engine Loop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTireNoiseClip()
        {
            const int sampleRate = 22050;
            const int samples = sampleRate;
            float[] data = new float[samples];
            System.Random random = new System.Random(19);
            float previous = 0f;
            for (int i = 0; i < samples; i += 1)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float highPassed = noise - previous * 0.72f;
                previous = noise;
                data[i] = Mathf.Clamp(highPassed * 0.28f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Tire Squeal Loop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(edge1 - edge0, 0.0001f));
            return t * t * (3f - 2f * t);
        }
    }
}
