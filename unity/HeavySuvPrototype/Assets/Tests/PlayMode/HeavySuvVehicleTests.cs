using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeavySuvPrototype.Tests
{
    public sealed class HeavySuvVehicleTests
    {
        [UnityTest]
        public IEnumerator StraightDriveMovesAlongLocalForward()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            yield return runner.Run(VehicleScenarioRunner.For(2.4f, VehicleScenarioRunner.Input(throttle: true)));

            VehicleTelemetrySample final = runner.Final;
            Assert.Greater(final.position.z, 5f);
            Assert.Less(Mathf.Abs(final.position.x), 1.25f);
            Assert.Less(Mathf.Abs(final.headingDegrees), 4f);
            Assert.Greater(final.signedSpeedMetersPerSecond, 4f);
        }

        [UnityTest]
        public IEnumerator SteeringSignsMatchWorldCoordinates()
        {
            VehicleScenarioRunner right = new VehicleScenarioRunner();
            yield return right.Create();
            yield return right.Run(
                VehicleScenarioRunner.For(1f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(2.2f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            VehicleScenarioRunner left = new VehicleScenarioRunner();
            yield return left.Create();
            yield return left.Run(
                VehicleScenarioRunner.For(1f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(2.2f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)));

            Assert.Greater(right.Final.position.x, 1.5f);
            Assert.Greater(right.Final.headingDegrees, 5f);
            Assert.Less(left.Final.position.x, -1.5f);
            Assert.Less(left.Final.headingDegrees, -5f);
            Assert.Greater(right.Final.position.x, left.Final.position.x + 5f);
        }

        [UnityTest]
        public IEnumerator SustainedSteeringDoesNotWrapIntoOppositeFacingSpin()
        {
            VehicleScenarioRunner right = new VehicleScenarioRunner();
            yield return right.Create();
            yield return right.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(3.1f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(0.4f, VehicleInputState.None));

            VehicleScenarioRunner left = new VehicleScenarioRunner();
            yield return left.Create();
            yield return left.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(3.1f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)),
                VehicleScenarioRunner.For(0.4f, VehicleInputState.None));

            Assert.Greater(right.Final.position.x, 4f);
            Assert.Greater(right.Final.headingDegrees, 4f);
            Assert.Less(right.Final.headingDegrees, 130f);
            Assert.Less(left.Final.position.x, -4f);
            Assert.Less(left.Final.headingDegrees, -4f);
            Assert.Greater(left.Final.headingDegrees, -130f);
        }

        [UnityTest]
        public IEnumerator BrakeThenReverseProducesNegativeSignedSpeed()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            yield return runner.Run(
                VehicleScenarioRunner.For(2.2f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(2.4f, VehicleScenarioRunner.Input(brake: true)),
                VehicleScenarioRunner.For(1.6f, VehicleScenarioRunner.Input(brake: true)));

            float maxForward = 0f;
            foreach (VehicleTelemetrySample sample in runner.Samples)
            {
                maxForward = Mathf.Max(maxForward, sample.signedSpeedMetersPerSecond);
            }

            Assert.Greater(maxForward, 4f);
            Assert.Less(runner.Final.signedSpeedMetersPerSecond, -1f);
        }

        [UnityTest]
        public IEnumerator StationarySettleStopsCreepAndBodyOscillation()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            yield return runner.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));

            float brakeTime = 0f;
            while (runner.Final.signedSpeedMetersPerSecond > 0.35f && brakeTime < 3f)
            {
                yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(brake: true));
                brakeTime += Time.fixedDeltaTime;
            }

            yield return runner.Run(VehicleScenarioRunner.For(3.2f, VehicleInputState.None));

            Assert.Less(Mathf.Abs(runner.Final.signedSpeedMetersPerSecond), 0.6f);
            Assert.Less(runner.Range(sample => sample.position.y, 1.4f), 0.08f);
            Assert.Less(runner.Range(sample => sample.pitchDegrees, 1.4f), 2.5f);
            Assert.Less(runner.Range(sample => sample.rollDegrees, 1.4f), 2.5f);
        }

        [UnityTest]
        public IEnumerator RwdProducesMoreRearSlipThanAwdUnderPowerWhileTurning()
        {
            VehicleScenarioRunner awd = new VehicleScenarioRunner();
            yield return awd.Create();
            awd.Vehicle.engineTorque = 12000f;
            awd.Vehicle.SetDriveMode(DriveMode.Awd);
            yield return awd.Run(
                VehicleScenarioRunner.For(1.2f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(2f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            VehicleScenarioRunner rwd = new VehicleScenarioRunner();
            yield return rwd.Create();
            rwd.Vehicle.engineTorque = 12000f;
            rwd.Vehicle.SetDriveMode(DriveMode.Rwd);
            yield return rwd.Run(
                VehicleScenarioRunner.For(1.2f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(2f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            float awdRearSlip = awd.AverageWheelValue(wheel => !wheel.isFront, wheel => Mathf.Abs(wheel.forwardSlip), 1.2f);
            float rwdRearSlip = rwd.AverageWheelValue(wheel => !wheel.isFront, wheel => Mathf.Abs(wheel.forwardSlip), 1.2f);
            Assert.Greater(rwdRearSlip, awdRearSlip + 0.05f);
        }

        [UnityTest]
        public IEnumerator WheelSizeUsesReferenceGearingScale()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            runner.Vehicle.SetDriveMode(DriveMode.Awd);

            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(throttle: true));

            float radius = runner.Vehicle.wheels[0].collider.radius;
            Assert.Less(radius, runner.Vehicle.drivetrainReferenceWheelRadius);

            float expectedTorque =
                runner.Vehicle.engineTorque *
                runner.Vehicle.firstGearTorqueMultiplier *
                (radius / runner.Vehicle.drivetrainReferenceWheelRadius) /
                runner.Vehicle.wheels.Length;
            foreach (HeavySuvVehicleController.Wheel wheel in runner.Vehicle.wheels)
            {
                Assert.AreEqual(expectedTorque, wheel.collider.motorTorque, 1f);
            }
        }

        [UnityTest]
        public IEnumerator ManualGearModesControlDriveDirection()
        {
            VehicleScenarioRunner neutral = new VehicleScenarioRunner();
            yield return neutral.Create();
            neutral.Vehicle.SetGearboxMode(GearboxMode.Neutral);
            yield return neutral.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Less(neutral.Final.position.z, 1f);

            VehicleScenarioRunner first = new VehicleScenarioRunner();
            yield return first.Create();
            first.Vehicle.SetGearboxMode(GearboxMode.First);
            yield return first.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Greater(first.Final.position.z, 3f);
            Assert.Greater(first.Final.signedSpeedMetersPerSecond, 2f);

            VehicleScenarioRunner reverse = new VehicleScenarioRunner();
            yield return reverse.Create();
            reverse.Vehicle.SetGearboxMode(GearboxMode.Reverse);
            yield return reverse.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Less(reverse.Final.position.z, -1f);
            Assert.Less(reverse.Final.signedSpeedMetersPerSecond, -1f);
        }

        [UnityTest]
        public IEnumerator BrakeLightsAndTireMarkSystemsArePresent()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();

            Assert.NotNull(runner.Vehicle.GetComponent<VehicleLights>());
            Assert.NotNull(runner.Vehicle.GetComponent<TireMarkController>());
            Assert.NotNull(runner.Vehicle.GetComponent<VehicleAudio>());
            Assert.NotNull(runner.Vehicle.transform.Find("Quaternius SUV Body"));
            Assert.AreEqual(12000f, runner.Vehicle.engineTorque);

            yield return runner.Run(VehicleScenarioRunner.For(0.9f, VehicleScenarioRunner.Input(throttle: true)));
            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(brake: true));
            Assert.IsTrue(runner.Vehicle.BrakeLightsActive);

            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(handbrake: true));
            Assert.IsTrue(runner.Vehicle.BrakeLightsActive);
            Assert.IsTrue(runner.Vehicle.HandbrakeActive);
        }
    }
}
