import RAPIER from "@dimforge/rapier3d-compat";
import * as THREE from "three";
import "./style.css";

type InputState = {
  throttle: boolean;
  brake: boolean;
  steerLeft: boolean;
  steerRight: boolean;
};

type VehicleConfig = {
  mass: number;
  engineForce: number;
  reverseForce: number;
  brakeForce: number;
  steerTorque: number;
  lateralGrip: number;
  drag: number;
  maxSteerAngle: number;
};

const FIXED_TIMESTEP = 1 / 60;
const MAX_FRAME_DELTA = 0.1;
const KEY_TAP_HOLD_MS = 90;
const WORLD_SIZE = 480;

const vehicleConfig: VehicleConfig = {
  mass: 1800,
  engineForce: 46000,
  reverseForce: 22000,
  brakeForce: 52000,
  steerTorque: 22000,
  lateralGrip: 0.72,
  drag: 1.2,
  maxSteerAngle: THREE.MathUtils.degToRad(28),
};

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
  private readonly body: RAPIER.RigidBody;
  private readonly wheels: THREE.Group[] = [];
  private readonly wheelOffsets = [
    new THREE.Vector3(-0.92, 0.03, 1.45),
    new THREE.Vector3(0.92, 0.03, 1.45),
    new THREE.Vector3(-0.92, 0.03, -1.32),
    new THREE.Vector3(0.92, 0.03, -1.32),
  ];
  private readonly forward = new THREE.Vector3();
  private readonly side = new THREE.Vector3();
  private readonly worldVelocity = new THREE.Vector3();
  private wheelSpin = 0;
  private latestSlip = 0;

  constructor(
    private readonly world: RAPIER.World,
    private readonly config: VehicleConfig,
  ) {
    this.body = this.createBody();
    this.createVisuals();
    scene.add(this.group);
  }

  updatePhysics(input: InputState, dt: number) {
    const rotation = this.body.rotation();
    const quaternion = new THREE.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
    this.forward.set(0, 0, 1).applyQuaternion(quaternion).normalize();
    this.side.set(1, 0, 0).applyQuaternion(quaternion).normalize();

    const velocity = this.body.linvel();
    this.worldVelocity.set(velocity.x, velocity.y, velocity.z);

    const forwardSpeed = this.worldVelocity.dot(this.forward);
    const lateralSpeed = this.worldVelocity.dot(this.side);
    const steerInput = Number(input.steerRight) - Number(input.steerLeft);
    const isNearlyStopped = Math.abs(forwardSpeed) < 1.2;
    const brakeAsReverse = input.brake && isNearlyStopped;

    if (input.throttle) {
      this.applyForce(this.forward, this.config.engineForce * dt);
    }

    if (brakeAsReverse) {
      this.applyForce(this.forward, -this.config.reverseForce * dt);
    } else if (input.brake) {
      const brakeDirection = forwardSpeed > 0 ? -1 : 1;
      const brakeImpulse = Math.min(
        Math.abs(forwardSpeed) * this.config.mass,
        this.config.brakeForce * dt,
      );
      this.applyForce(this.forward, brakeDirection * brakeImpulse);
    }

    if (steerInput !== 0 && Math.abs(forwardSpeed) > 0.45) {
      const directionFactor = forwardSpeed >= 0 ? 1 : -1;
      const speedFactor = THREE.MathUtils.clamp(Math.abs(forwardSpeed) / 16, 0.25, 1);
      this.body.applyTorqueImpulse(
        {
          x: 0,
          y: steerInput * this.config.steerTorque * speedFactor * directionFactor * dt,
          z: 0,
        },
        true,
      );
    }

    const gripFactor = input.brake ? this.config.lateralGrip * 0.6 : this.config.lateralGrip;
    const lateralImpulse = -lateralSpeed * this.config.mass * gripFactor * dt;
    this.applyForce(this.side, lateralImpulse);

    const dragImpulse = -forwardSpeed * Math.abs(forwardSpeed) * this.config.drag * dt;
    this.applyForce(this.forward, dragImpulse);

    this.latestSlip = THREE.MathUtils.clamp(Math.abs(lateralSpeed) / 12, 0, 1);
    this.animateWheels(input, forwardSpeed, steerInput, dt);
  }

  syncVisuals() {
    const translation = this.body.translation();
    const rotation = this.body.rotation();
    this.group.position.set(translation.x, translation.y, translation.z);
    this.group.quaternion.set(rotation.x, rotation.y, rotation.z, rotation.w);
  }

  getPosition() {
    const position = this.body.translation();
    return new THREE.Vector3(position.x, position.y, position.z);
  }

  getQuaternion() {
    const rotation = this.body.rotation();
    return new THREE.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
  }

  getSpeedKmh() {
    const velocity = this.body.linvel();
    return Math.sqrt(velocity.x ** 2 + velocity.z ** 2) * 3.6;
  }

  getSlipPercent() {
    return Math.round(this.latestSlip * 100);
  }

  private createBody() {
    const bodyDesc = RAPIER.RigidBodyDesc.dynamic()
      .setTranslation(0, 0.85, 0)
      .setLinearDamping(0.15)
      .setAngularDamping(1.8)
      .setAdditionalMass(this.config.mass);
    const body = this.world.createRigidBody(bodyDesc);
    const colliderDesc = RAPIER.ColliderDesc.cuboid(0.95, 0.35, 1.65)
      .setFriction(1.2)
      .setRestitution(0.05);
    this.world.createCollider(colliderDesc, body);
    return body;
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

    this.wheelOffsets.forEach((offset, index) => {
      const wheel = createWheelMesh();
      wheel.position.copy(offset);
      wheel.userData.basePosition = offset.clone();
      wheel.userData.isFront = index < 2;
      this.group.add(wheel);
      this.wheels.push(wheel);
    });

    return chassis;
  }

  private applyForce(direction: THREE.Vector3, impulse: number) {
    this.body.applyImpulse(
      {
        x: direction.x * impulse,
        y: direction.y * impulse,
        z: direction.z * impulse,
      },
      true,
    );
  }

  private animateWheels(input: InputState, forwardSpeed: number, steerInput: number, dt: number) {
    const steerAngle = steerInput * this.config.maxSteerAngle;
    this.wheelSpin += forwardSpeed * dt * 2.4;

    for (const wheel of this.wheels) {
      const basePosition = wheel.userData.basePosition as THREE.Vector3;
      const isFront = Boolean(wheel.userData.isFront);
      wheel.position.copy(basePosition);
      wheel.rotation.order = "YXZ";
      wheel.rotation.y = isFront ? steerAngle : 0;
      wheel.rotation.x = this.wheelSpin;
      wheel.rotation.z = Math.PI / 2;
      wheel.scale.y = input.brake && Math.abs(forwardSpeed) > 2 ? 0.96 : 1;
    }
  }
}

function createWheelMesh() {
  const group = new THREE.Group();
  const tire = new THREE.Mesh(
    new THREE.CylinderGeometry(0.36, 0.36, 0.28, 24),
    new THREE.MeshStandardMaterial({
      color: 0x15191c,
      roughness: 0.88,
      metalness: 0.02,
    }),
  );
  const hub = new THREE.Mesh(
    new THREE.CylinderGeometry(0.18, 0.18, 0.3, 18),
    new THREE.MeshStandardMaterial({
      color: 0xc3c9c9,
      roughness: 0.35,
      metalness: 0.45,
    }),
  );
  tire.castShadow = true;
  tire.receiveShadow = true;
  hub.castShadow = true;
  tire.rotation.z = Math.PI / 2;
  hub.rotation.z = Math.PI / 2;
  group.add(tire, hub);
  return group;
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

function createPhysicsWorld() {
  const world = new RAPIER.World({ x: 0, y: -9.81, z: 0 });
  world.timestep = FIXED_TIMESTEP;
  const groundColliderDesc = RAPIER.ColliderDesc.cuboid(WORLD_SIZE / 2, 0.1, WORLD_SIZE / 2)
    .setTranslation(0, -0.1, 0)
    .setFriction(1.4);
  world.createCollider(groundColliderDesc);
  return world;
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
  const world = createPhysicsWorld();
  const vehicle = new VehicleController(world, vehicleConfig);
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

    if (speedValue) {
      speedValue.textContent = `${Math.round(vehicle.getSpeedKmh())} km/h`;
    }
    if (slipValue) {
      slipValue.textContent = `${vehicle.getSlipPercent()}%`;
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
        speed: Math.round(vehicle.getSpeedKmh()),
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

  devAutodriveStage = "scheduled";
  window.setTimeout(() => {
    devAutodriveStage = "throttle";
    inputState.throttle = true;
  }, 1000);
  window.setTimeout(() => {
    devAutodriveStage = "steer";
    inputState.throttle = true;
    inputState.steerLeft = true;
  }, 3500);
  window.setTimeout(() => {
    devAutodriveStage = "brake";
    inputState.throttle = false;
    inputState.steerLeft = false;
    inputState.brake = true;
  }, 5500);
  window.setTimeout(() => {
    devAutodriveStage = "done";
    inputState.brake = false;
  }, 7000);
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
