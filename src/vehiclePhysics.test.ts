import RAPIER from "@dimforge/rapier3d-compat";
import { beforeAll, describe, expect, it } from "vitest";
import {
  averageWheelValue,
  averageSampleValue,
  runScenario,
  type ScenarioSample,
} from "./vehicleScenarios";
import { cloneVehicleConfig } from "./vehiclePhysics";

beforeAll(async () => {
  await RAPIER.init();
});

describe("custom vehicle physics", () => {
  it("settles at a plausible static ride height", () => {
    const result = runScenario([{ seconds: 0.5, input: {} }], { settleSeconds: 3 });
    const final = result.final;
    const compressions = final.telemetry.wheels.map((wheel) => wheel.compression);

    expect(final.telemetry.wheels.every((wheel) => wheel.contact)).toBe(true);
    expect(final.position.y).toBeGreaterThan(1);
    expect(final.position.y).toBeLessThan(1.22);
    expect(Math.min(...compressions)).toBeGreaterThan(0.08);
    expect(Math.max(...compressions)).toBeLessThan(0.24);
  });

  it("uses local +Z as forward and reverses after braking to a stop", () => {
    const result = runScenario([
      { seconds: 2.4, input: { throttle: true } },
      { seconds: 3.6, input: { brake: true } },
    ]);

    const maxForwardSpeed = Math.max(...result.samples.map((sample) => sample.telemetry.signedSpeedKmh));
    expect(maxForwardSpeed).toBeGreaterThan(18);
    expect(result.final.telemetry.signedSpeedKmh).toBeLessThan(-4);
    expect(result.final.position.z).toBeLessThan(20);
  });

  it("turns left for left input and right for right input", () => {
    const left = runScenario([{ seconds: 3.1, input: { throttle: true, steerLeft: true } }]);
    const right = runScenario([{ seconds: 3.1, input: { throttle: true, steerRight: true } }]);

    expect(left.final.position.x).toBeGreaterThan(1);
    expect(left.final.telemetry.headingDegrees).toBeGreaterThan(5);
    expect(left.final.telemetry.steeringDegrees).toBeGreaterThan(0);

    expect(right.final.position.x).toBeLessThan(-1);
    expect(right.final.telemetry.headingDegrees).toBeLessThan(-5);
    expect(right.final.telemetry.steeringDegrees).toBeLessThan(0);
  });

  it("shows progressive brake dive instead of an instant pitch snap", () => {
    const result = runScenario(
      [
        { seconds: 2.2, input: { throttle: true } },
        { seconds: 1.4, input: { brake: true } },
      ],
      { sampleEveryFrames: 3 },
    );
    const brakeStart = 2.2 + 2.2;
    const brakeSamples = result.samples.filter(
      (sample) => sample.time >= brakeStart && sample.time <= brakeStart + 0.8,
    );
    const frontCompression = averageWheelValue(brakeSamples, (wheel) => wheel.isFront, (wheel) => wheel.compression);
    const rearCompression = averageWheelValue(brakeSamples, (wheel) => !wheel.isFront, (wheel) => wheel.compression);
    const pitchValues = brakeSamples.map((sample) => sample.telemetry.pitchDegrees);
    const pitchDelta = Math.max(...pitchValues) - Math.min(...pitchValues);
    const largestPitchStep = maxStep(brakeSamples, (sample) => sample.telemetry.pitchDegrees);

    expect(frontCompression).toBeGreaterThan(rearCompression + 0.006);
    expect(pitchDelta).toBeGreaterThan(0.4);
    expect(largestPitchStep).toBeLessThan(3.5);
  });

  it("loads the outside wheels during steady cornering", () => {
    const left = runScenario([{ seconds: 3.2, input: { throttle: true, steerLeft: true } }]);
    const right = runScenario([{ seconds: 3.2, input: { throttle: true, steerRight: true } }]);

    const leftTurnRightLoad = averageWheelValue(left.samples, (wheel) => !wheel.isLeft, (wheel) => wheel.normalLoad, 1);
    const leftTurnLeftLoad = averageWheelValue(left.samples, (wheel) => wheel.isLeft, (wheel) => wheel.normalLoad, 1);
    const rightTurnLeftLoad = averageWheelValue(right.samples, (wheel) => wheel.isLeft, (wheel) => wheel.normalLoad, 1);
    const rightTurnRightLoad = averageWheelValue(right.samples, (wheel) => !wheel.isLeft, (wheel) => wheel.normalLoad, 1);

    expect(leftTurnRightLoad).toBeGreaterThan(leftTurnLeftLoad);
    expect(rightTurnLeftLoad).toBeGreaterThan(rightTurnRightLoad);
  });

  it("handbrake increases rear slip and slows the car", () => {
    const coasting = runScenario([
      { seconds: 2.4, input: { throttle: true } },
      { seconds: 1.2, input: { steerLeft: true } },
    ]);
    const serviceBrake = runScenario([
      { seconds: 2.4, input: { throttle: true } },
      { seconds: 1.2, input: { steerLeft: true, brake: true } },
    ]);
    const handbrake = runScenario([
      { seconds: 2.4, input: { throttle: true } },
      { seconds: 1.2, input: { steerLeft: true, handbrake: true } },
    ]);

    const normalRearSlip = averageWheelValue(
      coasting.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      1,
    );
    const handbrakeRearSlip = averageWheelValue(
      handbrake.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      1,
    );
    const serviceRearSlide = averageWheelValue(
      serviceBrake.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip) + Math.abs(wheel.slipAngle),
      1,
    );
    const handbrakeRearSlide = averageWheelValue(
      handbrake.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip) + Math.abs(wheel.slipAngle),
      1,
    );

    expect(handbrakeRearSlide).toBeGreaterThan(serviceRearSlide + 0.25);
    expect(handbrakeRearSlip).toBeGreaterThan(normalRearSlip * 0.75);
    expect(handbrakeRearSlip).toBeGreaterThan(normalRearSlip + 0.1);
    expect(handbrake.final.telemetry.speedKmh).toBeLessThan(coasting.final.telemetry.speedKmh);
  });

  it("uses a rear-drive two-speed gearbox and can spin the driven wheels", () => {
    const result = runScenario(
      [{ seconds: 5.5, input: { throttle: true } }],
      { sampleEveryFrames: 3 },
    );
    const maxGear = Math.max(...result.samples.map((sample) => sample.telemetry.gear));
    const maxRpm = Math.max(...result.samples.map((sample) => sample.telemetry.engineRpm));
    const rearSlip = averageWheelValue(
      result.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      2,
    );
    const frontSlip = averageWheelValue(
      result.samples,
      (wheel) => wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      2,
    );

    expect(maxGear).toBeGreaterThanOrEqual(2);
    expect(maxRpm).toBeGreaterThan(4_500);
    expect(rearSlip).toBeGreaterThan(frontSlip + 0.3);
    expect(rearSlip).toBeGreaterThan(0.5);
  });

  it("keeps a heavy soft setup on its bump stops instead of sinking through the ground", () => {
    const config = cloneVehicleConfig();
    config.mass = 3_000;
    config.engineTorque = 2_650;
    config.suspensionRestLength = 0.82;
    config.suspensionBumpTravel = 0.22;
    config.suspensionDroopTravel = 0.42;
    config.springRate = 24_000;
    config.bumpDamping = 5_800;
    config.reboundDamping = 8_800;
    config.frontAntiRoll = 0;
    config.rearAntiRoll = 0;

    const result = runScenario([{ seconds: 0.8, input: {} }], {
      config,
      settleSeconds: 4,
      sampleEveryFrames: 4,
    });
    const wheelBottoms = result.final.telemetry.wheels.map(
      (wheel) => result.final.position.y + wheel.localWheelCenter.y - config.wheelRadius,
    );
    const averageBumpStopForce = averageWheelValue(
      result.samples,
      () => true,
      (wheel) => wheel.bumpStopForce,
      1,
    );

    expect(result.final.telemetry.wheels.every((wheel) => wheel.contact)).toBe(true);
    expect(Math.min(...wheelBottoms)).toBeGreaterThan(-0.04);
    expect(averageBumpStopForce).toBeGreaterThan(250);
    expect(result.final.position.y).toBeGreaterThan(0.95);
  });

  it("rear-drive torque can saturate the rear tires and create power oversteer", () => {
    const lightThrottle = cloneVehicleConfig();
    lightThrottle.engineTorque = 180;
    lightThrottle.differentialLock = 0.05;
    lightThrottle.rearPowerOversteer = 0.1;
    lightThrottle.longitudinalSlideGrip = 0.9;
    lightThrottle.lateralSlideGrip = 0.8;

    const hardThrottle = cloneVehicleConfig();
    hardThrottle.engineTorque = 980;
    hardThrottle.differentialLock = 0.55;
    hardThrottle.rearPowerOversteer = 0.72;
    hardThrottle.longitudinalSlideGrip = 0.5;
    hardThrottle.lateralSlideGrip = 0.5;

    const gentle = runScenario(
      [
        { seconds: 1.8, input: { throttle: true } },
        { seconds: 2, input: { throttle: true, steerLeft: true } },
      ],
      { config: lightThrottle, sampleEveryFrames: 3 },
    );
    const powered = runScenario(
      [
        { seconds: 1.8, input: { throttle: true } },
        { seconds: 2, input: { throttle: true, steerLeft: true } },
      ],
      { config: hardThrottle, sampleEveryFrames: 3 },
    );

    const gentleRearSlip = averageWheelValue(
      gentle.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      1.2,
    );
    const poweredRearSlip = averageWheelValue(
      powered.samples,
      (wheel) => !wheel.isFront,
      (wheel) => Math.abs(wheel.longitudinalSlip),
      1.2,
    );
    const gentleYawRate = averageHeadingStep(gentle.samples, 1.2);
    const poweredYawRate = averageHeadingStep(powered.samples, 1.2);
    const gentleSlip = averageSampleValue(
      gentle.samples,
      (sample) => sample.telemetry.slipPercent,
      1.2,
    );
    const poweredSlip = averageSampleValue(
      powered.samples,
      (sample) => sample.telemetry.slipPercent,
      1.2,
    );

    expect(poweredRearSlip).toBeGreaterThan(gentleRearSlip + 0.16);
    expect(poweredYawRate).toBeGreaterThan(gentleYawRate * 1.12);
    expect(poweredSlip).toBeGreaterThan(gentleSlip + 5);
  });

  it("is deterministic for the same input sequence", () => {
    const first = runScenario([
      { seconds: 1.5, input: { throttle: true } },
      { seconds: 1.6, input: { throttle: true, steerRight: true } },
      { seconds: 1.2, input: { brake: true, steerLeft: true } },
    ]);
    const second = runScenario([
      { seconds: 1.5, input: { throttle: true } },
      { seconds: 1.6, input: { throttle: true, steerRight: true } },
      { seconds: 1.2, input: { brake: true, steerLeft: true } },
    ]);

    expect(second.final.position.x).toBeCloseTo(first.final.position.x, 4);
    expect(second.final.position.z).toBeCloseTo(first.final.position.z, 4);
    expect(second.final.telemetry.headingDegrees).toBeCloseTo(first.final.telemetry.headingDegrees, 4);
    expect(second.final.telemetry.signedSpeedKmh).toBeCloseTo(first.final.telemetry.signedSpeedKmh, 4);
  });
});

function maxStep(samples: ScenarioSample[], value: (sample: ScenarioSample) => number) {
  let largest = 0;
  for (let i = 1; i < samples.length; i += 1) {
    largest = Math.max(largest, Math.abs(value(samples[i]) - value(samples[i - 1])));
  }
  return largest;
}

function averageHeadingStep(samples: ScenarioSample[], lastSeconds: number) {
  const finalTime = samples[samples.length - 1]?.time ?? 0;
  const filtered = samples.filter((sample) => finalTime - sample.time <= lastSeconds);
  let total = 0;
  let steps = 0;
  for (let i = 1; i < filtered.length; i += 1) {
    total += Math.abs(filtered[i].telemetry.headingDegrees - filtered[i - 1].telemetry.headingDegrees);
    steps += 1;
  }
  return total / Math.max(steps, 1);
}
