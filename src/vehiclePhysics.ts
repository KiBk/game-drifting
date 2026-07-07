import RAPIER from "@dimforge/rapier3d-compat";
import * as THREE from "three";

export type InputState = {
  throttle: boolean;
  brake: boolean;
  steerLeft: boolean;
  steerRight: boolean;
  handbrake: boolean;
};

export type WheelConfig = {
  name: "frontLeft" | "frontRight" | "rearLeft" | "rearRight";
  localHardpoint: THREE.Vector3;
  isFront: boolean;
  isLeft: boolean;
  isDriven: boolean;
};

export type VehicleConfig = {
  mass: number;
  chassisStartHeight: number;
  chassisHalfExtents: THREE.Vector3;
  centerOfMass: THREE.Vector3;
  principalAngularInertia: THREE.Vector3;
  wheelRadius: number;
  wheelWidth: number;
  wheelInertia: number;
  suspensionRestLength: number;
  suspensionBumpTravel: number;
  suspensionDroopTravel: number;
  springRate: number;
  bumpDamping: number;
  reboundDamping: number;
  maxSuspensionForce: number;
  bumpStopRange: number;
  bumpStopRate: number;
  bumpStopDamping: number;
  maxBumpStopForce: number;
  frontAntiRoll: number;
  rearAntiRoll: number;
  engineTorque: number;
  reverseTorque: number;
  gearRatios: number[];
  reverseGearRatio: number;
  finalDriveRatio: number;
  drivetrainEfficiency: number;
  idleRpm: number;
  redlineRpm: number;
  shiftUpRpm: number;
  shiftDownRpm: number;
  engineBrakingTorque: number;
  differentialLock: number;
  brakeTorque: number;
  handbrakeTorque: number;
  brakeBias: number;
  maxSteerAngle: number;
  ackermann: number;
  frictionCoefficient: number;
  handbrakeFrictionCoefficient: number;
  loadSensitivity: number;
  corneringStiffness: number;
  handbrakeCorneringStiffness: number;
  longitudinalStiffness: number;
  longitudinalPeakSlip: number;
  longitudinalSlideSlip: number;
  longitudinalSlideGrip: number;
  lateralPeakSlipAngle: number;
  lateralSlideSlipAngle: number;
  lateralSlideGrip: number;
  tireForceCoupling: number;
  rearPowerOversteer: number;
  rollingResistance: number;
  camberAtDroop: number;
  camberAtRest: number;
  camberAtBump: number;
  camberStiffness: number;
  wheels: WheelConfig[];
};

export type WheelTelemetry = {
  name: WheelConfig["name"];
  isFront: boolean;
  isLeft: boolean;
  contact: boolean;
  localHardpoint: THREE.Vector3;
  localWheelCenter: THREE.Vector3;
  suspensionLength: number;
  compression: number;
  bumpStopForce: number;
  normalLoad: number;
  steeringAngle: number;
  camber: number;
  angularVelocity: number;
  slipAngle: number;
  longitudinalSlip: number;
  lateralForce: number;
  longitudinalForce: number;
  driveTorque: number;
  brakeTorque: number;
  tractionUsage: number;
  contactPoint: THREE.Vector3 | null;
};

export type VehicleTelemetry = {
  speedKmh: number;
  signedSpeedKmh: number;
  engineRpm: number;
  gear: number;
  headingDegrees: number;
  steeringDegrees: number;
  slipPercent: number;
  pitchDegrees: number;
  rollDegrees: number;
  wheels: WheelTelemetry[];
};

type WheelRuntime = WheelTelemetry & {
  previousSuspensionLength: number;
};

const ZERO_INPUT: InputState = {
  throttle: false,
  brake: false,
  steerLeft: false,
  steerRight: false,
  handbrake: false,
};

export const FIXED_TIMESTEP = 1 / 60;
export const WORLD_SIZE = 480;

// Three/Rapier are right-handed. With the chassis nose on local +Z and up on
// local +Y, vehicle-left is local +X and vehicle-right is local -X.
export const defaultVehicleConfig: VehicleConfig = {
  mass: 2750,
  chassisStartHeight: 1.36,
  chassisHalfExtents: new THREE.Vector3(0.95, 0.34, 1.65),
  centerOfMass: new THREE.Vector3(0, -0.32, 0.08),
  principalAngularInertia: new THREE.Vector3(1320, 2320, 1020),
  wheelRadius: 0.36,
  wheelWidth: 0.3,
  wheelInertia: 2.15,
  suspensionRestLength: 0.78,
  suspensionBumpTravel: 0.26,
  suspensionDroopTravel: 0.42,
  springRate: 36_000,
  bumpDamping: 6_100,
  reboundDamping: 9_400,
  maxSuspensionForce: 18_500,
  bumpStopRange: 0.12,
  bumpStopRate: 215_000,
  bumpStopDamping: 13_000,
  maxBumpStopForce: 48_000,
  frontAntiRoll: 0,
  rearAntiRoll: 0,
  engineTorque: 680,
  reverseTorque: 470,
  gearRatios: [3.2, 1.78],
  reverseGearRatio: 2.9,
  finalDriveRatio: 3.73,
  drivetrainEfficiency: 0.86,
  idleRpm: 850,
  redlineRpm: 6_200,
  shiftUpRpm: 5_450,
  shiftDownRpm: 2_050,
  engineBrakingTorque: 55,
  differentialLock: 0.42,
  brakeTorque: 2_250,
  handbrakeTorque: 2_800,
  brakeBias: 0.72,
  maxSteerAngle: THREE.MathUtils.degToRad(31),
  ackermann: 0.22,
  frictionCoefficient: 0.98,
  handbrakeFrictionCoefficient: 0.62,
  loadSensitivity: 0.2,
  corneringStiffness: 3.7,
  handbrakeCorneringStiffness: 1.5,
  longitudinalStiffness: 8.6,
  longitudinalPeakSlip: 0.12,
  longitudinalSlideSlip: 0.9,
  longitudinalSlideGrip: 0.54,
  lateralPeakSlipAngle: THREE.MathUtils.degToRad(9.5),
  lateralSlideSlipAngle: THREE.MathUtils.degToRad(31),
  lateralSlideGrip: 0.54,
  tireForceCoupling: 0.92,
  rearPowerOversteer: 0.62,
  rollingResistance: 0.1,
  camberAtDroop: THREE.MathUtils.degToRad(1.2),
  camberAtRest: THREE.MathUtils.degToRad(-0.3),
  camberAtBump: THREE.MathUtils.degToRad(-2.4),
  camberStiffness: 0.08,
  wheels: [
    {
      name: "frontLeft",
      localHardpoint: new THREE.Vector3(0.92, -0.08, 1.45),
      isFront: true,
      isLeft: true,
      isDriven: false,
    },
    {
      name: "frontRight",
      localHardpoint: new THREE.Vector3(-0.92, -0.08, 1.45),
      isFront: true,
      isLeft: false,
      isDriven: false,
    },
    {
      name: "rearLeft",
      localHardpoint: new THREE.Vector3(0.92, -0.08, -1.32),
      isFront: false,
      isLeft: true,
      isDriven: true,
    },
    {
      name: "rearRight",
      localHardpoint: new THREE.Vector3(-0.92, -0.08, -1.32),
      isFront: false,
      isLeft: false,
      isDriven: true,
    },
  ],
};

export function cloneVehicleConfig(config: VehicleConfig = defaultVehicleConfig): VehicleConfig {
  return {
    ...config,
    gearRatios: [...config.gearRatios],
    chassisHalfExtents: config.chassisHalfExtents.clone(),
    centerOfMass: config.centerOfMass.clone(),
    principalAngularInertia: config.principalAngularInertia.clone(),
    wheels: config.wheels.map((wheel) => ({
      ...wheel,
      localHardpoint: wheel.localHardpoint.clone(),
    })),
  };
}

export function createDrivingWorld() {
  const world = new RAPIER.World({ x: 0, y: -9.81, z: 0 });
  world.timestep = FIXED_TIMESTEP;
  const groundColliderDesc = RAPIER.ColliderDesc.cuboid(WORLD_SIZE / 2, 0.1, WORLD_SIZE / 2)
    .setTranslation(0, -0.1, 0)
    .setFriction(1.4);
  world.createCollider(groundColliderDesc);
  return world;
}

export class VehiclePhysics {
  readonly body: RAPIER.RigidBody;

  private readonly wheels: WheelRuntime[];
  private readonly bodyQuaternion = new THREE.Quaternion();
  private readonly forward = new THREE.Vector3(0, 0, 1);
  private readonly side = new THREE.Vector3(-1, 0, 0);
  private readonly up = new THREE.Vector3(0, 1, 0);
  private readonly velocity = new THREE.Vector3();
  private latestForwardSpeed = 0;
  private latestLateralSpeed = 0;
  private steeringAngle = 0;
  private currentGearIndex = 0;
  private gearDisplay = 1;
  private engineRpm = defaultVehicleConfig.idleRpm;

  constructor(
    private readonly world: RAPIER.World,
    readonly config: VehicleConfig = defaultVehicleConfig,
  ) {
    this.body = this.createBody();
    this.wheels = config.wheels.map((wheel) => ({
      name: wheel.name,
      isFront: wheel.isFront,
      isLeft: wheel.isLeft,
      contact: false,
      localHardpoint: wheel.localHardpoint.clone(),
      localWheelCenter: wheel.localHardpoint
        .clone()
        .add(new THREE.Vector3(0, -config.suspensionRestLength, 0)),
      suspensionLength: config.suspensionRestLength,
      previousSuspensionLength: config.suspensionRestLength,
      compression: 0,
      bumpStopForce: 0,
      normalLoad: 0,
      steeringAngle: 0,
      camber: config.camberAtRest,
      angularVelocity: 0,
      slipAngle: 0,
      longitudinalSlip: 0,
      lateralForce: 0,
      longitudinalForce: 0,
      driveTorque: 0,
      brakeTorque: 0,
      tractionUsage: 0,
      contactPoint: null,
    }));
  }

  update(input: InputState = ZERO_INPUT, dt = FIXED_TIMESTEP) {
    this.body.resetForces(true);
    this.body.resetTorques(true);
    this.updateLocalAxes();
    this.updateSpeedTelemetry();
    this.updateSteering(input);
    this.updateWheelContacts(dt);
    this.applyAntiRollForces();
    this.applyTireForces(input, dt);
  }

  getTelemetry(): VehicleTelemetry {
    const rotation = this.body.rotation();
    const quaternion = new THREE.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
    const euler = new THREE.Euler().setFromQuaternion(quaternion, "YXZ");
    return {
      speedKmh: Math.abs(this.latestForwardSpeed) * 3.6,
      signedSpeedKmh: this.latestForwardSpeed * 3.6,
      engineRpm: this.engineRpm,
      gear: this.gearDisplay,
      headingDegrees: THREE.MathUtils.radToDeg(Math.atan2(this.forward.x, this.forward.z)),
      steeringDegrees: THREE.MathUtils.radToDeg(this.steeringAngle),
      slipPercent: Math.round(
        THREE.MathUtils.clamp(
          Math.abs(this.latestLateralSpeed) / Math.max(Math.abs(this.latestForwardSpeed), 4),
          0,
          1,
        ) * 100,
      ),
      pitchDegrees: THREE.MathUtils.radToDeg(euler.x),
      rollDegrees: THREE.MathUtils.radToDeg(euler.z),
      wheels: this.wheels.map((wheel) => ({
        name: wheel.name,
        isFront: wheel.isFront,
        isLeft: wheel.isLeft,
        contact: wheel.contact,
        localHardpoint: wheel.localHardpoint.clone(),
        localWheelCenter: wheel.localWheelCenter.clone(),
        suspensionLength: wheel.suspensionLength,
        compression: wheel.compression,
        bumpStopForce: wheel.bumpStopForce,
        normalLoad: wheel.normalLoad,
        steeringAngle: wheel.steeringAngle,
        camber: wheel.camber,
        angularVelocity: wheel.angularVelocity,
        slipAngle: wheel.slipAngle,
        longitudinalSlip: wheel.longitudinalSlip,
        lateralForce: wheel.lateralForce,
        longitudinalForce: wheel.longitudinalForce,
        driveTorque: wheel.driveTorque,
        brakeTorque: wheel.brakeTorque,
        tractionUsage: wheel.tractionUsage,
        contactPoint: wheel.contactPoint?.clone() ?? null,
      })),
    };
  }

  getPosition() {
    const position = this.body.translation();
    return new THREE.Vector3(position.x, position.y, position.z);
  }

  getQuaternion() {
    const rotation = this.body.rotation();
    return new THREE.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
  }

  applyMassProperties() {
    this.body.setAdditionalMassProperties(
      this.config.mass,
      this.config.centerOfMass,
      this.config.principalAngularInertia,
      { x: 0, y: 0, z: 0, w: 1 },
      true,
    );
  }

  private createBody() {
    const bodyDesc = RAPIER.RigidBodyDesc.dynamic()
      .setTranslation(0, this.config.chassisStartHeight, 0)
      .setLinearDamping(0.04)
      .setAngularDamping(0.34)
      .setAdditionalSolverIterations(6)
      .setCanSleep(false)
      .setCcdEnabled(true)
      .setAdditionalMassProperties(
        this.config.mass,
        this.config.centerOfMass,
        this.config.principalAngularInertia,
        { x: 0, y: 0, z: 0, w: 1 },
      );
    const body = this.world.createRigidBody(bodyDesc);
    const colliderDesc = RAPIER.ColliderDesc.cuboid(
      this.config.chassisHalfExtents.x,
      this.config.chassisHalfExtents.y,
      this.config.chassisHalfExtents.z,
    )
      .setFriction(0.8)
      .setRestitution(0.03);
    this.world.createCollider(colliderDesc, body);
    return body;
  }

  private updateLocalAxes() {
    const rotation = this.body.rotation();
    this.bodyQuaternion.set(rotation.x, rotation.y, rotation.z, rotation.w);
    this.forward.set(0, 0, 1).applyQuaternion(this.bodyQuaternion).normalize();
    this.side.set(-1, 0, 0).applyQuaternion(this.bodyQuaternion).normalize();
    this.up.set(0, 1, 0).applyQuaternion(this.bodyQuaternion).normalize();
  }

  private updateSpeedTelemetry() {
    const velocity = this.body.linvel();
    this.velocity.set(velocity.x, velocity.y, velocity.z);
    this.latestForwardSpeed = this.velocity.dot(this.forward);
    this.latestLateralSpeed = this.velocity.dot(this.side);
  }

  private updateSteering(input: InputState) {
    const steerInput = Number(input.steerLeft) - Number(input.steerRight);
    this.steeringAngle = steerInput * this.config.maxSteerAngle;
  }

  private updateWheelContacts(dt: number) {
    const maxRayLength =
      this.config.suspensionRestLength +
      this.config.suspensionDroopTravel +
      this.config.wheelRadius;
    const minimumLength = this.config.suspensionRestLength - this.config.suspensionBumpTravel;
    const maximumLength = this.config.suspensionRestLength + this.config.suspensionDroopTravel;
    const down = this.up.clone().multiplyScalar(-1);

    for (const wheel of this.wheels) {
      const hardpoint = this.localToWorld(wheel.localHardpoint);
      const ray = new RAPIER.Ray(hardpoint, down);
      const hit = this.world.castRayAndGetNormal(
        ray,
        maxRayLength,
        true,
        undefined,
        undefined,
        undefined,
        this.body,
      );

      wheel.previousSuspensionLength = wheel.suspensionLength;
      wheel.contact = Boolean(hit);
      wheel.contactPoint = null;
      wheel.normalLoad = 0;
      wheel.compression = 0;
      wheel.bumpStopForce = 0;

      if (!hit) {
        wheel.suspensionLength = maximumLength;
        wheel.localWheelCenter.copy(wheel.localHardpoint).add(new THREE.Vector3(0, -maximumLength, 0));
        wheel.camber = this.computeCamber(wheel.suspensionLength);
        continue;
      }

      const rawSuspensionLength = hit.timeOfImpact - this.config.wheelRadius;
      wheel.suspensionLength = THREE.MathUtils.clamp(
        rawSuspensionLength,
        minimumLength,
        maximumLength,
      );
      wheel.localWheelCenter.copy(wheel.localHardpoint).add(
        new THREE.Vector3(0, -wheel.suspensionLength, 0),
      );
      wheel.contactPoint = rayPointAt(hardpoint, down, hit.timeOfImpact);
      wheel.compression = Math.max(0, this.config.suspensionRestLength - wheel.suspensionLength);
      wheel.camber = this.computeCamber(wheel.suspensionLength);

      const compressionVelocity = (wheel.previousSuspensionLength - wheel.suspensionLength) / dt;
      const damping =
        compressionVelocity >= 0 ? this.config.bumpDamping : this.config.reboundDamping;
      const springForce = wheel.compression * this.config.springRate;
      const damperForce = compressionVelocity * damping;
      const regularSuspensionForce = THREE.MathUtils.clamp(
        springForce + damperForce,
        0,
        this.config.maxSuspensionForce,
      );
      const bumpStopCompression = Math.max(
        0,
        minimumLength + this.config.bumpStopRange - rawSuspensionLength,
      );
      const bumpStopCompressionVelocity =
        bumpStopCompression > 0 ? Math.max(0, compressionVelocity) : 0;
      const normalizedBumpStop = bumpStopCompression / Math.max(this.config.bumpStopRange, 0.001);
      const bumpStopForce = THREE.MathUtils.clamp(
        this.config.bumpStopRate * bumpStopCompression * (1 + normalizedBumpStop * 0.75) +
          this.config.bumpStopDamping * bumpStopCompressionVelocity,
        0,
        this.config.maxBumpStopForce,
      );
      const suspensionForce = THREE.MathUtils.clamp(
        regularSuspensionForce + bumpStopForce,
        0,
        this.config.maxSuspensionForce + this.config.maxBumpStopForce,
      );

      wheel.bumpStopForce = bumpStopForce;
      wheel.normalLoad = suspensionForce;
      this.body.addForceAtPoint(vectorToRapier(this.up.clone().multiplyScalar(suspensionForce)), hardpoint, true);
    }
  }

  private applyAntiRollForces() {
    this.applyAxleAntiRoll(true, this.config.frontAntiRoll);
    this.applyAxleAntiRoll(false, this.config.rearAntiRoll);
  }

  private applyAxleAntiRoll(isFront: boolean, stiffness: number) {
    const left = this.wheels.find((wheel) => wheel.isFront === isFront && wheel.isLeft);
    const right = this.wheels.find((wheel) => wheel.isFront === isFront && !wheel.isLeft);
    if (!left || !right || !left.contact || !right.contact) {
      return;
    }

    const rightExtraCompression = right.compression - left.compression;
    const forceMagnitude = THREE.MathUtils.clamp(rightExtraCompression * stiffness, -4_000, 4_000);
    this.body.addForceAtPoint(
      vectorToRapier(this.up.clone().multiplyScalar(-forceMagnitude)),
      this.localToWorld(left.localHardpoint),
      true,
    );
    this.body.addForceAtPoint(
      vectorToRapier(this.up.clone().multiplyScalar(forceMagnitude)),
      this.localToWorld(right.localHardpoint),
      true,
    );
  }

  private applyTireForces(input: InputState, dt: number) {
    const shouldReverse = input.brake && this.latestForwardSpeed < 0.25;
    const shouldBrake = input.brake && !shouldReverse;
    const drivenWheelCount = this.config.wheels.filter((wheel) => wheel.isDriven).length;
    const drivenAngularVelocity =
      drivenWheelCount > 0
        ? this.wheels.reduce(
            (sum, wheel, index) =>
              sum + (this.config.wheels[index].isDriven ? wheel.angularVelocity : 0),
            0,
          ) / drivenWheelCount
        : 0;
    this.updateDrivetrain(input, shouldReverse, drivenAngularVelocity, dt);

    for (const [index, wheel] of this.wheels.entries()) {
      const config = this.config.wheels[index];
      wheel.steeringAngle = config.isFront ? this.getAckermannSteer(config) : 0;
      wheel.longitudinalForce = 0;
      wheel.lateralForce = 0;
      wheel.slipAngle = 0;
      wheel.longitudinalSlip = 0;
      wheel.driveTorque = 0;
      wheel.brakeTorque = 0;
      wheel.tractionUsage = 0;

      if (!wheel.contact || !wheel.contactPoint || wheel.normalLoad <= 0) {
        wheel.angularVelocity *= Math.exp(-dt * 0.8);
        continue;
      }

      const axes = this.getWheelAxes(wheel.steeringAngle);
      const pointVelocity = this.body.velocityAtPoint(wheel.contactPoint);
      const velocity = new THREE.Vector3(pointVelocity.x, pointVelocity.y, pointVelocity.z);
      const longitudinalSpeed = velocity.dot(axes.forward);
      const lateralSpeed = velocity.dot(axes.side);
      const isRear = !config.isFront;
      const isHandbrakingRear = input.handbrake && isRear;
      const effectiveMu = this.computeEffectiveFriction(wheel.normalLoad, isHandbrakingRear);
      const maxForce = effectiveMu * wheel.normalLoad;
      const corneringStiffness = isHandbrakingRear
        ? this.config.handbrakeCorneringStiffness
        : this.config.corneringStiffness;

      wheel.slipAngle = Math.atan2(lateralSpeed, Math.max(Math.abs(longitudinalSpeed), 0.8));
      wheel.longitudinalSlip = THREE.MathUtils.clamp(
        (wheel.angularVelocity * this.config.wheelRadius - longitudinalSpeed) /
          Math.max(Math.abs(longitudinalSpeed), 3),
        -2.5,
        2.5,
      );

      const longitudinalDemand = signedTireCurve(
        wheel.longitudinalSlip,
        this.config.longitudinalPeakSlip,
        this.config.longitudinalSlideSlip,
        this.config.longitudinalSlideGrip,
      );
      const longitudinalLinear = THREE.MathUtils.clamp(
        wheel.longitudinalSlip * this.config.longitudinalStiffness,
        -1,
        1,
      );
      let longitudinalForce =
        keepSmallerMagnitude(longitudinalDemand, longitudinalLinear) * maxForce;

      const camberSlipBias = wheel.camber * this.config.camberStiffness;
      const lateralDemand = signedTireCurve(
        -wheel.slipAngle + camberSlipBias,
        this.config.lateralPeakSlipAngle,
        this.config.lateralSlideSlipAngle,
        this.config.lateralSlideGrip,
      );
      const lateralLinear = THREE.MathUtils.clamp(
        -wheel.slipAngle * corneringStiffness + camberSlipBias,
        -1,
        1,
      );
      let lateralForce = keepSmallerMagnitude(lateralDemand, lateralLinear) * maxForce;

      const driveSlip = config.isDriven
        ? THREE.MathUtils.clamp(
            (Math.abs(wheel.longitudinalSlip) - this.config.longitudinalPeakSlip) /
              Math.max(
                this.config.longitudinalSlideSlip - this.config.longitudinalPeakSlip,
                0.001,
              ),
            0,
            1,
          )
        : 0;
      const lateralPowerLoss =
        config.isDriven && input.throttle
          ? 1 - this.config.rearPowerOversteer * driveSlip
          : 1;
      const longitudinalUsage = Math.abs(longitudinalForce) / Math.max(maxForce, 1);
      const lateralLimit =
        maxForce *
        lateralPowerLoss *
        Math.sqrt(
          Math.max(
            0,
            1 -
              Math.min(longitudinalUsage * this.config.tireForceCoupling, 1) ** 2,
          ),
        );
      lateralForce = THREE.MathUtils.clamp(lateralForce, -lateralLimit, lateralLimit);

      const combinedMagnitude = Math.hypot(longitudinalForce, lateralForce);
      if (combinedMagnitude > maxForce && combinedMagnitude > 0) {
        const scale = maxForce / combinedMagnitude;
        longitudinalForce *= scale;
        lateralForce *= scale;
      }

      wheel.longitudinalForce = longitudinalForce;
      wheel.lateralForce = lateralForce;
      wheel.tractionUsage = THREE.MathUtils.clamp(
        Math.hypot(longitudinalForce, lateralForce) / Math.max(maxForce, 1),
        0,
        2,
      );

      const tireForce = axes.forward
        .clone()
        .multiplyScalar(longitudinalForce)
        .add(axes.side.clone().multiplyScalar(lateralForce));
      this.body.addForceAtPoint(vectorToRapier(tireForce), wheel.contactPoint, true);

      const driveTorque = config.isDriven
        ? this.computeDriveTorque(
            input,
            shouldReverse,
            wheel,
            drivenAngularVelocity,
            drivenWheelCount,
          )
        : 0;
      const brakeTorque = this.computeBrakeTorque(config, shouldBrake, input.handbrake);
      const tireTorque = -longitudinalForce * this.config.wheelRadius;
      const rollingTorque = -wheel.angularVelocity * this.config.rollingResistance;
      wheel.driveTorque = driveTorque;
      wheel.brakeTorque = brakeTorque;

      wheel.angularVelocity +=
        ((driveTorque + tireTorque + rollingTorque) / this.config.wheelInertia) *
        dt;
      wheel.angularVelocity = this.applyBrakeToAngularVelocity(
        wheel.angularVelocity,
        brakeTorque,
        dt,
      );

      if (brakeTorque > 0 && Math.abs(longitudinalSpeed) < 0.35 && Math.abs(wheel.angularVelocity) < 1) {
        wheel.angularVelocity = 0;
      }
    }
  }

  private updateDrivetrain(
    input: InputState,
    shouldReverse: boolean,
    drivenAngularVelocity: number,
    dt: number,
  ) {
    const wheelRpm = Math.abs(radPerSecondToRpm(drivenAngularVelocity));
    if (shouldReverse) {
      this.gearDisplay = -1;
      const reverseRpm =
        wheelRpm * this.config.reverseGearRatio * this.config.finalDriveRatio;
      this.engineRpm = this.smoothEngineRpm(reverseRpm, dt);
      return;
    }

    this.gearDisplay = this.currentGearIndex + 1;
    let ratio = this.getCurrentGearRatio();
    let connectedRpm = wheelRpm * ratio * this.config.finalDriveRatio;
    if (
      input.throttle &&
      this.currentGearIndex < this.config.gearRatios.length - 1 &&
      connectedRpm > this.config.shiftUpRpm
    ) {
      this.currentGearIndex += 1;
      this.gearDisplay = this.currentGearIndex + 1;
      ratio = this.getCurrentGearRatio();
      connectedRpm = wheelRpm * ratio * this.config.finalDriveRatio;
    } else if (
      this.currentGearIndex > 0 &&
      connectedRpm < this.config.shiftDownRpm
    ) {
      this.currentGearIndex -= 1;
      this.gearDisplay = this.currentGearIndex + 1;
      ratio = this.getCurrentGearRatio();
      connectedRpm = wheelRpm * ratio * this.config.finalDriveRatio;
    }

    this.engineRpm = this.smoothEngineRpm(connectedRpm, dt);
  }

  private smoothEngineRpm(connectedRpm: number, dt: number) {
    const targetRpm = THREE.MathUtils.clamp(
      Math.max(this.config.idleRpm, connectedRpm),
      this.config.idleRpm,
      this.config.redlineRpm + 450,
    );
    const response = 1 - Math.exp(-dt * 16);
    return THREE.MathUtils.lerp(this.engineRpm, targetRpm, response);
  }

  private computeDriveTorque(
    input: InputState,
    shouldReverse: boolean,
    wheel: WheelRuntime,
    drivenAngularVelocity: number,
    drivenWheelCount: number,
  ) {
    const differentialTorque =
      (drivenAngularVelocity - wheel.angularVelocity) *
      this.config.differentialLock *
      this.config.wheelInertia *
      20;

    if (shouldReverse) {
      const reverseWheelTorque =
        -this.config.reverseTorque *
        this.config.reverseGearRatio *
        this.config.finalDriveRatio *
        this.config.drivetrainEfficiency;
      return reverseWheelTorque / drivenWheelCount + differentialTorque;
    }

    const gearTorqueMultiplier =
      this.getCurrentGearRatio() *
      this.config.finalDriveRatio *
      this.config.drivetrainEfficiency;

    if (!input.throttle) {
      const brakingDirection = Math.sign(wheel.angularVelocity);
      return (
        -brakingDirection *
          this.config.engineBrakingTorque *
          gearTorqueMultiplier /
          drivenWheelCount +
        differentialTorque
      );
    }

    const limiter =
      this.engineRpm <= this.config.redlineRpm
        ? 1
        : THREE.MathUtils.clamp(
            1 - (this.engineRpm - this.config.redlineRpm) / 450,
            0,
            1,
          );
    const crankTorque =
      this.config.engineTorque * engineTorqueCurve(this.engineRpm, this.config) * limiter;
    return (crankTorque * gearTorqueMultiplier) / drivenWheelCount + differentialTorque;
  }

  private getCurrentGearRatio() {
    return this.config.gearRatios[
      THREE.MathUtils.clamp(this.currentGearIndex, 0, this.config.gearRatios.length - 1)
    ];
  }

  private applyBrakeToAngularVelocity(angularVelocity: number, brakeTorque: number, dt: number) {
    if (brakeTorque <= 0 || angularVelocity === 0) {
      return angularVelocity;
    }

    const brakeDelta = (brakeTorque / this.config.wheelInertia) * dt;
    if (Math.abs(angularVelocity) <= brakeDelta) {
      return 0;
    }

    return angularVelocity - Math.sign(angularVelocity) * brakeDelta;
  }

  private computeBrakeTorque(wheel: WheelConfig, shouldBrake: boolean, handbrake: boolean) {
    const frontMultiplier = wheel.isFront ? this.config.brakeBias * 2 : (1 - this.config.brakeBias) * 2;
    const serviceBrake = shouldBrake ? this.config.brakeTorque * frontMultiplier : 0;
    const handbrakeTorque = handbrake && !wheel.isFront ? this.config.handbrakeTorque : 0;
    return serviceBrake + handbrakeTorque;
  }

  private computeEffectiveFriction(normalLoad: number, isHandbrakingRear: boolean) {
    const staticLoad = (this.config.mass * 9.81) / 4;
    const loadRatio = (normalLoad - staticLoad) / staticLoad;
    const loadAdjustedMu =
      this.config.frictionCoefficient *
      THREE.MathUtils.clamp(1 - this.config.loadSensitivity * loadRatio, 0.78, 1.12);
    return isHandbrakingRear
      ? Math.min(loadAdjustedMu, this.config.handbrakeFrictionCoefficient)
      : loadAdjustedMu;
  }

  private getAckermannSteer(wheel: WheelConfig) {
    if (!wheel.isFront || this.steeringAngle === 0) {
      return 0;
    }

    const steeringSign = Math.sign(this.steeringAngle);
    const insideWheel = wheel.isLeft ? steeringSign > 0 : steeringSign < 0;
    const ackermannScale = insideWheel ? 1 + this.config.ackermann : 1 - this.config.ackermann;
    return this.steeringAngle * ackermannScale;
  }

  private getWheelAxes(steeringAngle: number) {
    const steeringRotation = new THREE.Quaternion().setFromAxisAngle(this.up, steeringAngle);
    const wheelForward = this.forward.clone().applyQuaternion(steeringRotation).normalize();
    const wheelSide = this.side.clone().applyQuaternion(steeringRotation).normalize();
    return { forward: wheelForward, side: wheelSide };
  }

  private computeCamber(suspensionLength: number) {
    const rest = this.config.suspensionRestLength;
    if (suspensionLength <= rest) {
      const t = THREE.MathUtils.clamp(
        (rest - suspensionLength) / this.config.suspensionBumpTravel,
        0,
        1,
      );
      return THREE.MathUtils.lerp(this.config.camberAtRest, this.config.camberAtBump, t);
    }

    const t = THREE.MathUtils.clamp(
      (suspensionLength - rest) / this.config.suspensionDroopTravel,
      0,
      1,
    );
    return THREE.MathUtils.lerp(this.config.camberAtRest, this.config.camberAtDroop, t);
  }

  private localToWorld(local: THREE.Vector3) {
    return local.clone().applyQuaternion(this.bodyQuaternion).add(this.getPosition());
  }
}

function rayPointAt(origin: THREE.Vector3, direction: THREE.Vector3, distance: number) {
  return origin.clone().add(direction.clone().multiplyScalar(distance));
}

function signedTireCurve(
  value: number,
  peakValue: number,
  slideValue: number,
  slideGrip: number,
) {
  if (value === 0) {
    return 0;
  }

  const sign = Math.sign(value);
  const magnitude = Math.abs(value);
  const peak = Math.max(peakValue, 0.001);
  const slide = Math.max(slideValue, peak + 0.001);

  if (magnitude <= peak) {
    return sign * smootherStep(magnitude / peak);
  }

  const slideT = THREE.MathUtils.clamp((magnitude - peak) / (slide - peak), 0, 1);
  return sign * THREE.MathUtils.lerp(1, slideGrip, smootherStep(slideT));
}

function keepSmallerMagnitude(curveValue: number, linearValue: number) {
  return Math.sign(curveValue) * Math.min(Math.abs(curveValue), Math.abs(linearValue));
}

function smootherStep(value: number) {
  const t = THREE.MathUtils.clamp(value, 0, 1);
  return t * t * t * (t * (t * 6 - 15) + 10);
}

function radPerSecondToRpm(value: number) {
  return (value * 60) / (Math.PI * 2);
}

function engineTorqueCurve(rpm: number, config: VehicleConfig) {
  const normalized = THREE.MathUtils.clamp(
    (rpm - config.idleRpm) / Math.max(config.redlineRpm - config.idleRpm, 1),
    0,
    1,
  );

  if (normalized < 0.28) {
    return THREE.MathUtils.lerp(0.68, 0.98, smootherStep(normalized / 0.28));
  }

  if (normalized < 0.72) {
    return THREE.MathUtils.lerp(0.98, 1.08, smootherStep((normalized - 0.28) / 0.44));
  }

  return THREE.MathUtils.lerp(1.08, 0.72, smootherStep((normalized - 0.72) / 0.28));
}

function vectorToRapier(vector: THREE.Vector3) {
  return { x: vector.x, y: vector.y, z: vector.z };
}
