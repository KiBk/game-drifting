using System;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class VehicleAudio : MonoBehaviour
    {
        public HeavySuvVehicleController controller;
        [Range(0f, 1f)] public float masterVolume = 0.78f;
        [Range(0f, 1f)] public float tireSquealVolume = 0.28f;
        [Range(0f, 1f)] public float tireLockVolume = 0.32f;
        [Range(0f, 1f)] public float asphaltScrubVolume = 0.24f;

        private static AudioClip sharedAsphaltScrubClip;
        private AudioSource motorLowSource;
        private AudioSource motorHighSource;
        private AudioSource rollingSource;
        private AudioSource spinSource;
        private AudioSource lockSource;
        private AudioSource asphaltScrubSource;

        public bool UsesExternalClips { get; private set; }
        public bool UsesAsphaltScrub { get; private set; }
        public float RollingLevel { get; private set; }
        public float SpinLevel { get; private set; }
        public float LockLevel { get; private set; }
        public float AsphaltScrubLevel { get; private set; }
        public float EffectsVolume => masterVolume;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
        }

        public void SetEffectsVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        private void Awake()
        {
            motorLowSource = GetComponent<AudioSource>();
            motorHighSource = gameObject.AddComponent<AudioSource>();
            rollingSource = gameObject.AddComponent<AudioSource>();
            spinSource = gameObject.AddComponent<AudioSource>();
            lockSource = gameObject.AddComponent<AudioSource>();
            asphaltScrubSource = gameObject.AddComponent<AudioSource>();

            AudioClip motorLow = Resources.Load<AudioClip>("Audio/ev_motor_low");
            AudioClip motorHigh = Resources.Load<AudioClip>("Audio/ev_motor_high");
            AudioClip rolling = Resources.Load<AudioClip>("Audio/tire_rolling");
            AudioClip squeal = Resources.Load<AudioClip>("Audio/tire_squeal");
            sharedAsphaltScrubClip ??= CreateAsphaltScrubClip();
            UsesExternalClips = motorLow != null && motorHigh != null && rolling != null && squeal != null;
            UsesAsphaltScrub = sharedAsphaltScrubClip != null;

            ConfigureSource(motorLowSource, motorLow ?? CreateToneClip("EV Motor Low Fallback", 110f), 0.02f);
            ConfigureSource(motorHighSource, motorHigh ?? CreateToneClip("EV Motor High Fallback", 310f), 0f);
            ConfigureSource(rollingSource, rolling ?? CreateNoiseClip("Tire Rolling Fallback", 0.18f), 0f);
            ConfigureSource(spinSource, squeal ?? CreateNoiseClip("Tire Spin Fallback", 0.4f), 0f);
            ConfigureSource(lockSource, squeal ?? CreateNoiseClip("Tire Lock Fallback", 0.4f), 0f);
            ConfigureSource(asphaltScrubSource, sharedAsphaltScrubClip, 0f);
        }

        private void OnEnable()
        {
            Play(motorLowSource);
            Play(motorHighSource);
            Play(rollingSource);
            Play(spinSource);
            Play(lockSource);
            Play(asphaltScrubSource);
        }

        private void Update()
        {
            if (controller == null || motorLowSource == null)
            {
                return;
            }

            float speedKmh = Mathf.Abs(controller.SignedSpeedMetersPerSecond) * 3.6f;
            float speed01 = Mathf.InverseLerp(0f, controller.motorMaximumSpeedKmh, speedKmh);
            float throttle = controller.LastInput.throttle ? 1f : 0f;
            float boost = controller.ConvoyTurbo != null
                ? Mathf.InverseLerp(1f, controller.ConvoyTurbo.maximumTorqueMultiplier, controller.ConvoyTurbo.TorqueMultiplier)
                : 0f;
            bool neutral = controller.selectorMode == DriveSelectorMode.Neutral;

            float groundedRatio;
            float drivenSpin;
            float lateralSlip;
            float lockedSlip;
            MeasureTires(speedKmh, out groundedRatio, out drivenSpin, out lateralSlip, out lockedSlip);

            float loadedMotor = neutral ? 0f : throttle;
            motorLowSource.pitch = Mathf.Lerp(0.72f, 1.48f, speed01) + boost * 0.12f;
            motorLowSource.volume = masterVolume * (0.025f + speed01 * 0.055f + loadedMotor * 0.1f + boost * 0.06f);

            motorHighSource.pitch = Mathf.Lerp(0.58f, 2.05f, speed01) + boost * 0.18f;
            motorHighSource.volume = masterVolume * Mathf.Clamp01(
                Mathf.InverseLerp(8f, 95f, speedKmh) * 0.1f + loadedMotor * speed01 * 0.13f + boost * 0.12f);

            RollingLevel = groundedRatio * Mathf.InverseLerp(4f, 115f, speedKmh);
            rollingSource.pitch = Mathf.Lerp(0.72f, 1.45f, Mathf.InverseLerp(0f, 150f, speedKmh));
            rollingSource.volume = masterVolume * RollingLevel * 0.28f;

            SpinLevel = Mathf.Clamp01(Mathf.Max(drivenSpin, lateralSlip * 0.82f));
            spinSource.pitch = Mathf.Lerp(0.78f, 1.38f, SpinLevel) + speed01 * 0.08f;
            spinSource.volume = masterVolume * Mathf.Pow(SpinLevel, 1.25f) * tireSquealVolume;

            AsphaltScrubLevel = groundedRatio *
                                 Mathf.Clamp01(Mathf.Max(lateralSlip, drivenSpin * 0.55f)) *
                                 Mathf.InverseLerp(5f, 38f, speedKmh);
            asphaltScrubSource.pitch = Mathf.Lerp(
                0.78f,
                1.34f,
                Mathf.Clamp01(speed01 * 0.65f + AsphaltScrubLevel * 0.35f));
            asphaltScrubSource.volume = masterVolume *
                                         Mathf.Pow(AsphaltScrubLevel, 0.9f) *
                                         asphaltScrubVolume;

            LockLevel = Mathf.Clamp01(lockedSlip);
            lockSource.pitch = Mathf.Lerp(0.62f, 0.92f, Mathf.InverseLerp(8f, 90f, speedKmh));
            lockSource.volume = masterVolume * Mathf.Pow(LockLevel, 1.15f) * tireLockVolume;
        }

        private void MeasureTires(
            float speedKmh,
            out float groundedRatio,
            out float drivenSpin,
            out float lateralSlip,
            out float lockedSlip)
        {
            int wheelCount = 0;
            int groundedCount = 0;
            drivenSpin = 0f;
            lateralSlip = 0f;
            lockedSlip = 0f;

            if (controller.wheels != null)
            {
                foreach (HeavySuvVehicleController.Wheel wheel in controller.wheels)
                {
                    if (wheel?.collider == null)
                    {
                        continue;
                    }

                    wheelCount += 1;
                    if (!wheel.collider.GetGroundHit(out WheelHit hit))
                    {
                        continue;
                    }

                    groundedCount += 1;
                    bool driven = controller.driveMode == DriveMode.Awd || !wheel.isFront;
                    if (driven)
                    {
                        drivenSpin = Mathf.Max(drivenSpin, Mathf.InverseLerp(0.22f, 1.15f, Mathf.Abs(hit.forwardSlip)));
                    }

                    lateralSlip = Mathf.Max(lateralSlip, Mathf.InverseLerp(0.2f, 0.85f, Mathf.Abs(hit.sidewaysSlip)));
                    bool brakeApplied = wheel.collider.brakeTorque > 100f;
                    bool nearlyStoppedWheel = Mathf.Abs(wheel.collider.rpm) < 18f;
                    if (brakeApplied && nearlyStoppedWheel)
                    {
                        lockedSlip = Mathf.Max(lockedSlip, Mathf.InverseLerp(9f, 45f, speedKmh));
                    }
                }
            }

            groundedRatio = wheelCount > 0 ? (float)groundedCount / wheelCount : 0f;
        }

        private static void ConfigureSource(AudioSource source, AudioClip clip, float volume)
        {
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = volume;
        }

        private static void Play(AudioSource source)
        {
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        private static AudioClip CreateToneClip(string name, float frequency)
        {
            const int sampleRate = 22050;
            float[] samples = new float[sampleRate];
            for (int index = 0; index < samples.Length; index += 1)
            {
                float phase = index / (float)sampleRate;
                samples[index] = Mathf.Sin(phase * frequency * Mathf.PI * 2f) * 0.18f;
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoiseClip(string name, float amplitude)
        {
            const int sampleRate = 22050;
            float[] samples = new float[sampleRate];
            System.Random random = new System.Random(name.GetHashCode());
            float filtered = 0f;
            for (int index = 0; index < samples.Length; index += 1)
            {
                filtered = Mathf.Lerp(filtered, (float)(random.NextDouble() * 2d - 1d), 0.12f);
                samples[index] = filtered * amplitude;
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateAsphaltScrubClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(19790527);
            for (int layer = 0; layer < 18; layer += 1)
            {
                int cycles = random.Next(45, 760);
                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                float amplitude = 1f / (1f + layer * 0.22f);
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex += 1)
                {
                    float loopPhase = sampleIndex / (float)sampleCount;
                    samples[sampleIndex] += Mathf.Sin(
                        loopPhase * cycles * Mathf.PI * 2f + phase) * amplitude;
                }
            }

            for (int grain = 0; grain < 96; grain += 1)
            {
                int center = random.Next(0, sampleCount);
                int width = random.Next(22, 140);
                float amplitude = Mathf.Lerp(0.3f, 1f, (float)random.NextDouble());
                for (int offset = -width; offset <= width; offset += 1)
                {
                    int sampleIndex = (center + offset + sampleCount) % sampleCount;
                    float envelope = 1f - Mathf.Abs(offset) / (float)width;
                    samples[sampleIndex] += envelope * envelope * amplitude *
                                            Mathf.Sin(offset * 0.73f);
                }
            }

            float peak = 0f;
            foreach (float sample in samples)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            float normalization = peak > 0f ? 0.32f / peak : 1f;
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += 1)
            {
                samples[sampleIndex] *= normalization;
            }

            AudioClip clip = AudioClip.Create(
                "Procedural Asphalt Tire Scrub",
                samples.Length,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
