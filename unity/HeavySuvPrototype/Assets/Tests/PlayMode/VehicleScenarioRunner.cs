using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeavySuvPrototype.Tests
{
    public sealed class VehicleScenarioRunner
    {
        public struct Segment
        {
            public float seconds;
            public VehicleInputState input;
        }

        public readonly List<VehicleTelemetrySample> Samples = new List<VehicleTelemetrySample>();
        public HeavySuvVehicleController Vehicle { get; private set; }
        public VehicleTelemetrySample Final => Samples.Count > 0 ? Samples[Samples.Count - 1] : default;

        public IEnumerator Create(float settleSeconds = 0.8f)
        {
            CleanupScene();
            Time.fixedDeltaTime = 1f / 60f;
            Vehicle = HeavySuvPrototypeFactory.CreatePrototype(includeCameraAndHud: false);
            Vehicle.useKeyboardInput = false;
            yield return RunFor(settleSeconds, VehicleInputState.None);
            Samples.Clear();
        }

        public IEnumerator Run(params Segment[] segments)
        {
            foreach (Segment segment in segments)
            {
                yield return RunFor(segment.seconds, segment.input);
            }
        }

        public IEnumerator RunFor(float seconds, VehicleInputState input)
        {
            int frames = Mathf.RoundToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < frames; i += 1)
            {
                Vehicle.SetScriptedInput(input);
                yield return new WaitForFixedUpdate();
                Samples.Add(Vehicle.CaptureTelemetry());
            }
        }

        public static Segment For(float seconds, VehicleInputState input)
        {
            return new Segment { seconds = seconds, input = input };
        }

        public static VehicleInputState Input(
            bool throttle = false,
            bool brake = false,
            bool steerLeft = false,
            bool steerRight = false,
            bool handbrake = false,
            bool turbo = false)
        {
            return new VehicleInputState
            {
                throttle = throttle,
                brake = brake,
                steerLeft = steerLeft,
                steerRight = steerRight,
                handbrake = handbrake,
                turbo = turbo
            };
        }

        public float AverageWheelValue(System.Func<WheelTelemetry, bool> filter, System.Func<WheelTelemetry, float> value, float lastSeconds)
        {
            float finalTime = Samples.Count * Time.fixedDeltaTime;
            float startTime = Mathf.Max(0f, finalTime - lastSeconds);
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < Samples.Count; i += 1)
            {
                float sampleTime = (i + 1) * Time.fixedDeltaTime;
                if (sampleTime < startTime)
                {
                    continue;
                }

                foreach (WheelTelemetry wheel in Samples[i].wheels)
                {
                    if (filter(wheel))
                    {
                        sum += value(wheel);
                        count += 1;
                    }
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        public float Range(System.Func<VehicleTelemetrySample, float> value, float lastSeconds)
        {
            float finalTime = Samples.Count * Time.fixedDeltaTime;
            float startTime = Mathf.Max(0f, finalTime - lastSeconds);
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < Samples.Count; i += 1)
            {
                float sampleTime = (i + 1) * Time.fixedDeltaTime;
                if (sampleTime < startTime)
                {
                    continue;
                }

                float current = value(Samples[i]);
                min = Mathf.Min(min, current);
                max = Mathf.Max(max, current);
            }

            return float.IsInfinity(min) ? 0f : max - min;
        }

        public static void CleanupScene()
        {
            Object[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (Object item in objects)
            {
                Object.DestroyImmediate(item);
            }
        }
    }
}
