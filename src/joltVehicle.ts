import initJolt from "jolt-physics";
import * as THREE from "three";

export type InputState = {
  throttle: boolean;
  brake: boolean;
  steerLeft: boolean;
  steerRight: boolean;
  handbrake: boolean;
};

export type JoltWheelConfig = {
  name: "frontLeft" | "frontRight" | "rearLeft" | "rearRight";
  localPosition: THREE.Vector3;
  isFront: boolean;
  isLeft: boolean;
  driven: boolean;
  handbrake: boolean;
};

export type JoltVehicleConfig = {
  mass: number;
  chassisStartHeight: number;
  chassisHalfExtents: THREE.Vector3;
  centerOfMassOffset: THREE.Vector3;
  wheelRadius: number;
  wheelWidth: number;
  suspensionMinLength: number;
  suspensionMaxLength: number;
  springStiffness: number;
  springDamping: number;
  maxSteerAngle: number;
  maxEngineTorque: number;
  minRpm: number;
  maxRpm: number;
  shiftUpRpm: number;
  shiftDownRpm: number;
  clutchStrength: number;
  gearRatios: number[];
  reverseGearRatios: number[];
  finalDriveRatio: number;
  limitedSlipRatio: number;
  brakeTorque: number;
  handbrakeTorque: number;
  tireFrictionScale: number;
  wheels: JoltWheelConfig[];
};

export type JoltWheelTelemetry = {
  name: JoltWheelConfig["name"];
  isFront: boolean;
  isLeft: boolean;
  contact: boolean;
  suspensionLength: number;
  angularVelocity: number;
  rotationAngle: number;
  steeringAngle: number;
  longitudinalSlip: number;
  lateralSlip: number;
  longitudinalFriction: number;
  lateralFriction: number;
};

export type JoltVehicleTelemetry = {
  speedKmh: number;
  signedSpeedKmh: number;
  engineRpm: number;
  gear: number;
  headingDegrees: number;
  steeringDegrees: number;
  slipPercent: number;
  pitchDegrees: number;
  rollDegrees: number;
  wheels: JoltWheelTelemetry[];
};

type JoltApi = Awaited<ReturnType<typeof initJolt>>;
type JoltInterface = InstanceType<JoltApi["JoltInterface"]>;
type PhysicsSystem = InstanceType<JoltApi["PhysicsSystem"]>;
type BodyInterface = InstanceType<JoltApi["BodyInterface"]>;
type Body = InstanceType<JoltApi["Body"]>;
type VehicleConstraint = InstanceType<JoltApi["VehicleConstraint"]>;
type WheeledVehicleController = InstanceType<JoltApi["WheeledVehicleController"]>;
type VehicleConstraintStepListener = InstanceType<JoltApi["VehicleConstraintStepListener"]>;

const NON_MOVING_LAYER = 0;
const MOVING_LAYER = 1;
const NUM_OBJECT_LAYERS = 2;
const NON_MOVING_BP_LAYER = 0;
const MOVING_BP_LAYER = 1;
const NUM_BROAD_PHASE_LAYERS = 2;
const RL_WHEEL = 2;
const RR_WHEEL = 3;
const ZERO_INPUT: InputState = {
  throttle: false,
  brake: false,
  steerLeft: false,
  steerRight: false,
  handbrake: false,
};

export const FIXED_TIMESTEP = 1 / 60;
export const WORLD_SIZE = 480;

// Coordinate convention: chassis front is local +Z, up is local +Y, and
// vehicle-left is local +X. This matches the Three visual model.
export const defaultJoltVehicleConfig: JoltVehicleConfig = {
  mass: 3100,
  chassisStartHeight: 1.55,
  chassisHalfExtents: new THREE.Vector3(0.95, 0.34, 1.65),
  centerOfMassOffset: new THREE.Vector3(0, -0.42, 0.06),
  wheelRadius: 0.38,
  wheelWidth: 0.32,
  suspensionMinLength: 0.22,
  suspensionMaxLength: 1.05,
  springStiffness: 27_000,
  springDamping: 4_800,
  maxSteerAngle: THREE.MathUtils.degToRad(33),
  maxEngineTorque: 980,
  minRpm: 850,
  maxRpm: 6_200,
  shiftUpRpm: 5_350,
  shiftDownRpm: 2_000,
  clutchStrength: 15,
  gearRatios: [3.2, 1.78, 1.16],
  reverseGearRatios: [-2.9],
  finalDriveRatio: 3.9,
  limitedSlipRatio: 1.65,
  brakeTorque: 2_150,
  handbrakeTorque: 4_800,
  tireFrictionScale: 1.06,
  wheels: [
    {
      name: "frontLeft",
      localPosition: new THREE.Vector3(0.96, -0.08, 1.45),
      isFront: true,
      isLeft: true,
      driven: false,
      handbrake: false,
    },
    {
      name: "frontRight",
      localPosition: new THREE.Vector3(-0.96, -0.08, 1.45),
      isFront: true,
      isLeft: false,
      driven: false,
      handbrake: false,
    },
    {
      name: "rearLeft",
      localPosition: new THREE.Vector3(0.96, -0.08, -1.32),
      isFront: false,
      isLeft: true,
      driven: true,
      handbrake: true,
    },
    {
      name: "rearRight",
      localPosition: new THREE.Vector3(-0.96, -0.08, -1.32),
      isFront: false,
      isLeft: false,
      driven: true,
      handbrake: true,
    },
  ],
};

export class JoltDrivingWorld {
  constructor(
    readonly Jolt: JoltApi,
    readonly jolt: JoltInterface,
    readonly physicsSystem: PhysicsSystem,
    readonly bodyInterface: BodyInterface,
    private readonly retainedObjects: unknown[],
  ) {}

  step(dt: number) {
    this.jolt.Step(dt, 2);
  }

  retain(object: unknown) {
    this.retainedObjects.push(object);
  }
}

export async function createJoltDrivingWorld() {
  const Jolt = await initJolt();
  const bpLayerInterface = new Jolt.BroadPhaseLayerInterfaceTable(
    NUM_OBJECT_LAYERS,
    NUM_BROAD_PHASE_LAYERS,
  );
  bpLayerInterface.MapObjectToBroadPhaseLayer(
    NON_MOVING_LAYER,
    new Jolt.BroadPhaseLayer(NON_MOVING_BP_LAYER),
  );
  bpLayerInterface.MapObjectToBroadPhaseLayer(
    MOVING_LAYER,
    new Jolt.BroadPhaseLayer(MOVING_BP_LAYER),
  );

  const objectLayerPairFilter = new Jolt.ObjectLayerPairFilterTable(NUM_OBJECT_LAYERS);
  objectLayerPairFilter.EnableCollision(MOVING_LAYER, NON_MOVING_LAYER);
  objectLayerPairFilter.EnableCollision(MOVING_LAYER, MOVING_LAYER);
  objectLayerPairFilter.DisableCollision(NON_MOVING_LAYER, NON_MOVING_LAYER);

  const objectVsBroadPhaseLayerFilter = new Jolt.ObjectVsBroadPhaseLayerFilterTable(
    bpLayerInterface,
    NUM_BROAD_PHASE_LAYERS,
    objectLayerPairFilter,
    NUM_OBJECT_LAYERS,
  );

  const settings = new Jolt.JoltSettings();
  settings.mMaxBodies = 2048;
  settings.mMaxBodyPairs = 4096;
  settings.mMaxContactConstraints = 4096;
  settings.mBroadPhaseLayerInterface = bpLayerInterface;
  settings.mObjectLayerPairFilter = objectLayerPairFilter;
  settings.mObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter;

  const jolt = new Jolt.JoltInterface(settings);
  const physicsSystem = jolt.GetPhysicsSystem();
  physicsSystem.SetGravity(new Jolt.Vec3(0, -9.81, 0));
  const physicsSettings = physicsSystem.GetPhysicsSettings();
  physicsSettings.mNumVelocitySteps = 24;
  physicsSettings.mNumPositionSteps = 4;

  const world = new JoltDrivingWorld(Jolt, jolt, physicsSystem, physicsSystem.GetBodyInterface(), [
    bpLayerInterface,
    objectLayerPairFilter,
    objectVsBroadPhaseLayerFilter,
    settings,
  ]);
  createGroundBody(world);
  physicsSystem.OptimizeBroadPhase();
  return world;
}

export class JoltVehiclePhysics {
  private readonly body: Body;
  private readonly constraint: VehicleConstraint;
  private readonly controller: WheeledVehicleController;
  private readonly stepListener: VehicleConstraintStepListener;
  private previousForward = 1;

  constructor(
    private readonly world: JoltDrivingWorld,
    readonly config: JoltVehicleConfig = defaultJoltVehicleConfig,
  ) {
    this.body = this.createBody();
    const vehicleSettings = this.createVehicleSettings();
    this.constraint = new world.Jolt.VehicleConstraint(this.body, vehicleSettings);
    this.constraint.SetNumStepsBetweenCollisionTestActive(1);
    this.constraint.SetNumStepsBetweenCollisionTestInactive(1);
    this.constraint.SetVehicleCollisionTester(
      new world.Jolt.VehicleCollisionTesterCastCylinder(MOVING_LAYER, 0.08),
    );
    world.physicsSystem.AddConstraint(this.constraint);
    this.stepListener = new world.Jolt.VehicleConstraintStepListener(this.constraint);
    world.physicsSystem.AddStepListener(this.stepListener);
    this.controller = world.Jolt.castObject(
      this.constraint.GetController(),
      world.Jolt.WheeledVehicleController,
    );
    world.retain(vehicleSettings);
    world.retain(this.constraint);
    world.retain(this.stepListener);
  }

  updateInput(input: InputState = ZERO_INPUT) {
    const localSpeed = this.getLocalVelocity();
    let forward = input.throttle ? 1 : input.brake ? -1 : 0;
    let brake = 0;
    const right = Number(input.steerRight) - Number(input.steerLeft);
    const handbrake = input.handbrake ? 1 : 0;

    if (this.previousForward * forward < 0) {
      if ((forward > 0 && localSpeed.z < -0.15) || (forward < 0 && localSpeed.z > 0.15)) {
        forward = 0;
        brake = 1;
      } else {
        this.previousForward = forward || this.previousForward;
      }
    }

    if (input.handbrake) {
      brake = Math.max(brake, 0.15);
    }

    this.controller.SetDriverInput(forward, right, brake, handbrake);
    if (forward !== 0 || right !== 0 || brake !== 0 || handbrake !== 0) {
      this.world.bodyInterface.ActivateBody(this.body.GetID());
    }
  }

  getPosition() {
    return toThreeVector(this.body.GetPosition());
  }

  getQuaternion() {
    return toThreeQuaternion(this.body.GetRotation());
  }

  getWheelLocalTransform(index: number) {
    const isLeft = this.config.wheels[index]?.isLeft ?? false;
    const wheelRight = isLeft
      ? new this.world.Jolt.Vec3(0, -1, 0)
      : new this.world.Jolt.Vec3(0, 1, 0);
    const wheelUp = new this.world.Jolt.Vec3(1, 0, 0);
    const transform = this.constraint.GetWheelLocalTransform(index, wheelRight, wheelUp);

    return {
      position: toThreeVector(transform.GetTranslation()),
      quaternion: toThreeQuaternion(transform.GetQuaternion()),
    };
  }

  getTelemetry(): JoltVehicleTelemetry {
    const velocity = this.getLocalVelocity();
    const forwardSpeed = velocity.z;
    const lateralSpeed = velocity.x;
    const rotation = this.getQuaternion();
    const euler = new THREE.Euler().setFromQuaternion(rotation, "YXZ");
    const engine = this.controller.GetEngine();
    const transmission = this.controller.GetTransmission();
    const wheelTelemetry = this.config.wheels.map((wheelConfig, index) => {
      const wheel = this.constraint.GetWheel(index);
      const wv = this.world.Jolt.castObject(wheel, this.world.Jolt.WheelWV);
      return {
        name: wheelConfig.name,
        isFront: wheelConfig.isFront,
        isLeft: wheelConfig.isLeft,
        contact: wheel.HasContact(),
        suspensionLength: wheel.GetSuspensionLength(),
        angularVelocity: wheel.GetAngularVelocity(),
        rotationAngle: wheel.GetRotationAngle(),
        steeringAngle: wheel.GetSteerAngle(),
        longitudinalSlip: wv.mLongitudinalSlip,
        lateralSlip: wv.mLateralSlip,
        longitudinalFriction: wv.mCombinedLongitudinalFriction,
        lateralFriction: wv.mCombinedLateralFriction,
      };
    });
    const frontWheelSteering = wheelTelemetry.filter((wheel) => wheel.isFront);
    const signedSteering =
      frontWheelSteering.length > 0
        ? frontWheelSteering.reduce((sum, wheel) => sum + wheel.steeringAngle, 0) /
          frontWheelSteering.length
        : 0;

    return {
      speedKmh: Math.abs(forwardSpeed) * 3.6,
      signedSpeedKmh: forwardSpeed * 3.6,
      engineRpm: engine.GetCurrentRPM(),
      gear: transmission.GetCurrentGear(),
      headingDegrees: THREE.MathUtils.radToDeg(Math.atan2(rotationForward(rotation).x, rotationForward(rotation).z)),
      steeringDegrees: THREE.MathUtils.radToDeg(signedSteering),
      slipPercent: Math.round(
        THREE.MathUtils.clamp(Math.abs(lateralSpeed) / Math.max(Math.abs(forwardSpeed), 4), 0, 1) *
          100,
      ),
      pitchDegrees: THREE.MathUtils.radToDeg(euler.x),
      rollDegrees: THREE.MathUtils.radToDeg(euler.z),
      wheels: wheelTelemetry,
    };
  }

  private createBody() {
    const Jolt = this.world.Jolt;
    const baseShapeSettings = new Jolt.BoxShapeSettings(
      toJoltVec3(Jolt, this.config.chassisHalfExtents),
      0.04,
    );
    const comShapeSettings = new Jolt.OffsetCenterOfMassShapeSettings(
      toJoltVec3(Jolt, this.config.centerOfMassOffset),
      baseShapeSettings,
    );
    const shape = getShapeFromResult(comShapeSettings.Create());
    const bodySettings = new Jolt.BodyCreationSettings(
      shape,
      new Jolt.RVec3(0, this.config.chassisStartHeight, 0),
      new Jolt.Quat(0, 0, 0, 1),
      Jolt.EMotionType_Dynamic,
      MOVING_LAYER,
    );
    bodySettings.mOverrideMassProperties = Jolt.EOverrideMassProperties_CalculateInertia;
    bodySettings.mMassPropertiesOverride.mMass = this.config.mass;
    bodySettings.mFriction = 0.82;
    bodySettings.mRestitution = 0.02;
    bodySettings.mLinearDamping = 0.03;
    bodySettings.mAngularDamping = 0.22;
    bodySettings.mAllowSleeping = false;
    bodySettings.mMotionQuality = Jolt.EMotionQuality_LinearCast;
    bodySettings.mNumVelocityStepsOverride = 30;
    bodySettings.mNumPositionStepsOverride = 6;

    const body = this.world.bodyInterface.CreateBody(bodySettings);
    this.world.bodyInterface.AddBody(body.GetID(), Jolt.EActivation_Activate);
    this.world.retain(baseShapeSettings);
    this.world.retain(comShapeSettings);
    this.world.retain(shape);
    this.world.retain(bodySettings);
    return body;
  }

  private createVehicleSettings() {
    const Jolt = this.world.Jolt;
    const settings = new Jolt.VehicleConstraintSettings();
    settings.mUp = new Jolt.Vec3(0, 1, 0);
    settings.mForward = new Jolt.Vec3(0, 0, 1);
    settings.mMaxPitchRollAngle = Math.PI;
    settings.mWheels.clear();
    settings.mAntiRollBars.clear();

    for (const wheelConfig of this.config.wheels) {
      const wheel = new Jolt.WheelSettingsWV();
      wheel.mPosition = toJoltVec3(Jolt, wheelConfig.localPosition);
      wheel.mSuspensionForcePoint = toJoltVec3(Jolt, wheelConfig.localPosition);
      wheel.mSuspensionDirection = new Jolt.Vec3(0, -1, 0);
      wheel.mSteeringAxis = new Jolt.Vec3(0, 1, 0);
      wheel.mWheelUp = new Jolt.Vec3(0, 1, 0);
      wheel.mWheelForward = new Jolt.Vec3(0, 0, 1);
      wheel.mEnableSuspensionForcePoint = true;
      wheel.mRadius = this.config.wheelRadius;
      wheel.mWidth = this.config.wheelWidth;
      wheel.mSuspensionMinLength = this.config.suspensionMinLength;
      wheel.mSuspensionMaxLength = this.config.suspensionMaxLength;
      wheel.mSuspensionPreloadLength = 0.12;
      wheel.mSuspensionSpring.mMode = Jolt.ESpringMode_StiffnessAndDamping;
      wheel.mSuspensionSpring.mStiffness = this.config.springStiffness;
      wheel.mSuspensionSpring.mDamping = this.config.springDamping;
      wheel.mInertia = 1.25;
      wheel.mAngularDamping = 0.18;
      wheel.mMaxSteerAngle = wheelConfig.isFront ? this.config.maxSteerAngle : 0;
      wheel.mMaxBrakeTorque = this.config.brakeTorque;
      wheel.mMaxHandBrakeTorque = wheelConfig.handbrake ? this.config.handbrakeTorque : 0;
      configureTireCurves(wheel, this.config.tireFrictionScale);
      settings.mWheels.push_back(wheel);
      this.world.retain(wheel);
    }

    const controllerSettings = new Jolt.WheeledVehicleControllerSettings();
    controllerSettings.mEngine.mMaxTorque = this.config.maxEngineTorque;
    controllerSettings.mEngine.mMinRPM = this.config.minRpm;
    controllerSettings.mEngine.mMaxRPM = this.config.maxRpm;
    controllerSettings.mEngine.mInertia = 0.62;
    controllerSettings.mEngine.mAngularDamping = 0.18;
    controllerSettings.mTransmission.mMode = Jolt.ETransmissionMode_Auto;
    controllerSettings.mTransmission.mGearRatios.clear();
    for (const ratio of this.config.gearRatios) {
      controllerSettings.mTransmission.mGearRatios.push_back(ratio);
    }
    controllerSettings.mTransmission.mReverseGearRatios.clear();
    for (const ratio of this.config.reverseGearRatios) {
      controllerSettings.mTransmission.mReverseGearRatios.push_back(ratio);
    }
    controllerSettings.mTransmission.mShiftUpRPM = this.config.shiftUpRpm;
    controllerSettings.mTransmission.mShiftDownRPM = this.config.shiftDownRpm;
    controllerSettings.mTransmission.mSwitchTime = 0.35;
    controllerSettings.mTransmission.mSwitchLatency = 0.18;
    controllerSettings.mTransmission.mClutchReleaseTime = 0.2;
    controllerSettings.mTransmission.mClutchStrength = this.config.clutchStrength;
    controllerSettings.mDifferentials.clear();
    controllerSettings.mDifferentialLimitedSlipRatio = this.config.limitedSlipRatio;

    const rearDifferential = new Jolt.VehicleDifferentialSettings();
    rearDifferential.mLeftWheel = RL_WHEEL;
    rearDifferential.mRightWheel = RR_WHEEL;
    rearDifferential.mDifferentialRatio = this.config.finalDriveRatio;
    rearDifferential.mLeftRightSplit = 0.5;
    rearDifferential.mLimitedSlipRatio = this.config.limitedSlipRatio;
    rearDifferential.mEngineTorqueRatio = 1;
    controllerSettings.mDifferentials.push_back(rearDifferential);
    settings.mController = controllerSettings;

    this.world.retain(controllerSettings);
    this.world.retain(rearDifferential);
    return settings;
  }

  private getLocalVelocity() {
    const velocity = this.body.GetLinearVelocity();
    const worldVelocity = toThreeVector(velocity);
    const inverseRotation = this.getQuaternion().invert();
    return worldVelocity.applyQuaternion(inverseRotation);
  }
}

function createGroundBody(world: JoltDrivingWorld) {
  const Jolt = world.Jolt;
  const shapeSettings = new Jolt.BoxShapeSettings(
    new Jolt.Vec3(WORLD_SIZE / 2, 0.1, WORLD_SIZE / 2),
    0.02,
  );
  const shape = getShapeFromResult(shapeSettings.Create());
  const bodySettings = new Jolt.BodyCreationSettings(
    shape,
    new Jolt.RVec3(0, -0.1, 0),
    new Jolt.Quat(0, 0, 0, 1),
    Jolt.EMotionType_Static,
    NON_MOVING_LAYER,
  );
  bodySettings.mFriction = 1.25;
  bodySettings.mRestitution = 0.02;
  const body = world.bodyInterface.CreateBody(bodySettings);
  world.bodyInterface.AddBody(body.GetID(), Jolt.EActivation_DontActivate);
  world.retain(shapeSettings);
  world.retain(shape);
  world.retain(bodySettings);
  world.retain(body);
}

function configureTireCurves(wheel: InstanceType<JoltApi["WheelSettingsWV"]>, scale: number) {
  wheel.mLongitudinalFriction.Clear();
  wheel.mLongitudinalFriction.AddPoint(-1.5, 0.72 * scale);
  wheel.mLongitudinalFriction.AddPoint(-0.18, 1.16 * scale);
  wheel.mLongitudinalFriction.AddPoint(0, 1.05 * scale);
  wheel.mLongitudinalFriction.AddPoint(0.18, 1.16 * scale);
  wheel.mLongitudinalFriction.AddPoint(1.5, 0.72 * scale);
  wheel.mLongitudinalFriction.Sort();

  wheel.mLateralFriction.Clear();
  wheel.mLateralFriction.AddPoint(-35, 0.58 * scale);
  wheel.mLateralFriction.AddPoint(-10, 1.08 * scale);
  wheel.mLateralFriction.AddPoint(0, 1.0 * scale);
  wheel.mLateralFriction.AddPoint(10, 1.08 * scale);
  wheel.mLateralFriction.AddPoint(35, 0.58 * scale);
  wheel.mLateralFriction.Sort();
}

function getShapeFromResult(result: InstanceType<JoltApi["ShapeResult"]>) {
  if (!result.IsValid()) {
    throw new Error(`Failed to create Jolt shape: ${result.GetError().c_str()}`);
  }
  return result.Get();
}

function toJoltVec3(Jolt: JoltApi, value: THREE.Vector3) {
  return new Jolt.Vec3(value.x, value.y, value.z);
}

function toThreeVector(
  value:
    | InstanceType<JoltApi["Vec3"]>
    | InstanceType<JoltApi["RVec3"]>,
) {
  return new THREE.Vector3(value.GetX(), value.GetY(), value.GetZ());
}

function toThreeQuaternion(value: InstanceType<JoltApi["Quat"]>) {
  return new THREE.Quaternion(value.GetX(), value.GetY(), value.GetZ(), value.GetW());
}

function rotationForward(rotation: THREE.Quaternion) {
  return new THREE.Vector3(0, 0, 1).applyQuaternion(rotation).normalize();
}
