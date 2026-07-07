import RAPIER from "@dimforge/rapier3d-compat";
import * as THREE from "three";
import "./style.css";
import {
  FIXED_TIMESTEP,
  WORLD_SIZE,
  createDrivingWorld,
  defaultVehicleConfig,
  type InputState,
  type VehicleConfig,
  type VehicleTelemetry,
  type WheelTelemetry,
  VehiclePhysics,
} from "./vehiclePhysics";

type WheelVisual = {
  root: THREE.Group;
  steerPivot: THREE.Group;
  spinPivot: THREE.Group;
  strut: THREE.Mesh;
};

type TuningControl = {
  label: string;
  unit?: string;
  min: number;
  max: number;
  step: number;
  precision?: number;
  get: () => number;
  set: (value: number) => void;
  afterChange?: () => void;
};

type TuningSection = {
  title: string;
  controls: TuningControl[];
};

const SHOW_TUNING_PANEL = false;
const MAX_FRAME_DELTA = 0.1;
const KEY_TAP_HOLD_MS = 90;

const appElement = document.querySelector<HTMLDivElement>("#app");

if (!appElement) {
  throw new Error("Missing #app root element.");
}

const app = appElement;

const loading = document.createElement("div");
loading.className = "loading";
loading.textContent = "Loading prototype...";
app.append(loading);

const hud = document.createElement("aside");
hud.className = "hud";
hud.innerHTML = `
  <p class="hud__title">Drifting Prototype</p>
  <div class="hud__row">
    <span class="hud__label">Speed</span>
    <span class="hud__value" data-speed>0 km/h</span>
  </div>
  <div class="hud__row">
    <span class="hud__label">Gear</span>
    <span class="hud__value" data-gear>1</span>
  </div>
  <div class="hud__row">
    <span class="hud__label">RPM</span>
    <span class="hud__value" data-rpm>850</span>
  </div>
  <div class="hud__tach" aria-hidden="true">
    <span class="hud__tach-fill" data-tach-fill></span>
  </div>
  <div class="hud__row">
    <span class="hud__label">Slip</span>
    <span class="hud__value" data-slip>0%</span>
  </div>
  <div class="hud__keys" aria-label="Arrow key controls">
    <span class="hud__key hud__key--up">↑</span>
    <span class="hud__key hud__key--left">←</span>
    <span class="hud__key hud__key--down">↓</span>
    <span class="hud__key hud__key--right">→</span>
    <span class="hud__key hud__key--space">SPACE</span>
  </div>
`;
app.append(hud);

const speedValue = hud.querySelector<HTMLElement>("[data-speed]");
const gearValue = hud.querySelector<HTMLElement>("[data-gear]");
const rpmValue = hud.querySelector<HTMLElement>("[data-rpm]");
const tachFill = hud.querySelector<HTMLElement>("[data-tach-fill]");
const slipValue = hud.querySelector<HTMLElement>("[data-slip]");
const devStatusValue = createDevStatusElement(app);
let devAutodriveStage = "off";

const inputState: InputState = {
  throttle: false,
  brake: false,
  steerLeft: false,
  steerRight: false,
  handbrake: false,
};

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.setClearColor(0x8fb2c0);
renderer.domElement.tabIndex = 0;
renderer.domElement.setAttribute("aria-label", "Driving game viewport");
renderer.domElement.addEventListener("pointerdown", () => {
  renderer.domElement.focus();
});
app.append(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x8fb2c0);
scene.fog = new THREE.Fog(0x8fb2c0, 120, 260);

const camera = new THREE.PerspectiveCamera(
  58,
  window.innerWidth / window.innerHeight,
  0.1,
  500,
);
camera.position.set(0, 8, 13);

const sun = new THREE.DirectionalLight(0xffffff, 2.6);
sun.position.set(30, 40, 10);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -80;
sun.shadow.camera.right = 80;
sun.shadow.camera.top = 80;
sun.shadow.camera.bottom = -80;
scene.add(sun);
scene.add(new THREE.HemisphereLight(0xd8f2ff, 0x5e6a62, 1.1));

createFlatWorld(scene);

class VehicleController {
  private readonly group = new THREE.Group();
  private readonly physics: VehiclePhysics;
  private readonly wheelVisuals: WheelVisual[] = [];
  private readonly wheelSpin: number[] = [];
  private telemetry: VehicleTelemetry;

  constructor(world: RAPIER.World, private readonly config: VehicleConfig) {
    this.physics = new VehiclePhysics(world, config);
    this.telemetry = this.physics.getTelemetry();
    this.createVisuals();
    scene.add(this.group);
  }

  updatePhysics(input: InputState, dt: number) {
    this.physics.update(input, dt);
    this.telemetry = this.physics.getTelemetry();
    this.telemetry.wheels.forEach((wheel, index) => {
      this.wheelSpin[index] = (this.wheelSpin[index] ?? 0) + wheel.angularVelocity * dt;
    });
  }

  syncVisuals() {
    const translation = this.physics.getPosition();
    const rotation = this.physics.getQuaternion();
    this.group.position.copy(translation);
    this.group.quaternion.copy(rotation);
    this.syncWheelVisuals(this.telemetry.wheels);
  }

  getPosition() {
    return this.physics.getPosition();
  }

  getQuaternion() {
    return this.physics.getQuaternion();
  }

  getTelemetry() {
    return this.telemetry;
  }

  applyMassProperties() {
    this.physics.applyMassProperties();
  }

  private createVisuals() {
    const chassisGeometry = new THREE.BoxGeometry(2.05, 0.72, 3.35);
    const chassisMaterial = new THREE.MeshStandardMaterial({
      color: 0xd5482f,
      roughness: 0.62,
      metalness: 0.08,
    });
    const chassis = new THREE.Mesh(chassisGeometry, chassisMaterial);
    chassis.castShadow = true;
    chassis.receiveShadow = true;
    chassis.position.y = 0.05;
    this.group.add(chassis);

    const cabin = new THREE.Mesh(
      new THREE.BoxGeometry(1.5, 0.46, 1.24),
      new THREE.MeshStandardMaterial({
        color: 0x384850,
        roughness: 0.5,
        metalness: 0.05,
      }),
    );
    cabin.position.set(0, 0.58, -0.2);
    cabin.castShadow = true;
    this.group.add(cabin);

    const strutMaterial = new THREE.MeshStandardMaterial({
      color: 0x20272b,
      roughness: 0.72,
      metalness: 0.12,
    });

    for (const wheel of this.config.wheels) {
      const visual = createWheelMesh(this.config, strutMaterial);
      visual.root.position.copy(
        wheel.localHardpoint.clone().add(new THREE.Vector3(0, -this.config.suspensionRestLength, 0)),
      );
      this.group.add(visual.strut, visual.root);
      this.wheelVisuals.push(visual);
      this.wheelSpin.push(0);
    }
  }

  private syncWheelVisuals(wheels: WheelTelemetry[]) {
    for (const [index, wheel] of wheels.entries()) {
      const visual = this.wheelVisuals[index];
      visual.root.position.copy(wheel.localWheelCenter);
      visual.steerPivot.rotation.order = "YXZ";
      visual.steerPivot.rotation.y = wheel.steeringAngle;
      visual.steerPivot.rotation.z = wheel.isLeft ? -wheel.camber : wheel.camber;
      visual.spinPivot.rotation.x = this.wheelSpin[index] ?? 0;

      const strutLength = Math.max(
        0.1,
        Math.abs(wheel.localHardpoint.y - wheel.localWheelCenter.y),
      );
      visual.strut.position.set(
        wheel.localHardpoint.x,
        wheel.localHardpoint.y - strutLength / 2,
        wheel.localHardpoint.z,
      );
      visual.strut.scale.set(1, strutLength, 1);
    }
  }
}

function createWheelMesh(config: VehicleConfig, strutMaterial: THREE.Material): WheelVisual {
  const root = new THREE.Group();
  const steerPivot = new THREE.Group();
  const spinPivot = new THREE.Group();
  root.add(steerPivot);
  steerPivot.add(spinPivot);

  const tireMaterial = new THREE.MeshStandardMaterial({
    color: 0x15191c,
    roughness: 0.88,
    metalness: 0.02,
  });
  const hubMaterial = new THREE.MeshStandardMaterial({
    color: 0xc3c9c9,
    roughness: 0.35,
    metalness: 0.45,
  });
  const tire = new THREE.Mesh(
    new THREE.CylinderGeometry(config.wheelRadius, config.wheelRadius, config.wheelWidth, 28),
    tireMaterial,
  );
  const hub = new THREE.Mesh(
    new THREE.CylinderGeometry(
      config.wheelRadius * 0.48,
      config.wheelRadius * 0.48,
      config.wheelWidth + 0.02,
      18,
    ),
    hubMaterial,
  );
  const spokeA = new THREE.Mesh(
    new THREE.BoxGeometry(config.wheelWidth * 0.5, 0.055, config.wheelRadius * 1.45),
    hubMaterial,
  );
  const spokeB = new THREE.Mesh(
    new THREE.BoxGeometry(config.wheelWidth * 0.5, config.wheelRadius * 1.45, 0.055),
    hubMaterial,
  );
  const strut = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 1, 10), strutMaterial);
  tire.castShadow = true;
  tire.receiveShadow = true;
  hub.castShadow = true;
  spokeA.castShadow = true;
  spokeB.castShadow = true;
  strut.castShadow = true;
  tire.rotation.z = Math.PI / 2;
  hub.rotation.z = Math.PI / 2;
  spinPivot.add(tire, hub, spokeA, spokeB);
  return { root, steerPivot, spinPivot, strut };
}

function createFlatWorld(targetScene: THREE.Scene) {
  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(WORLD_SIZE, WORLD_SIZE),
    new THREE.MeshStandardMaterial({
      color: 0x4f675f,
      roughness: 0.94,
      metalness: 0,
    }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  targetScene.add(ground);

  const grid = new THREE.GridHelper(WORLD_SIZE, WORLD_SIZE / 5, 0xd9e2d7, 0x6f857b);
  grid.position.y = 0.012;
  targetScene.add(grid);

  const roadMaterial = new THREE.MeshStandardMaterial({
    color: 0x293235,
    roughness: 0.82,
  });
  const stripeMaterial = new THREE.MeshStandardMaterial({
    color: 0xf2d55c,
    roughness: 0.5,
  });
  const edgeMaterial = new THREE.MeshStandardMaterial({
    color: 0xf5f7f0,
    roughness: 0.65,
  });

  addFlatBox(targetScene, 0, 0.018, 0, 14, 0.02, WORLD_SIZE, roadMaterial);
  addFlatBox(targetScene, 0, 0.022, 0, WORLD_SIZE, 0.02, 14, roadMaterial);

  for (let z = -WORLD_SIZE / 2 + 4; z < WORLD_SIZE / 2; z += 12) {
    addFlatBox(targetScene, -0.32, 0.036, z, 0.16, 0.02, 5.5, stripeMaterial);
    addFlatBox(targetScene, 0.32, 0.036, z, 0.16, 0.02, 5.5, stripeMaterial);
  }

  for (let x = -WORLD_SIZE / 2 + 4; x < WORLD_SIZE / 2; x += 12) {
    addFlatBox(targetScene, x, 0.037, -0.32, 5.5, 0.02, 0.16, stripeMaterial);
    addFlatBox(targetScene, x, 0.037, 0.32, 5.5, 0.02, 0.16, stripeMaterial);
  }

  for (const offset of [-7.2, 7.2]) {
    addFlatBox(targetScene, offset, 0.04, 0, 0.12, 0.02, WORLD_SIZE, edgeMaterial);
    addFlatBox(targetScene, 0, 0.04, offset, WORLD_SIZE, 0.02, 0.12, edgeMaterial);
  }

  const coneMaterial = new THREE.MeshStandardMaterial({
    color: 0xff7b2f,
    roughness: 0.65,
  });
  for (let i = 0; i < 24; i += 1) {
    const angle = (i / 24) * Math.PI * 2;
    const cone = new THREE.Mesh(new THREE.ConeGeometry(0.38, 0.9, 18), coneMaterial);
    cone.position.set(Math.cos(angle) * 27, 0.45, Math.sin(angle) * 27);
    cone.castShadow = true;
    targetScene.add(cone);
  }
}

function addFlatBox(
  targetScene: THREE.Scene,
  x: number,
  y: number,
  z: number,
  width: number,
  height: number,
  depth: number,
  material: THREE.Material,
) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth), material);
  mesh.position.set(x, y, z);
  mesh.receiveShadow = true;
  targetScene.add(mesh);
}

function updateCamera(vehicle: VehicleController, dt: number) {
  const position = vehicle.getPosition();
  const rotation = vehicle.getQuaternion();
  const behind = new THREE.Vector3(0, 4.6, -9.4).applyQuaternion(rotation);
  const targetPosition = position.clone().add(behind);
  const cameraLag = 1 - Math.exp(-dt * 5.2);
  camera.position.lerp(targetPosition, cameraLag);

  const lookAt = position.clone().add(new THREE.Vector3(0, 1.05, 0));
  camera.lookAt(lookAt);
}

function bindInput(targetInput: InputState) {
  const keyMap: Record<string, keyof InputState> = {
    ArrowUp: "throttle",
    ArrowDown: "brake",
    ArrowLeft: "steerLeft",
    ArrowRight: "steerRight",
    Space: "handbrake",
    " ": "handbrake",
  };
  const releaseTimers: Partial<Record<keyof InputState, number>> = {};
  const getAction = (event: KeyboardEvent) => keyMap[event.code] ?? keyMap[event.key];

  window.addEventListener("keydown", (event) => {
    const action = getAction(event);
    if (!action) {
      return;
    }
    event.preventDefault();
    clearTimeout(releaseTimers[action]);
    targetInput[action] = true;
  });

  window.addEventListener("keyup", (event) => {
    const action = getAction(event);
    if (!action) {
      return;
    }
    event.preventDefault();
    clearTimeout(releaseTimers[action]);
    releaseTimers[action] = window.setTimeout(() => {
      targetInput[action] = false;
    }, KEY_TAP_HOLD_MS);
  });
}

function resize() {
  const width = window.innerWidth;
  const height = window.innerHeight;
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  renderer.setSize(width, height);
}

async function start() {
  await initRapier();
  const world = createDrivingWorld();
  const vehicle = new VehicleController(world, defaultVehicleConfig);
  if (SHOW_TUNING_PANEL) {
    app.append(createTuningPanel(defaultVehicleConfig, () => vehicle.applyMassProperties()));
  }
  bindInput(inputState);
  startDevAutodrive();
  loading.classList.add("loading--hidden");

  let accumulator = 0;
  let previousTime = performance.now();

  const frame = (time: number) => {
    const frameDelta = Math.min((time - previousTime) / 1000, MAX_FRAME_DELTA);
    previousTime = time;
    accumulator += frameDelta;

    while (accumulator >= FIXED_TIMESTEP) {
      vehicle.updatePhysics(inputState, FIXED_TIMESTEP);
      world.step();
      accumulator -= FIXED_TIMESTEP;
    }

    vehicle.syncVisuals();
    updateCamera(vehicle, frameDelta);

    const telemetry = vehicle.getTelemetry();
    if (speedValue) {
      speedValue.textContent = `${Math.round(telemetry.speedKmh)} km/h`;
    }
    if (gearValue) {
      gearValue.textContent = telemetry.gear < 0 ? "R" : String(telemetry.gear);
    }
    if (rpmValue) {
      rpmValue.textContent = String(Math.round(telemetry.engineRpm));
    }
    if (tachFill) {
      const rpmRatio = THREE.MathUtils.clamp(
        (telemetry.engineRpm - defaultVehicleConfig.idleRpm) /
          (defaultVehicleConfig.redlineRpm - defaultVehicleConfig.idleRpm),
        0,
        1,
      );
      tachFill.style.transform = `scaleX(${rpmRatio.toFixed(3)})`;
    }
    if (slipValue) {
      slipValue.textContent = `${telemetry.slipPercent}%`;
    }
    if (devStatusValue) {
      const position = vehicle.getPosition();
      devStatusValue.textContent = JSON.stringify({
        input: inputState,
        position: {
          x: Number(position.x.toFixed(2)),
          y: Number(position.y.toFixed(2)),
          z: Number(position.z.toFixed(2)),
        },
        speed: Math.round(telemetry.speedKmh),
        signedSpeed: Math.round(telemetry.signedSpeedKmh),
        rpm: Math.round(telemetry.engineRpm),
        gear: telemetry.gear,
        heading: Number(telemetry.headingDegrees.toFixed(1)),
        steering: Number(telemetry.steeringDegrees.toFixed(1)),
        pitch: Number(telemetry.pitchDegrees.toFixed(1)),
        roll: Number(telemetry.rollDegrees.toFixed(1)),
        suspension: telemetry.wheels.map((wheel) => Number(wheel.suspensionLength.toFixed(2))),
        loads: telemetry.wheels.map((wheel) => Math.round(wheel.normalLoad)),
        tuning: {
          mass: defaultVehicleConfig.mass,
          engineTorque: defaultVehicleConfig.engineTorque,
          gearRatios: defaultVehicleConfig.gearRatios,
          finalDriveRatio: defaultVehicleConfig.finalDriveRatio,
          differentialLock: defaultVehicleConfig.differentialLock,
          springRate: defaultVehicleConfig.springRate,
          bumpDamping: defaultVehicleConfig.bumpDamping,
          reboundDamping: defaultVehicleConfig.reboundDamping,
          bumpStopRate: defaultVehicleConfig.bumpStopRate,
          rearPowerOversteer: defaultVehicleConfig.rearPowerOversteer,
        },
        stage: devAutodriveStage,
      });
    }

    renderer.render(scene, camera);
    requestAnimationFrame(frame);
  };

  requestAnimationFrame(frame);
}

function createTuningPanel(config: VehicleConfig, applyMassProperties: () => void) {
  const panel = document.createElement("aside");
  panel.className = "tuning-panel";
  const sections = createTuningSections(config, applyMassProperties);
  const controls = sections.flatMap((section) => section.controls);
  const initialValues = new Map(controls.map((control) => [control, control.get()]));

  const header = document.createElement("div");
  header.className = "tuning-panel__header";
  const title = document.createElement("h2");
  title.className = "tuning-panel__title";
  title.textContent = "Vehicle Tuning";
  const resetButton = document.createElement("button");
  resetButton.className = "tuning-panel__reset";
  resetButton.type = "button";
  resetButton.textContent = "Reset";
  resetButton.addEventListener("click", () => {
    for (const control of controls) {
      const initialValue = initialValues.get(control);
      if (initialValue === undefined) {
        continue;
      }
      control.set(initialValue);
      control.afterChange?.();
    }
    refreshControls();
  });
  header.append(title, resetButton);
  panel.append(header);

  const controlRows: { control: TuningControl; range: HTMLInputElement; number: HTMLInputElement }[] = [];
  for (const [sectionIndex, section] of sections.entries()) {
    const group = document.createElement("details");
    group.className = "tuning-panel__section";
    group.open = sectionIndex < 3;
    const summary = document.createElement("summary");
    summary.className = "tuning-panel__summary";
    summary.textContent = section.title;
    group.append(summary);

    for (const control of section.controls) {
      const row = createTuningControlRow(control);
      controlRows.push({ control, range: row.range, number: row.number });
      group.append(row.element);
    }
    panel.append(group);
  }

  function refreshControls() {
    for (const { control, range, number } of controlRows) {
      const value = control.get();
      range.value = String(value);
      number.value = formatTuningValue(value, control);
    }
  }

  refreshControls();
  return panel;
}

function createTuningSections(
  config: VehicleConfig,
  applyMassProperties: () => void,
): TuningSection[] {
  const degrees = (getRadians: () => number, setRadians: (value: number) => void) => ({
    get: () => THREE.MathUtils.radToDeg(getRadians()),
    set: (degreesValue: number) => setRadians(THREE.MathUtils.degToRad(degreesValue)),
  });
  const massChange = { afterChange: applyMassProperties };

  return [
    {
      title: "Body",
      controls: [
        numericControl("Mass", "kg", 900, 3_200, 25, () => config.mass, (value) => {
          config.mass = value;
        }, massChange),
        numericControl("COM X", "m", -0.5, 0.5, 0.01, () => config.centerOfMass.x, (value) => {
          config.centerOfMass.x = value;
        }, massChange),
        numericControl("COM Y", "m", -0.65, 0.15, 0.01, () => config.centerOfMass.y, (value) => {
          config.centerOfMass.y = value;
        }, massChange),
        numericControl("COM Z", "m", -0.8, 0.8, 0.01, () => config.centerOfMass.z, (value) => {
          config.centerOfMass.z = value;
        }, massChange),
        numericControl("Pitch inertia", "", 200, 2_500, 10, () => config.principalAngularInertia.x, (value) => {
          config.principalAngularInertia.x = value;
        }, massChange),
        numericControl("Yaw inertia", "", 300, 4_000, 10, () => config.principalAngularInertia.y, (value) => {
          config.principalAngularInertia.y = value;
        }, massChange),
        numericControl("Roll inertia", "", 150, 2_500, 10, () => config.principalAngularInertia.z, (value) => {
          config.principalAngularInertia.z = value;
        }, massChange),
      ],
    },
    {
      title: "Power / Brakes",
      controls: [
        numericControl("Engine torque", "Nm", 250, 3_800, 10, () => config.engineTorque, (value) => {
          config.engineTorque = value;
        }),
        numericControl("Reverse torque", "Nm", 150, 2_600, 10, () => config.reverseTorque, (value) => {
          config.reverseTorque = value;
        }),
        numericControl("Diff lock", "", 0, 1, 0.01, () => config.differentialLock, (value) => {
          config.differentialLock = value;
        }),
        numericControl("Brake torque", "Nm", 300, 5_500, 10, () => config.brakeTorque, (value) => {
          config.brakeTorque = value;
        }),
        numericControl("Handbrake torque", "Nm", 300, 7_000, 10, () => config.handbrakeTorque, (value) => {
          config.handbrakeTorque = value;
        }),
        numericControl("Brake bias", "", 0.45, 0.9, 0.01, () => config.brakeBias, (value) => {
          config.brakeBias = value;
        }),
        numericControl("Wheel inertia", "", 0.4, 5, 0.05, () => config.wheelInertia, (value) => {
          config.wheelInertia = value;
        }),
        numericControl("Rolling resistance", "", 0, 0.5, 0.01, () => config.rollingResistance, (value) => {
          config.rollingResistance = value;
        }),
      ],
    },
    {
      title: "Suspension",
      controls: [
        numericControl("Rest length", "m", 0.25, 0.9, 0.01, () => config.suspensionRestLength, (value) => {
          config.suspensionRestLength = value;
        }),
        numericControl("Bump travel", "m", 0.08, 0.45, 0.01, () => config.suspensionBumpTravel, (value) => {
          config.suspensionBumpTravel = value;
        }),
        numericControl("Droop travel", "m", 0.08, 0.55, 0.01, () => config.suspensionDroopTravel, (value) => {
          config.suspensionDroopTravel = value;
        }),
        numericControl("Spring rate", "N/m", 20_000, 130_000, 500, () => config.springRate, (value) => {
          config.springRate = value;
        }),
        numericControl("Bump damping", "N*s/m", 500, 18_000, 100, () => config.bumpDamping, (value) => {
          config.bumpDamping = value;
        }),
        numericControl("Rebound damping", "N*s/m", 500, 22_000, 100, () => config.reboundDamping, (value) => {
          config.reboundDamping = value;
        }),
        numericControl("Max susp force", "N", 5_000, 40_000, 500, () => config.maxSuspensionForce, (value) => {
          config.maxSuspensionForce = value;
        }),
        numericControl("Bump stop range", "m", 0.01, 0.22, 0.01, () => config.bumpStopRange, (value) => {
          config.bumpStopRange = value;
        }),
        numericControl("Bump stop rate", "N/m", 20_000, 400_000, 1_000, () => config.bumpStopRate, (value) => {
          config.bumpStopRate = value;
        }),
        numericControl("Bump stop damp", "N*s/m", 0, 35_000, 250, () => config.bumpStopDamping, (value) => {
          config.bumpStopDamping = value;
        }),
        numericControl("Max bump force", "N", 5_000, 80_000, 500, () => config.maxBumpStopForce, (value) => {
          config.maxBumpStopForce = value;
        }),
        numericControl("Front anti-roll", "N/m", 0, 22_000, 250, () => config.frontAntiRoll, (value) => {
          config.frontAntiRoll = value;
        }),
        numericControl("Rear anti-roll", "N/m", 0, 22_000, 250, () => config.rearAntiRoll, (value) => {
          config.rearAntiRoll = value;
        }),
      ],
    },
    {
      title: "Tires / Steering",
      controls: [
        numericControl("Friction", "", 0.45, 1.8, 0.01, () => config.frictionCoefficient, (value) => {
          config.frictionCoefficient = value;
        }),
        numericControl("Handbrake friction", "", 0.2, 1.3, 0.01, () => config.handbrakeFrictionCoefficient, (value) => {
          config.handbrakeFrictionCoefficient = value;
        }),
        numericControl("Load sensitivity", "", 0, 0.45, 0.01, () => config.loadSensitivity, (value) => {
          config.loadSensitivity = value;
        }),
        numericControl("Corner stiffness", "", 0.8, 10, 0.1, () => config.corneringStiffness, (value) => {
          config.corneringStiffness = value;
        }),
        numericControl("HB corner stiff", "", 0.2, 5, 0.1, () => config.handbrakeCorneringStiffness, (value) => {
          config.handbrakeCorneringStiffness = value;
        }),
        numericControl("Long stiffness", "", 1, 16, 0.1, () => config.longitudinalStiffness, (value) => {
          config.longitudinalStiffness = value;
        }),
        numericControl("Long peak slip", "", 0.03, 0.4, 0.01, () => config.longitudinalPeakSlip, (value) => {
          config.longitudinalPeakSlip = value;
        }),
        numericControl("Long slide slip", "", 0.2, 2, 0.01, () => config.longitudinalSlideSlip, (value) => {
          config.longitudinalSlideSlip = value;
        }),
        numericControl("Long slide grip", "", 0.2, 1, 0.01, () => config.longitudinalSlideGrip, (value) => {
          config.longitudinalSlideGrip = value;
        }),
        numericControl("Lat peak angle", "deg", 2, 20, 0.5, degrees(
          () => config.lateralPeakSlipAngle,
          (value) => {
            config.lateralPeakSlipAngle = value;
          },
        ).get, degrees(
          () => config.lateralPeakSlipAngle,
          (value) => {
            config.lateralPeakSlipAngle = value;
          },
        ).set),
        numericControl("Lat slide angle", "deg", 8, 55, 0.5, degrees(
          () => config.lateralSlideSlipAngle,
          (value) => {
            config.lateralSlideSlipAngle = value;
          },
        ).get, degrees(
          () => config.lateralSlideSlipAngle,
          (value) => {
            config.lateralSlideSlipAngle = value;
          },
        ).set),
        numericControl("Lat slide grip", "", 0.2, 1, 0.01, () => config.lateralSlideGrip, (value) => {
          config.lateralSlideGrip = value;
        }),
        numericControl("Force coupling", "", 0.2, 1.2, 0.01, () => config.tireForceCoupling, (value) => {
          config.tireForceCoupling = value;
        }),
        numericControl("Power oversteer", "", 0, 1, 0.01, () => config.rearPowerOversteer, (value) => {
          config.rearPowerOversteer = value;
        }),
        numericControl("Max steer", "deg", 8, 55, 1, degrees(
          () => config.maxSteerAngle,
          (value) => {
            config.maxSteerAngle = value;
          },
        ).get, degrees(
          () => config.maxSteerAngle,
          (value) => {
            config.maxSteerAngle = value;
          },
        ).set),
        numericControl("Ackermann", "", 0, 0.7, 0.01, () => config.ackermann, (value) => {
          config.ackermann = value;
        }),
        numericControl("Camber droop", "deg", -5, 5, 0.1, degrees(
          () => config.camberAtDroop,
          (value) => {
            config.camberAtDroop = value;
          },
        ).get, degrees(
          () => config.camberAtDroop,
          (value) => {
            config.camberAtDroop = value;
          },
        ).set),
        numericControl("Camber rest", "deg", -5, 5, 0.1, degrees(
          () => config.camberAtRest,
          (value) => {
            config.camberAtRest = value;
          },
        ).get, degrees(
          () => config.camberAtRest,
          (value) => {
            config.camberAtRest = value;
          },
        ).set),
        numericControl("Camber bump", "deg", -6, 3, 0.1, degrees(
          () => config.camberAtBump,
          (value) => {
            config.camberAtBump = value;
          },
        ).get, degrees(
          () => config.camberAtBump,
          (value) => {
            config.camberAtBump = value;
          },
        ).set),
        numericControl("Camber thrust", "", 0, 0.4, 0.01, () => config.camberStiffness, (value) => {
          config.camberStiffness = value;
        }),
      ],
    },
  ];
}

function numericControl(
  label: string,
  unit: string,
  min: number,
  max: number,
  step: number,
  get: () => number,
  set: (value: number) => void,
  options: Pick<TuningControl, "afterChange" | "precision"> = {},
): TuningControl {
  return {
    label,
    unit,
    min,
    max,
    step,
    get,
    set,
    precision: options.precision,
    afterChange: options.afterChange,
  };
}

function createTuningControlRow(control: TuningControl) {
  const row = document.createElement("label");
  row.className = "tuning-control";
  row.dataset.tuningControl = control.label;

  const text = document.createElement("span");
  text.className = "tuning-control__label";
  text.textContent = control.label;

  const valueWrap = document.createElement("span");
  valueWrap.className = "tuning-control__value-wrap";

  const range = document.createElement("input");
  range.className = "tuning-control__range";
  range.type = "range";
  range.min = String(control.min);
  range.max = String(control.max);
  range.step = String(control.step);

  const number = document.createElement("input");
  number.className = "tuning-control__number";
  number.type = "number";
  number.min = String(control.min);
  number.max = String(control.max);
  number.step = String(control.step);

  const unit = document.createElement("span");
  unit.className = "tuning-control__unit";
  unit.textContent = control.unit ?? "";

  valueWrap.append(number, unit);
  row.append(text, valueWrap, range);

  const updateValue = (rawValue: string) => {
    const numericValue = Number(rawValue);
    if (!Number.isFinite(numericValue)) {
      return;
    }
    const value = THREE.MathUtils.clamp(numericValue, control.min, control.max);
    control.set(value);
    control.afterChange?.();
    range.value = String(value);
    number.value = formatTuningValue(value, control);
  };
  const updateLiveNumberValue = () => {
    const numericValue = Number(number.value);
    if (
      !Number.isFinite(numericValue) ||
      numericValue < control.min ||
      numericValue > control.max
    ) {
      return;
    }
    control.set(numericValue);
    control.afterChange?.();
    range.value = String(numericValue);
  };

  range.addEventListener("input", () => updateValue(range.value));
  number.addEventListener("input", updateLiveNumberValue);
  number.addEventListener("change", () => updateValue(number.value));
  number.addEventListener("blur", () => {
    number.value = formatTuningValue(control.get(), control);
  });

  return { element: row, range, number };
}

function formatTuningValue(value: number, control: TuningControl) {
  if (control.precision !== undefined) {
    return value.toFixed(control.precision);
  }

  if (control.step >= 1) {
    return String(Math.round(value));
  }

  const decimals = Math.max(0, Math.ceil(Math.abs(Math.log10(control.step))));
  return value.toFixed(decimals);
}

function startDevAutodrive() {
  if (!import.meta.env.DEV) {
    return;
  }

  const params = new URLSearchParams(window.location.search);
  if (!params.has("autodrive")) {
    return;
  }

  const mode = params.get("autodrive");
  if (mode === "left" || mode === "right") {
    startDevSteeringRun(mode);
    return;
  }

  devAutodriveStage = "scheduled";
  window.setTimeout(() => {
    devAutodriveStage = "throttle";
    inputState.throttle = true;
  }, 1000);
  window.setTimeout(() => {
    devAutodriveStage = "steer-left";
    inputState.throttle = true;
    inputState.steerLeft = true;
  }, 3500);
  window.setTimeout(() => {
    devAutodriveStage = "steer-right";
    inputState.steerLeft = false;
    inputState.steerRight = true;
  }, 5200);
  window.setTimeout(() => {
    devAutodriveStage = "handbrake";
    inputState.throttle = false;
    inputState.steerRight = false;
    inputState.handbrake = true;
  }, 6900);
  window.setTimeout(() => {
    devAutodriveStage = "brake";
    inputState.handbrake = false;
    inputState.brake = true;
  }, 8300);
  window.setTimeout(() => {
    devAutodriveStage = "reverse";
    inputState.brake = true;
  }, 10300);
  window.setTimeout(() => {
    devAutodriveStage = "done";
    inputState.brake = false;
    inputState.handbrake = false;
  }, 12200);
}

function startDevSteeringRun(direction: "left" | "right") {
  const steeringKey = direction === "left" ? "steerLeft" : "steerRight";
  devAutodriveStage = `scheduled-${direction}`;
  window.setTimeout(() => {
    devAutodriveStage = "throttle";
    inputState.throttle = true;
  }, 500);
  window.setTimeout(() => {
    devAutodriveStage = `steer-${direction}`;
    inputState.throttle = true;
    inputState[steeringKey] = true;
  }, 1100);
  window.setTimeout(() => {
    devAutodriveStage = "done";
    inputState.throttle = false;
    inputState.steerLeft = false;
    inputState.steerRight = false;
  }, 3800);
}

function createDevStatusElement(root: HTMLDivElement) {
  if (!import.meta.env.DEV) {
    return null;
  }

  const element = document.createElement("pre");
  element.dataset.devStatus = "";
  element.hidden = true;
  root.append(element);
  return element;
}

async function initRapier() {
  const warn = console.warn;
  console.warn = (...args: unknown[]) => {
    const [message] = args;
    if (
      typeof message === "string" &&
      message.includes("deprecated parameters for the initialization function")
    ) {
      return;
    }
    warn(...args);
  };

  try {
    await RAPIER.init();
  } finally {
    console.warn = warn;
  }
}

window.addEventListener("resize", resize);
start().catch((error: unknown) => {
  loading.textContent = "Failed to start prototype. Check the console.";
  console.error(error);
});
