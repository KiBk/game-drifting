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

const MAX_FRAME_DELTA = 0.1;
const KEY_TAP_HOLD_MS = 90;

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("Missing #app root element.");
}

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
        heading: Number(telemetry.headingDegrees.toFixed(1)),
        steering: Number(telemetry.steeringDegrees.toFixed(1)),
        pitch: Number(telemetry.pitchDegrees.toFixed(1)),
        roll: Number(telemetry.rollDegrees.toFixed(1)),
        suspension: telemetry.wheels.map((wheel) => Number(wheel.suspensionLength.toFixed(2))),
        loads: telemetry.wheels.map((wheel) => Math.round(wheel.normalLoad)),
        stage: devAutodriveStage,
      });
    }

    renderer.render(scene, camera);
    requestAnimationFrame(frame);
  };

  requestAnimationFrame(frame);
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
