import RAPIER from "@dimforge/rapier3d-compat";
import { mkdir, writeFile } from "node:fs/promises";
import { beforeAll, describe, expect, it } from "vitest";
import { runScenario } from "./vehicleScenarios";
import { cloneVehicleConfig } from "./vehiclePhysics";

beforeAll(async () => {
  await RAPIER.init();
});

describe("vehicle physics trace", () => {
  it("writes a deterministic trace for tuning", async () => {
    const heavySoftConfig = cloneVehicleConfig();
    heavySoftConfig.mass = 3_000;
    heavySoftConfig.engineTorque = 2_650;
    heavySoftConfig.suspensionRestLength = 0.82;
    heavySoftConfig.suspensionBumpTravel = 0.22;
    heavySoftConfig.suspensionDroopTravel = 0.42;
    heavySoftConfig.springRate = 24_000;
    heavySoftConfig.frontAntiRoll = 0;
    heavySoftConfig.rearAntiRoll = 0;

    const powerOversteerConfig = cloneVehicleConfig();
    powerOversteerConfig.engineTorque = 3_250;
    powerOversteerConfig.differentialLock = 0.48;
    powerOversteerConfig.rearPowerOversteer = 0.72;
    powerOversteerConfig.longitudinalSlideGrip = 0.5;
    powerOversteerConfig.lateralSlideGrip = 0.5;

    const scenarios = {
      mixedDriving: runScenario(
        [
          { seconds: 2.2, input: { throttle: true } },
          { seconds: 2.2, input: { throttle: true, steerLeft: true } },
          { seconds: 1.2, input: { steerLeft: true, handbrake: true } },
          { seconds: 2.4, input: { brake: true } },
        ],
        { sampleEveryFrames: 6 },
      ),
      forwardReverse: runScenario(
        [
          { seconds: 2.4, input: { throttle: true } },
          { seconds: 3.6, input: { brake: true } },
        ],
        { sampleEveryFrames: 6 },
      ),
      brakeDive: runScenario(
        [
          { seconds: 2.2, input: { throttle: true } },
          { seconds: 1.4, input: { brake: true } },
        ],
        { sampleEveryFrames: 3 },
      ),
      coastingTurn: runScenario(
        [
          { seconds: 2.4, input: { throttle: true } },
          { seconds: 1.2, input: { steerLeft: true } },
        ],
        { sampleEveryFrames: 6 },
      ),
      serviceBrakeTurn: runScenario(
        [
          { seconds: 2.4, input: { throttle: true } },
          { seconds: 1.2, input: { steerLeft: true, brake: true } },
        ],
        { sampleEveryFrames: 6 },
      ),
      handbrakeTurn: runScenario(
        [
          { seconds: 2.4, input: { throttle: true } },
          { seconds: 1.2, input: { steerLeft: true, handbrake: true } },
        ],
        { sampleEveryFrames: 6 },
      ),
      heavySoftBumpStops: runScenario([{ seconds: 1.2, input: {} }], {
        config: heavySoftConfig,
        settleSeconds: 4,
        sampleEveryFrames: 3,
      }),
      powerOversteer: runScenario(
        [
          { seconds: 1.8, input: { throttle: true } },
          { seconds: 2, input: { throttle: true, steerLeft: true } },
        ],
        { config: powerOversteerConfig, sampleEveryFrames: 3 },
      ),
    };

    await mkdir("physics-traces", { recursive: true });
    await writeFile(
      "physics-traces/custom-vehicle-trace.json",
      JSON.stringify(
        Object.fromEntries(
          Object.entries(scenarios).map(([name, result]) => [
            name,
            result.samples.map((sample) => ({
              time: Number(sample.time.toFixed(3)),
              position: {
                x: Number(sample.position.x.toFixed(3)),
                y: Number(sample.position.y.toFixed(3)),
                z: Number(sample.position.z.toFixed(3)),
              },
              speedKmh: Number(sample.telemetry.speedKmh.toFixed(2)),
              signedSpeedKmh: Number(sample.telemetry.signedSpeedKmh.toFixed(2)),
              engineRpm: Math.round(sample.telemetry.engineRpm),
              gear: sample.telemetry.gear,
              headingDegrees: Number(sample.telemetry.headingDegrees.toFixed(2)),
              pitchDegrees: Number(sample.telemetry.pitchDegrees.toFixed(2)),
              rollDegrees: Number(sample.telemetry.rollDegrees.toFixed(2)),
              slipPercent: sample.telemetry.slipPercent,
              wheels: sample.telemetry.wheels.map((wheel) => ({
                name: wheel.name,
                contact: wheel.contact,
                suspensionLength: Number(wheel.suspensionLength.toFixed(3)),
                compression: Number(wheel.compression.toFixed(3)),
                bumpStopForce: Math.round(wheel.bumpStopForce),
                normalLoad: Math.round(wheel.normalLoad),
                slipAngle: Number(wheel.slipAngle.toFixed(3)),
                longitudinalSlip: Number(wheel.longitudinalSlip.toFixed(3)),
                tractionUsage: Number(wheel.tractionUsage.toFixed(3)),
                driveTorque: Math.round(wheel.driveTorque),
                brakeTorque: Math.round(wheel.brakeTorque),
                camber: Number(wheel.camber.toFixed(3)),
              })),
            })),
          ]),
        ),
        null,
        2,
      ),
    );

    expect(scenarios.mixedDriving.samples.length).toBeGreaterThan(20);
  });
});
