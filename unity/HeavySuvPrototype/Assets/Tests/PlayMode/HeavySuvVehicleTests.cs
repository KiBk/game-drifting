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
                VehicleScenarioRunner.For(0.8f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            VehicleScenarioRunner left = new VehicleScenarioRunner();
            yield return left.Create();
            yield return left.Run(
                VehicleScenarioRunner.For(1f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(0.8f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)));

            Assert.Greater(right.Final.position.x, 0.4f);
            Assert.Greater(right.Final.headingDegrees, 2f);
            Assert.Less(left.Final.position.x, -0.4f);
            Assert.Less(left.Final.headingDegrees, -2f);
            Assert.Greater(right.Final.position.x, left.Final.position.x + 1f);
        }

        [UnityTest]
        public IEnumerator SustainedSteeringRemainsUprightOnFlatGround()
        {
            VehicleScenarioRunner right = new VehicleScenarioRunner();
            yield return right.Create();
            yield return right.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(6.5f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(0.4f, VehicleInputState.None));

            VehicleScenarioRunner left = new VehicleScenarioRunner();
            yield return left.Create();
            yield return left.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(6.5f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)),
                VehicleScenarioRunner.For(0.4f, VehicleInputState.None));

            Assert.Greater(Vector3.Distance(right.Final.position, Vector3.zero), 4f);
            Assert.Greater(Vector3.Distance(left.Final.position, Vector3.zero), 4f);
            Assert.Greater(Vector3.Dot(left.Vehicle.transform.up, Vector3.up), 0.85f);
            Assert.Less(MaximumAbsoluteRoll(right), 32f);
            Assert.Less(MaximumAbsoluteRoll(left), 32f);
        }

        [UnityTest]
        public IEnumerator SlalomShowsWeightTransferWithoutRollover()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            yield return runner.Run(
                VehicleScenarioRunner.For(1f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(0.65f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(0.65f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)),
                VehicleScenarioRunner.For(0.65f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(0.65f, VehicleScenarioRunner.Input(throttle: true, steerLeft: true)),
                VehicleScenarioRunner.For(0.5f, VehicleInputState.None));

            Assert.Greater(MaximumAbsoluteRoll(runner), 1f);
            Assert.Less(MaximumAbsoluteRoll(runner), 45f);
            Assert.Greater(Vector3.Dot(runner.Vehicle.transform.up, Vector3.up), 0.7f);
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
        public IEnumerator SuspensionCompressesAndReboundsAfterVerticalImpulse()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create(1.2f);

            float settledCompression = AverageCompression(runner.Vehicle.CaptureTelemetry());
            runner.Vehicle.Body.AddForce(
                Vector3.down * runner.Vehicle.Body.mass * 1.1f,
                ForceMode.Impulse);
            yield return runner.Run(VehicleScenarioRunner.For(0.45f, VehicleInputState.None));

            float peakCompression = MaximumAverageCompression(runner);
            yield return runner.Run(VehicleScenarioRunner.For(1.8f, VehicleInputState.None));
            float recoveredCompression = AverageCompression(runner.Final);

            Assert.Greater(peakCompression, settledCompression + 0.04f);
            Assert.AreEqual(settledCompression, recoveredCompression, 0.08f);
        }

        [UnityTest]
        public IEnumerator WheelCollidersUseRestoredReferenceSuspensionAndFriction()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();

            foreach (HeavySuvVehicleController.Wheel wheel in runner.Vehicle.wheels)
            {
                JointSpring spring = wheel.collider.suspensionSpring;
                Assert.AreEqual(0.36f, wheel.collider.suspensionDistance, 0.001f);
                Assert.AreEqual(35000f, spring.spring, 1f);
                Assert.AreEqual(4500f, spring.damper, 1f);
                Assert.AreEqual(0.5f, spring.targetPosition, 0.001f);
                Assert.AreEqual(0.32f, wheel.collider.forwardFriction.extremumSlip, 0.001f);
                Assert.AreEqual(0.24f, wheel.collider.sidewaysFriction.extremumSlip, 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator RwdRoutesAllDriveTorqueToRearWheels()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            runner.Vehicle.SetDriveMode(DriveMode.Rwd);
            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(throttle: true));

            float expectedRearTorque = runner.Vehicle.motorTorque / 2f;
            foreach (HeavySuvVehicleController.Wheel wheel in runner.Vehicle.wheels)
            {
                Assert.AreEqual(wheel.isFront ? 0f : expectedRearTorque, wheel.collider.motorTorque, 1f);
            }
        }

        [UnityTest]
        public IEnumerator RwdPowerProducesMoreRotationAndRearSlipThanAwd()
        {
            VehicleScenarioRunner awd = new VehicleScenarioRunner();
            yield return awd.Create();
            awd.Vehicle.SetDriveMode(DriveMode.Awd);
            awd.Vehicle.countersteerAssistEnabled = false;
            awd.Vehicle.Body.linearVelocity = awd.Vehicle.transform.forward * 12f;
            yield return awd.Run(
                VehicleScenarioRunner.For(1.25f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            float awdSlipAngle = VehicleSlipAngleDegrees(awd.Vehicle);
            float awdRearSlip = awd.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.forwardSlip),
                0.6f);
            float awdRearLateralSlip = awd.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.sidewaysSlip),
                0.6f);

            VehicleScenarioRunner rwd = new VehicleScenarioRunner();
            yield return rwd.Create();
            rwd.Vehicle.SetDriveMode(DriveMode.Rwd);
            rwd.Vehicle.countersteerAssistEnabled = false;
            rwd.Vehicle.Body.linearVelocity = rwd.Vehicle.transform.forward * 12f;
            yield return rwd.Run(
                VehicleScenarioRunner.For(1.25f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            float rwdSlipAngle = VehicleSlipAngleDegrees(rwd.Vehicle);
            float rwdRearSlip = rwd.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.forwardSlip),
                0.6f);
            float rwdRearLateralSlip = rwd.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.sidewaysSlip),
                0.6f);

            Assert.Greater(
                rwdRearLateralSlip,
                awdRearLateralSlip + 0.05f,
                $"AWD rear forward/lateral {awdRearSlip:0.00}/{awdRearLateralSlip:0.00}; " +
                $"RWD {rwdRearSlip:0.00}/{rwdRearLateralSlip:0.00}; slip angles {awdSlipAngle:0.0}/{rwdSlipAngle:0.0}");
            Assert.Greater(
                rwdSlipAngle,
                awdSlipAngle + 4f,
                $"AWD/RWD slip angles {awdSlipAngle:0.0}/{rwdSlipAngle:0.0}");
            Assert.Less(rwdSlipAngle, 75f);
            Assert.AreEqual(1f, rwd.Vehicle.TractionDelivery, 0.001f);
            Assert.Less(MaximumAbsoluteRoll(rwd), 45f);

            yield return rwd.Run(VehicleScenarioRunner.For(0.5f, VehicleInputState.None));
            foreach (HeavySuvVehicleController.Wheel wheel in rwd.Vehicle.wheels)
            {
                if (!wheel.isFront)
                {
                    Assert.AreEqual(
                        rwd.Vehicle.rearSidewaysStiffness,
                        wheel.collider.sidewaysFriction.stiffness,
                        0.001f);
                }
            }
        }

        [UnityTest]
        public IEnumerator CountersteerAssistLimitsRwdSpinWhilePreservingDrift()
        {
            VehicleScenarioRunner unassisted = new VehicleScenarioRunner();
            yield return unassisted.Create();
            unassisted.Vehicle.SetDriveMode(DriveMode.Rwd);
            unassisted.Vehicle.countersteerAssistEnabled = false;
            unassisted.Vehicle.Body.linearVelocity = unassisted.Vehicle.transform.forward * 12f;
            yield return unassisted.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(1.2f, VehicleScenarioRunner.Input(throttle: true)));
            float unassistedMaximumSlip = MaximumAbsoluteSlipAngle(unassisted);
            float unassistedFinalSlip = Mathf.Abs(unassisted.Final.slipAngleDegrees);

            VehicleScenarioRunner assisted = new VehicleScenarioRunner();
            yield return assisted.Create();
            assisted.Vehicle.SetDriveMode(DriveMode.Rwd);
            assisted.Vehicle.countersteerAssistEnabled = true;
            assisted.Vehicle.Body.linearVelocity = assisted.Vehicle.transform.forward * 12f;
            yield return assisted.Run(
                VehicleScenarioRunner.For(0.6f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)),
                VehicleScenarioRunner.For(1.2f, VehicleScenarioRunner.Input(throttle: true)));

            float assistedMaximumSlip = MaximumAbsoluteSlipAngle(assisted);
            float assistedFinalSlip = Mathf.Abs(assisted.Final.slipAngleDegrees);
            float maximumAssist = MaximumAbsoluteCountersteer(assisted);

            Assert.Greater(maximumAssist, 0.15f);
            Assert.Greater(assistedMaximumSlip, 5f);
            Assert.Less(
                assistedMaximumSlip,
                unassistedMaximumSlip - 5f,
                $"maximum slip raw/assist {unassistedMaximumSlip:0.0}/{assistedMaximumSlip:0.0}; " +
                $"final {unassistedFinalSlip:0.0}/{assistedFinalSlip:0.0}; assist {maximumAssist:0.00}");
            Assert.Less(MaximumAbsoluteRoll(assisted), 45f);
        }

        [UnityTest]
        public IEnumerator RearBiasedAwdBuildsRearSlipAndRecoversOffThrottle()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            runner.Vehicle.SetDriveMode(DriveMode.Awd);
            yield return runner.Run(
                VehicleScenarioRunner.For(1.1f, VehicleScenarioRunner.Input(throttle: true)),
                VehicleScenarioRunner.For(1.5f, VehicleScenarioRunner.Input(throttle: true, steerRight: true)));

            float poweredRearSlip = runner.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.forwardSlip),
                0.8f);
            float poweredFrontSlip = runner.AverageWheelValue(
                wheel => wheel.isFront,
                wheel => Mathf.Abs(wheel.forwardSlip),
                0.8f);

            yield return runner.Run(VehicleScenarioRunner.For(2.2f, VehicleScenarioRunner.Input(steerRight: true)));
            float recoveredRearSlip = runner.AverageWheelValue(
                wheel => !wheel.isFront,
                wheel => Mathf.Abs(wheel.forwardSlip),
                0.5f);

            Assert.Greater(poweredRearSlip, poweredFrontSlip);
            Assert.Less(recoveredRearSlip, poweredRearSlip);
            Assert.Less(MaximumAbsoluteRoll(runner), 45f);
        }

        [UnityTest]
        public IEnumerator HandbrakeBreaksRearGripAndAssistWaitsForRelease()
        {
            VehicleScenarioRunner normalTurn = new VehicleScenarioRunner();
            yield return normalTurn.Create();
            normalTurn.Vehicle.SetDriveMode(DriveMode.Rwd);
            normalTurn.Vehicle.countersteerAssistEnabled = false;
            normalTurn.Vehicle.Body.linearVelocity = normalTurn.Vehicle.transform.forward * 12f;
            yield return normalTurn.Run(
                VehicleScenarioRunner.For(0.55f, VehicleScenarioRunner.Input(steerRight: true)));
            float normalSlipAngle = Mathf.Abs(normalTurn.Final.slipAngleDegrees);

            VehicleScenarioRunner handbrakeTurn = new VehicleScenarioRunner();
            yield return handbrakeTurn.Create();
            handbrakeTurn.Vehicle.SetDriveMode(DriveMode.Rwd);
            handbrakeTurn.Vehicle.countersteerAssistEnabled = true;
            handbrakeTurn.Vehicle.Body.linearVelocity = handbrakeTurn.Vehicle.transform.forward * 12f;
            yield return handbrakeTurn.Run(
                VehicleScenarioRunner.For(0.55f, VehicleScenarioRunner.Input(steerRight: true, handbrake: true)));

            float handbrakeSlipAngle = Mathf.Abs(handbrakeTurn.Final.slipAngleDegrees);
            Assert.Greater(handbrakeSlipAngle, normalSlipAngle + 4f);
            Assert.AreEqual(0f, MaximumAbsoluteCountersteer(handbrakeTurn), 0.001f);
            foreach (HeavySuvVehicleController.Wheel wheel in handbrakeTurn.Vehicle.wheels)
            {
                if (!wheel.isFront)
                {
                    Assert.AreEqual(
                        handbrakeTurn.Vehicle.handbrakeRearSidewaysStiffness,
                        wheel.collider.sidewaysFriction.stiffness,
                        0.001f);
                    Assert.AreEqual(0f, wheel.collider.motorTorque, 0.001f);
                }
            }

            handbrakeTurn.Samples.Clear();
            yield return handbrakeTurn.Run(
                VehicleScenarioRunner.For(0.7f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Greater(MaximumAbsoluteCountersteer(handbrakeTurn), 0.1f);
            Assert.Less(MaximumAbsoluteRoll(handbrakeTurn), 45f);
        }

        [UnityTest]
        public IEnumerator WheelSizeUsesReferenceGearingScale()
        {
            VehicleScenarioRunner runner = new VehicleScenarioRunner();
            yield return runner.Create();
            runner.Vehicle.SetDriveMode(DriveMode.Awd);

            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(throttle: true));

            float radius = runner.Vehicle.wheels[0].collider.radius;
            Assert.AreEqual(runner.Vehicle.drivetrainReferenceWheelRadius, radius, 0.001f);

            float totalTorque = runner.Vehicle.motorTorque;
            foreach (HeavySuvVehicleController.Wheel wheel in runner.Vehicle.wheels)
            {
                float axleShare = wheel.isFront
                    ? runner.Vehicle.awdFrontTorqueShare
                    : 1f - runner.Vehicle.awdFrontTorqueShare;
                float expectedTorque = totalTorque * axleShare / 2f;
                Assert.AreEqual(expectedTorque, wheel.collider.motorTorque, 1f);
            }
        }

        [UnityTest]
        public IEnumerator DriveSelectorModesControlDirection()
        {
            VehicleScenarioRunner neutral = new VehicleScenarioRunner();
            yield return neutral.Create();
            neutral.Vehicle.SetSelectorMode(DriveSelectorMode.Neutral);
            yield return neutral.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Less(neutral.Final.position.z, 1f);

            VehicleScenarioRunner drive = new VehicleScenarioRunner();
            yield return drive.Create();
            drive.Vehicle.SetSelectorMode(DriveSelectorMode.Drive);
            yield return drive.Run(VehicleScenarioRunner.For(1.8f, VehicleScenarioRunner.Input(throttle: true)));
            Assert.Greater(drive.Final.position.z, 3f);
            Assert.Greater(drive.Final.signedSpeedMetersPerSecond, 2f);

            VehicleScenarioRunner reverse = new VehicleScenarioRunner();
            yield return reverse.Create();
            reverse.Vehicle.SetSelectorMode(DriveSelectorMode.Reverse);
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
            VehicleAudio audio = runner.Vehicle.GetComponent<VehicleAudio>();
            Assert.NotNull(audio);
            Assert.IsTrue(audio.UsesExternalClips);
            Assert.AreEqual(5, runner.Vehicle.GetComponents<AudioSource>().Length);
            Assert.NotNull(runner.Vehicle.GetComponent<ConvoyTurboController>());
            Assert.NotNull(runner.Vehicle.transform.Find("Rally Hatch Lower Body"));
            Assert.AreEqual(6800f, runner.Vehicle.motorTorque);
            Assert.AreEqual(1550f, runner.Vehicle.Body.mass);
            Assert.AreEqual(-0.08f, runner.Vehicle.Body.centerOfMass.y, 0.001f);

            audio.SetEffectsVolume(0f);
            yield return null;
            foreach (AudioSource source in runner.Vehicle.GetComponents<AudioSource>())
            {
                Assert.AreEqual(0f, source.volume, 0.001f);
            }

            yield return runner.Run(VehicleScenarioRunner.For(0.9f, VehicleScenarioRunner.Input(throttle: true)));
            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(brake: true));
            Assert.IsTrue(runner.Vehicle.BrakeLightsActive);

            yield return runner.RunFor(Time.fixedDeltaTime, VehicleScenarioRunner.Input(handbrake: true));
            Assert.IsTrue(runner.Vehicle.BrakeLightsActive);
            Assert.IsTrue(runner.Vehicle.HandbrakeActive);
        }

        [Test]
        public void ShiftBoostIsAlwaysAvailableInPrototype()
        {
            ConvoyTurboController turbo = CreateTurboController();
            turbo.Step(0.3f, true, 0f);

            Assert.IsTrue(turbo.IsEligible);
            Assert.IsTrue(turbo.IsActive);
            Assert.AreEqual(1f, turbo.Charge01);
            Assert.Greater(turbo.TorqueMultiplier, 1.2f);

            Object.DestroyImmediate(turbo.gameObject);
        }

        [Test]
        public void TurboRequiresValidTrailingProgress()
        {
            ConvoyTurboController turbo = CreateTurboController();
            turbo.alwaysAvailable = false;
            turbo.SetGapState(new ConvoyGapState { valid = false, isTrailing = true, progressGapMeters = 30f });
            turbo.Step(10f, false, 0f);
            Assert.AreEqual(0f, turbo.Charge01);

            turbo.SetGapState(new ConvoyGapState { valid = true, isTrailing = false, progressGapMeters = 30f });
            turbo.Step(10f, false, 0f);
            Assert.AreEqual(0f, turbo.Charge01);

            Object.DestroyImmediate(turbo.gameObject);
        }

        [Test]
        public void TurboChargesConsumesAndFadesWhenRegrouped()
        {
            ConvoyTurboController turbo = CreateChargedTurboController();
            Assert.Greater(turbo.Charge01, 0.99f);

            turbo.Step(0.3f, true, 0f);
            Assert.IsTrue(turbo.IsActive);
            Assert.Greater(turbo.TorqueMultiplier, 1.2f);
            Assert.Less(turbo.Charge01, 1f);

            turbo.SetGapState(new ConvoyGapState { valid = true, isTrailing = true, progressGapMeters = 4f });
            turbo.Step(0.5f, true, 0f);
            Assert.IsFalse(turbo.IsActive);
            Assert.AreEqual(1f, turbo.TorqueMultiplier, 0.01f);

            Object.DestroyImmediate(turbo.gameObject);
        }

        [Test]
        public void TurboReducesDeliveryDuringSevereWheelspin()
        {
            ConvoyTurboController gripTurbo = CreateChargedTurboController();
            ConvoyTurboController slippingTurbo = CreateChargedTurboController();

            gripTurbo.Step(0.28f, true, 0.1f);
            slippingTurbo.Step(0.28f, true, 1.5f);

            Assert.Greater(gripTurbo.TorqueMultiplier, slippingTurbo.TorqueMultiplier + 0.2f);
            Assert.Less(slippingTurbo.SlipDelivery, gripTurbo.SlipDelivery);

            Object.DestroyImmediate(gripTurbo.gameObject);
            Object.DestroyImmediate(slippingTurbo.gameObject);
        }

        private static ConvoyTurboController CreateChargedTurboController()
        {
            ConvoyTurboController turbo = CreateTurboController();
            turbo.alwaysAvailable = false;
            turbo.SetGapState(new ConvoyGapState { valid = true, isTrailing = true, progressGapMeters = 30f });
            turbo.Step(5.1f, false, 0f);
            return turbo;
        }

        private static ConvoyTurboController CreateTurboController()
        {
            return new GameObject("Turbo Test").AddComponent<ConvoyTurboController>();
        }

        private static float MaximumAbsoluteRoll(VehicleScenarioRunner runner)
        {
            float maximum = 0f;
            foreach (VehicleTelemetrySample sample in runner.Samples)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(sample.rollDegrees));
            }

            return maximum;
        }

        private static float MaximumAverageCompression(VehicleScenarioRunner runner)
        {
            float maximum = 0f;
            foreach (VehicleTelemetrySample sample in runner.Samples)
            {
                maximum = Mathf.Max(maximum, AverageCompression(sample));
            }

            return maximum;
        }

        private static float AverageCompression(VehicleTelemetrySample sample)
        {
            float total = 0f;
            int grounded = 0;
            foreach (WheelTelemetry wheel in sample.wheels)
            {
                if (!wheel.grounded)
                {
                    continue;
                }

                total += wheel.suspensionCompression;
                grounded += 1;
            }

            return grounded > 0 ? total / grounded : 0f;
        }

        private static float VehicleSlipAngleDegrees(HeavySuvVehicleController vehicle)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(vehicle.Body.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude < 0.01f)
            {
                return 0f;
            }

            return Mathf.Abs(Vector3.SignedAngle(vehicle.transform.forward, planarVelocity, Vector3.up));
        }

        private static float MaximumAbsoluteSlipAngle(VehicleScenarioRunner runner)
        {
            float maximum = 0f;
            foreach (VehicleTelemetrySample sample in runner.Samples)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(sample.slipAngleDegrees));
            }

            return maximum;
        }

        private static float MaximumAbsoluteCountersteer(VehicleScenarioRunner runner)
        {
            float maximum = 0f;
            foreach (VehicleTelemetrySample sample in runner.Samples)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(sample.countersteerAssistInput));
            }

            return maximum;
        }
    }
}
