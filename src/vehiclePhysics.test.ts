import RAPIER from "@dimforge/rapier3d-compat";
import { beforeAll, describe, expect, it } from "vitest";
import {
  averageWheelValue,
  runScenario,
  type ScenarioSample,
} from "./vehicleScenarios";

beforeAll(async () => {
  await RAPIER.init();
});

describe("custom vehicle physics", () => {
  it("settles at a plausible static ride height", () => {
    const result = runScenario([{ seconds: 0.5, input: {} }], { settleSeconds: 3 });
    const final = result.final;
    const compressions = final.telemetry.wheels.map((wheel) => wheel.compression);

    expect(final.telemetry.wheels.every((wheel) => wheel.contact)).toBe(true);
    expect(final.position.y).toBeGreaterThan(0.72);
    expect(final.position.y).toBeLessThan(1.05);
    expect(Math.min(...compressions)).toBeGreaterThan(0.015);
    expect(Math.max(...compressions)).toBeLessThan(0.18);
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
    const serviceRearSlip = averageWheelValue(
      serviceBrake.samples,
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

    expect(handbrakeRearSlip).toBeGreaterThan(serviceRearSlip + 0.03);
    expect(handbrakeRearSlide).toBeGreaterThan(serviceRearSlide + 0.25);
    expect(handbrakeRearSlip).toBeGreaterThan(normalRearSlip * 0.75);
    expect(handbrake.final.telemetry.speedKmh).toBeLessThan(coasting.final.telemetry.speedKmh);
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
