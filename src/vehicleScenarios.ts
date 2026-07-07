import * as THREE from "three";
import {
  createDrivingWorld,
  defaultVehicleConfig,
  FIXED_TIMESTEP,
  type InputState,
  type VehicleConfig,
  type VehicleTelemetry,
  VehiclePhysics,
} from "./vehiclePhysics";

export type ScenarioSegment = {
  seconds: number;
  input: Partial<InputState>;
};

export type ScenarioSample = {
  time: number;
  position: THREE.Vector3;
  telemetry: VehicleTelemetry;
};

export type ScenarioResult = {
  samples: ScenarioSample[];
  final: ScenarioSample;
};

const EMPTY_INPUT: InputState = {
  throttle: false,
  brake: false,
  steerLeft: false,
  steerRight: false,
  handbrake: false,
};

export function input(overrides: Partial<InputState> = {}): InputState {
  return { ...EMPTY_INPUT, ...overrides };
}

export function runScenario(
  segments: ScenarioSegment[],
  options: {
    config?: VehicleConfig;
    settleSeconds?: number;
    sampleEveryFrames?: number;
  } = {},
): ScenarioResult {
  const config = options.config ?? defaultVehicleConfig;
  const world = createDrivingWorld();
  const vehicle = new VehiclePhysics(world, config);
  const sampleEveryFrames = options.sampleEveryFrames ?? 6;
  const samples: ScenarioSample[] = [];
  let frame = 0;
  let elapsed = 0;

  stepMany(vehicle, world, input(), options.settleSeconds ?? 2.2, () => {
    frame += 1;
    elapsed += FIXED_TIMESTEP;
  });

  for (const segment of segments) {
    const segmentInput = input(segment.input);
    stepMany(vehicle, world, segmentInput, segment.seconds, () => {
      frame += 1;
      elapsed += FIXED_TIMESTEP;
      if (frame % sampleEveryFrames === 0) {
        samples.push(sampleVehicle(vehicle, elapsed));
      }
    });
  }

  if (samples.length === 0) {
    samples.push(sampleVehicle(vehicle, elapsed));
  }

  const final = sampleVehicle(vehicle, elapsed);
  samples.push(final);
  return { samples, final };
}

export function averageWheelValue(
  samples: ScenarioSample[],
  wheelFilter: (wheel: VehicleTelemetry["wheels"][number]) => boolean,
  value: (wheel: VehicleTelemetry["wheels"][number]) => number,
  lastSeconds?: number,
) {
  const filteredSamples = filterLastSeconds(samples, lastSeconds);
  const values = filteredSamples.flatMap((sample) =>
    sample.telemetry.wheels.filter(wheelFilter).map(value),
  );
  return values.reduce((sum, current) => sum + current, 0) / Math.max(values.length, 1);
}

export function averageSampleValue(
  samples: ScenarioSample[],
  value: (sample: ScenarioSample) => number,
  lastSeconds?: number,
) {
  const filteredSamples = filterLastSeconds(samples, lastSeconds);
  return (
    filteredSamples.reduce((sum, sample) => sum + value(sample), 0) /
    Math.max(filteredSamples.length, 1)
  );
}

function filterLastSeconds(samples: ScenarioSample[], lastSeconds?: number) {
  if (!lastSeconds || samples.length === 0) {
    return samples;
  }
  const finalTime = samples[samples.length - 1].time;
  return samples.filter((sample) => finalTime - sample.time <= lastSeconds);
}

function stepMany(
  vehicle: VehiclePhysics,
  world: ReturnType<typeof createDrivingWorld>,
  stepInput: InputState,
  seconds: number,
  afterStep?: () => void,
) {
  const frames = Math.max(1, Math.round(seconds / FIXED_TIMESTEP));
  for (let i = 0; i < frames; i += 1) {
    vehicle.update(stepInput, FIXED_TIMESTEP);
    world.step();
    afterStep?.();
  }
}

function sampleVehicle(vehicle: VehiclePhysics, time: number): ScenarioSample {
  return {
    time,
    position: vehicle.getPosition(),
    telemetry: vehicle.getTelemetry(),
  };
}
