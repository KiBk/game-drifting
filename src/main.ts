import * as THREE from "three";
import "./style.css";
import {
  FIXED_TIMESTEP,
  WORLD_SIZE,
  JoltDrivingWorld,
  JoltVehiclePhysics,
  createJoltDrivingWorld,
  defaultJoltVehicleConfig,
  type InputState,
  type JoltVehicleConfig,
  type JoltVehicleTelemetry,
} from "./joltVehicle";

type WheelVisual = {
  root: THREE.Group;
  strut: THREE.Mesh;
};

type RearLightVisual = {
  material: THREE.MeshStandardMaterial;
  light: THREE.PointLight;
  activeColor: THREE.Color;
  inactiveColor: THREE.Color;
  activeIntensity: number;
};

type WheelEffectSample = {
  contact: boolean;
  position: THREE.Vector3;
  skid: number;
  lock: number;
  width: number;
};

type TrackSegment = {
  start: THREE.Vector3;
  end: THREE.Vector3;
  width: number;
  intensity: number;
  age: number;
  y: number;
};

type TrackDebugState = {
  segments: number;
  skid: number;
  lock: number;
  peakLock: number;
};

type AudioContextWindow = Window &
  typeof globalThis & {
    webkitAudioContext?: typeof AudioContext;
  };

const MAX_FRAME_DELTA = 0.1;
const KEY_TAP_HOLD_MS = 90;
const MAX_TIRE_TRACK_SEGMENTS = 2600;
const TIRE_TRACK_LIFETIME_SECONDS = 13;
const TIRE_TRACK_FADE_START_SECONDS = 4.5;

let vehicleAudio: VehicleAudio | null = null;

const appElement = document.querySelector<HTMLDivElement>("#app");

if (!appElement) {
  throw new Error("Missing #app root element.");
}

const app = appElement;

const loading = document.createElement("div");
loading.className = "loading";
loading.textContent = "Loading Jolt prototype...";
app.append(loading);

const hud = document.createElement("aside");
hud.className = "hud";
hud.innerHTML = `
  <p class="hud__title">Jolt Drifting Prototype</p>
  <div class="hud__row">
    <span class="hud__label">Speed</span>
    <span class="hud__value" data-speed>0 km/h</span>
  </div>
  <div class="hud__row">
    <span class="hud__label">Gear</span>
    <span class="hud__value" data-gear>N</span>
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
  vehicleAudio?.unlock();
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
  private readonly physics: JoltVehiclePhysics;
  private readonly wheelVisuals: WheelVisual[] = [];
  private readonly brakeLights: RearLightVisual[] = [];
  private readonly reverseLights: RearLightVisual[] = [];
  private inputState: InputState = { ...inputState };
  private telemetry: JoltVehicleTelemetry;

  constructor(
    world: JoltDrivingWorld,
    private readonly config: JoltVehicleConfig,
  ) {
    this.physics = new JoltVehiclePhysics(world, config);
    this.telemetry = this.physics.getTelemetry();
    this.createVisuals();
    scene.add(this.group);
  }

  updateInput(input: InputState) {
    this.inputState = { ...input };
    this.physics.updateInput(input);
  }

  syncVisuals() {
    this.group.position.copy(this.physics.getPosition());
    this.group.quaternion.copy(this.physics.getQuaternion());
    this.syncWheelVisuals();
    this.telemetry = this.physics.getTelemetry();
    this.syncLightVisuals();
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

  getWheelEffectSamples(input: InputState): WheelEffectSample[] {
    this.group.updateWorldMatrix(true, true);
    return this.telemetry.wheels.map((wheel, index) => {
      const position = new THREE.Vector3();
      this.wheelVisuals[index]?.root.getWorldPosition(position);
      position.y = 0.052;

      const roadSpeed = Math.abs(this.telemetry.signedSpeedKmh) / 3.6;
      const surfaceSpeed = Math.abs(wheel.angularVelocity * this.config.wheelRadius);
      const speedMismatch =
        Math.abs(surfaceSpeed - roadSpeed) / Math.max(roadSpeed, surfaceSpeed, 1.4);
      const speedMismatchSlip =
        smoothStep(0.18, 0.72, speedMismatch) *
        smoothStep(1.2, 4.5, Math.max(roadSpeed, surfaceSpeed));
      const lockRatio =
        roadSpeed > 0.2 ? 1 - surfaceSpeed / Math.max(roadSpeed, 0.001) : 0;
      const lockSlip = smoothStep(0.28, 0.76, lockRatio) * smoothStep(1.2, 4.0, roadSpeed);
      const handbrakeLock =
        input.handbrake && !wheel.isFront ? smoothStep(1.2, 4.0, roadSpeed) : 0;
      const spinSlip = smoothStep(0.75, 3.6, Math.abs(wheel.longitudinalSlip));
      const lateralSlip = smoothStep(0.42, 1.25, Math.abs(wheel.lateralSlip));
      const skid = Math.max(spinSlip, lateralSlip, speedMismatchSlip, handbrakeLock * 0.8);
      const lock = Math.max(lockSlip, handbrakeLock);
      return {
        contact: wheel.contact,
        position,
        skid,
        lock,
        width: this.config.wheelWidth * THREE.MathUtils.lerp(0.62, 0.95, Math.max(skid, lock)),
      };
    });
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

    this.createRearLights();

    const strutMaterial = new THREE.MeshStandardMaterial({
      color: 0x20272b,
      roughness: 0.72,
      metalness: 0.12,
    });

    for (const wheel of this.config.wheels) {
      const visual = createWheelMesh(this.config, strutMaterial);
      visual.root.position.copy(wheel.localPosition);
      this.group.add(visual.strut, visual.root);
      this.wheelVisuals.push(visual);
    }
  }

  private syncWheelVisuals() {
    for (const [index, visual] of this.wheelVisuals.entries()) {
      const transform = this.physics.getWheelLocalTransform(index);
      visual.root.position.copy(transform.position);
      visual.root.quaternion.copy(transform.quaternion);
      const hardpoint = this.config.wheels[index]?.localPosition;
      if (hardpoint) {
        orientStrut(visual.strut, hardpoint, transform.position);
      }
    }
  }

  private createRearLights() {
    for (const x of [-0.62, 0.62]) {
      this.brakeLights.push(
        this.createRearLight({
          x,
          y: 0.12,
          z: -1.72,
          width: 0.34,
          height: 0.14,
          activeColor: 0xff1f17,
          inactiveColor: 0x3a0806,
          activeIntensity: 1.9,
        }),
      );
    }

    for (const x of [-0.24, 0.24]) {
      this.reverseLights.push(
        this.createRearLight({
          x,
          y: 0.11,
          z: -1.725,
          width: 0.18,
          height: 0.12,
          activeColor: 0xf6fbff,
          inactiveColor: 0x2f3437,
          activeIntensity: 1.35,
        }),
      );
    }
  }

  private createRearLight({
    x,
    y,
    z,
    width,
    height,
    activeColor,
    inactiveColor,
    activeIntensity,
  }: {
    x: number;
    y: number;
    z: number;
    width: number;
    height: number;
    activeColor: number;
    inactiveColor: number;
    activeIntensity: number;
  }) {
    const active = new THREE.Color(activeColor);
    const inactive = new THREE.Color(inactiveColor);
    const material = new THREE.MeshStandardMaterial({
      color: inactive,
      emissive: active,
      emissiveIntensity: 0.05,
      roughness: 0.28,
      metalness: 0,
    });
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(width, height, 0.045), material);
    mesh.position.set(x, y, z);
    mesh.castShadow = false;
    this.group.add(mesh);

    const light = new THREE.PointLight(active, 0, 5);
    light.position.set(x, y, z - 0.12);
    this.group.add(light);

    return {
      material,
      light,
      activeColor: active,
      inactiveColor: inactive,
      activeIntensity,
    };
  }

  private syncLightVisuals() {
    const brakeActive = this.inputState.brake && this.telemetry.signedSpeedKmh > -2;
    const reverseActive = this.telemetry.gear < 0;
    this.setLightState(this.brakeLights, brakeActive);
    this.setLightState(this.reverseLights, reverseActive);
  }

  private setLightState(lights: RearLightVisual[], active: boolean) {
    for (const light of lights) {
      light.material.color.copy(active ? light.activeColor : light.inactiveColor);
      light.material.emissiveIntensity = active ? light.activeIntensity : 0.05;
      light.light.intensity = active ? light.activeIntensity : 0;
    }
  }
}

class TireTrackSystem {
  private readonly geometry = new THREE.BufferGeometry();
  private readonly material = new THREE.MeshBasicMaterial({
    color: 0xffffff,
    transparent: true,
    opacity: 0.82,
    depthWrite: false,
    side: THREE.DoubleSide,
    polygonOffset: true,
    polygonOffsetFactor: -1,
    polygonOffsetUnits: -1,
  });
  private readonly mesh = new THREE.Mesh(this.geometry, this.material);
  private readonly segments: TrackSegment[] = [];
  private readonly previousPoints: Array<THREE.Vector3 | null> = [];
  private latestSkid = 0;
  private latestLock = 0;
  private peakLock = 0;
  private sequence = 0;

  constructor(targetScene: THREE.Scene) {
    this.mesh.frustumCulled = false;
    this.mesh.renderOrder = 1;
    targetScene.add(this.mesh);
  }

  update(samples: WheelEffectSample[], dt: number) {
    let changed = false;
    this.latestSkid = 0;
    this.latestLock = 0;

    for (let index = this.segments.length - 1; index >= 0; index -= 1) {
      const segment = this.segments[index];
      if (!segment) {
        continue;
      }
      segment.age += dt;
      if (segment.age >= TIRE_TRACK_LIFETIME_SECONDS) {
        this.segments.splice(index, 1);
      }
      changed = true;
    }

    for (const [index, sample] of samples.entries()) {
      this.latestSkid = Math.max(this.latestSkid, sample.skid);
      this.latestLock = Math.max(this.latestLock, sample.lock);
      this.peakLock = Math.max(this.peakLock, sample.lock);
      const markStrength = Math.max(sample.skid, sample.lock);
      if (!sample.contact || markStrength < 0.16) {
        this.previousPoints[index] = null;
        continue;
      }

      const current = sample.position.clone();
      const previous = this.previousPoints[index];
      if (!previous) {
        this.previousPoints[index] = current;
        continue;
      }

      const distance = previous.distanceTo(current);
      if (distance < 0.11) {
        continue;
      }

      if (distance > 2.4) {
        this.previousPoints[index] = current;
        continue;
      }

      this.addSegment(previous, current, sample.width, markStrength);
      this.previousPoints[index] = current;
      changed = true;
    }

    if (changed) {
      this.rebuildGeometry();
    }
  }

  getDebugState(): TrackDebugState {
    return {
      segments: this.segments.length,
      skid: Number(this.latestSkid.toFixed(2)),
      lock: Number(this.latestLock.toFixed(2)),
      peakLock: Number(this.peakLock.toFixed(2)),
    };
  }

  private addSegment(start: THREE.Vector3, end: THREE.Vector3, width: number, intensity: number) {
    this.sequence += 1;
    this.segments.push({
      start: start.clone(),
      end: end.clone(),
      width,
      intensity: THREE.MathUtils.clamp(intensity, 0, 1),
      age: 0,
      y: 0.052 + (this.sequence % 6) * 0.0006,
    });
    if (this.segments.length > MAX_TIRE_TRACK_SEGMENTS) {
      this.segments.splice(0, this.segments.length - MAX_TIRE_TRACK_SEGMENTS);
    }
  }

  private rebuildGeometry() {
    const positions = new Float32Array(this.segments.length * 6 * 3);
    const colors = new Float32Array(this.segments.length * 6 * 3);
    const color = new THREE.Color();
    let positionOffset = 0;
    let colorOffset = 0;

    for (const segment of this.segments) {
      const start = segment.start.clone();
      const end = segment.end.clone();
      start.y = segment.y;
      end.y = segment.y;

      const direction = end.clone().sub(start);
      direction.y = 0;
      if (direction.lengthSq() < 0.0001) {
        continue;
      }
      direction.normalize();
      const perpendicular = new THREE.Vector3(-direction.z, 0, direction.x).multiplyScalar(
        segment.width / 2,
      );
      const a = start.clone().add(perpendicular);
      const b = start.clone().sub(perpendicular);
      const c = end.clone().add(perpendicular);
      const d = end.clone().sub(perpendicular);
      const vertices = [a, b, c, c, b, d];
      const fade = smoothStep(
        TIRE_TRACK_FADE_START_SECONDS,
        TIRE_TRACK_LIFETIME_SECONDS,
        segment.age,
      );
      const visibleIntensity = segment.intensity * (1 - fade);
      color.setScalar(THREE.MathUtils.lerp(0.13, 0.0, visibleIntensity));

      for (const vertex of vertices) {
        positions[positionOffset++] = vertex.x;
        positions[positionOffset++] = vertex.y;
        positions[positionOffset++] = vertex.z;
        colors[colorOffset++] = color.r;
        colors[colorOffset++] = color.g;
        colors[colorOffset++] = color.b;
      }
    }

    this.geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    this.geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
    this.material.vertexColors = true;
    this.geometry.computeBoundingSphere();
  }
}

class VehicleAudio {
  private context: AudioContext | null = null;
  private engineOscillator: OscillatorNode | null = null;
  private engineHarmonic: OscillatorNode | null = null;
  private engineGain: GainNode | null = null;
  private tireGain: GainNode | null = null;
  private tireFilter: BiquadFilterNode | null = null;

  unlock() {
    if (!this.context) {
      this.createGraph();
    }
    void this.context?.resume();
  }

  update(telemetry: JoltVehicleTelemetry, input: InputState) {
    if (!this.context || !this.engineOscillator || !this.engineHarmonic || !this.engineGain || !this.tireGain || !this.tireFilter) {
      return;
    }

    const now = this.context.currentTime;
    const rpmRatio = THREE.MathUtils.clamp(
      (telemetry.engineRpm - defaultJoltVehicleConfig.minRpm) /
        (defaultJoltVehicleConfig.maxRpm - defaultJoltVehicleConfig.minRpm),
      0,
      1,
    );
    const throttle = input.throttle ? 1 : 0;
    const engineFrequency = THREE.MathUtils.lerp(34, 142, rpmRatio);
    const engineVolume = THREE.MathUtils.lerp(0.018, 0.075, rpmRatio) + throttle * 0.042;
    const slip = this.computeSlipLevel(telemetry, input);
    const tireVolume = Math.pow(slip, 1.4) * 0.22;

    this.engineOscillator.frequency.setTargetAtTime(engineFrequency, now, 0.035);
    this.engineHarmonic.frequency.setTargetAtTime(engineFrequency * 1.98, now, 0.035);
    this.engineGain.gain.setTargetAtTime(engineVolume, now, 0.06);
    this.tireGain.gain.setTargetAtTime(tireVolume, now, 0.025);
    this.tireFilter.frequency.setTargetAtTime(THREE.MathUtils.lerp(650, 2600, slip), now, 0.04);
    this.tireFilter.Q.setTargetAtTime(THREE.MathUtils.lerp(5, 12, slip), now, 0.05);
  }

  private createGraph() {
    const AudioContextCtor =
      window.AudioContext || (window as AudioContextWindow).webkitAudioContext;
    if (!AudioContextCtor) {
      return;
    }

    const context = new AudioContextCtor();
    const master = context.createGain();
    const compressor = context.createDynamicsCompressor();
    const engineFilter = context.createBiquadFilter();
    const engineGain = context.createGain();
    const engineOscillator = context.createOscillator();
    const engineHarmonic = context.createOscillator();
    const harmonicGain = context.createGain();
    const noise = context.createBufferSource();
    const tireFilter = context.createBiquadFilter();
    const tireGain = context.createGain();

    master.gain.value = 0.48;
    compressor.threshold.value = -20;
    compressor.knee.value = 20;
    compressor.ratio.value = 8;
    compressor.attack.value = 0.004;
    compressor.release.value = 0.18;

    engineFilter.type = "lowpass";
    engineFilter.frequency.value = 720;
    engineFilter.Q.value = 0.8;
    engineGain.gain.value = 0;
    engineOscillator.type = "sawtooth";
    engineHarmonic.type = "triangle";
    harmonicGain.gain.value = 0.34;

    noise.buffer = createNoiseBuffer(context);
    noise.loop = true;
    tireFilter.type = "bandpass";
    tireFilter.frequency.value = 900;
    tireFilter.Q.value = 7;
    tireGain.gain.value = 0;

    engineOscillator.connect(engineGain);
    engineHarmonic.connect(harmonicGain);
    harmonicGain.connect(engineGain);
    engineGain.connect(engineFilter);
    engineFilter.connect(compressor);
    noise.connect(tireFilter);
    tireFilter.connect(tireGain);
    tireGain.connect(compressor);
    compressor.connect(master);
    master.connect(context.destination);

    engineOscillator.start();
    engineHarmonic.start();
    noise.start();

    this.context = context;
    this.engineOscillator = engineOscillator;
    this.engineHarmonic = engineHarmonic;
    this.engineGain = engineGain;
    this.tireGain = tireGain;
    this.tireFilter = tireFilter;
  }

  private computeSlipLevel(telemetry: JoltVehicleTelemetry, input: InputState) {
    const roadSpeed = Math.abs(telemetry.signedSpeedKmh) / 3.6;
    return telemetry.wheels.reduce((maximum, wheel) => {
      if (!wheel.contact) {
        return maximum;
      }
      const surfaceSpeed = Math.abs(wheel.angularVelocity * defaultJoltVehicleConfig.wheelRadius);
      const speedMismatch =
        Math.abs(surfaceSpeed - roadSpeed) / Math.max(roadSpeed, surfaceSpeed, 1.4);
      const lockRatio =
        roadSpeed > 0.2 ? 1 - surfaceSpeed / Math.max(roadSpeed, 0.001) : 0;
      const spinSlip = smoothStep(0.75, 4.5, Math.abs(wheel.longitudinalSlip));
      const lateralSlip = smoothStep(0.38, 1.25, Math.abs(wheel.lateralSlip));
      const speedSlip =
        smoothStep(0.18, 0.72, speedMismatch) *
        smoothStep(1.2, 4.5, Math.max(roadSpeed, surfaceSpeed));
      const lockSlip =
        smoothStep(0.28, 0.76, lockRatio) * smoothStep(1.2, 4.0, roadSpeed);
      const handbrakeLock =
        input.handbrake && !wheel.isFront ? smoothStep(1.2, 4.0, roadSpeed) : 0;
      return Math.max(maximum, spinSlip, lateralSlip, speedSlip, lockSlip, handbrakeLock);
    }, 0);
  }
}

function createWheelMesh(config: JoltVehicleConfig, strutMaterial: THREE.Material): WheelVisual {
  const root = new THREE.Group();

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
    new THREE.CylinderGeometry(config.wheelRadius, config.wheelRadius, config.wheelWidth, 32),
    tireMaterial,
  );
  const hub = new THREE.Mesh(
    new THREE.CylinderGeometry(
      config.wheelRadius * 0.48,
      config.wheelRadius * 0.48,
      config.wheelWidth + 0.03,
      18,
    ),
    hubMaterial,
  );
  const spokeA = new THREE.Mesh(
    new THREE.BoxGeometry(config.wheelRadius * 1.45, 0.055, config.wheelWidth * 0.5),
    hubMaterial,
  );
  const spokeB = new THREE.Mesh(
    new THREE.BoxGeometry(0.055, config.wheelRadius * 1.45, config.wheelWidth * 0.5),
    hubMaterial,
  );
  const strut = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 1, 10), strutMaterial);
  tire.castShadow = true;
  tire.receiveShadow = true;
  hub.castShadow = true;
  spokeA.castShadow = true;
  spokeB.castShadow = true;
  strut.castShadow = true;
  root.add(tire, hub, spokeA, spokeB);
  return { root, strut };
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
  const behind = new THREE.Vector3(0, 5.2, -10).applyQuaternion(rotation);
  const targetPosition = position.clone().add(behind);
  const cameraLag = 1 - Math.exp(-dt * 5.2);
  camera.position.lerp(targetPosition, cameraLag);

  const lookAt = position.clone().add(new THREE.Vector3(0, 1.05, 0));
  camera.lookAt(lookAt);
}

function bindInput(targetInput: InputState, unlockAudio: () => void) {
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
    unlockAudio();
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
  const world = await createJoltDrivingWorld();
  const vehicle = new VehicleController(world, defaultJoltVehicleConfig);
  const tireTracks = new TireTrackSystem(scene);
  vehicleAudio = new VehicleAudio();
  bindInput(inputState, () => vehicleAudio?.unlock());
  startDevAutodrive();
  loading.classList.add("loading--hidden");

  let accumulator = 0;
  let previousTime = performance.now();

  const frame = (time: number) => {
    const frameDelta = Math.min((time - previousTime) / 1000, MAX_FRAME_DELTA);
    previousTime = time;
    accumulator += frameDelta;

    while (accumulator >= FIXED_TIMESTEP) {
      vehicle.updateInput(inputState);
      world.step(FIXED_TIMESTEP);
      accumulator -= FIXED_TIMESTEP;
    }

    vehicle.syncVisuals();
    const telemetry = vehicle.getTelemetry();
    tireTracks.update(vehicle.getWheelEffectSamples(inputState), frameDelta);
    vehicleAudio?.update(telemetry, inputState);
    updateCamera(vehicle, frameDelta);
    updateHud(telemetry, vehicle.getPosition(), tireTracks.getDebugState());
    renderer.render(scene, camera);
    requestAnimationFrame(frame);
  };

  requestAnimationFrame(frame);
}

function updateHud(
  telemetry: JoltVehicleTelemetry,
  position: THREE.Vector3,
  trackDebug: TrackDebugState,
) {
  if (speedValue) {
    speedValue.textContent = `${Math.round(telemetry.speedKmh)} km/h`;
  }
  if (gearValue) {
    gearValue.textContent = formatGear(telemetry.gear);
  }
  if (rpmValue) {
    rpmValue.textContent = String(Math.round(telemetry.engineRpm));
  }
  if (tachFill) {
    const rpmRatio = THREE.MathUtils.clamp(
      (telemetry.engineRpm - defaultJoltVehicleConfig.minRpm) /
        (defaultJoltVehicleConfig.maxRpm - defaultJoltVehicleConfig.minRpm),
      0,
      1,
    );
    tachFill.style.transform = `scaleX(${rpmRatio.toFixed(3)})`;
  }
  if (slipValue) {
    slipValue.textContent = `${telemetry.slipPercent}%`;
  }
  if (devStatusValue) {
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
      lights: {
        brake: inputState.brake && telemetry.signedSpeedKmh > -2,
        reverse: telemetry.gear < 0,
      },
      driven: telemetry.wheels.map((wheel) => wheel.driven),
      suspension: telemetry.wheels.map((wheel) => Number(wheel.suspensionLength.toFixed(2))),
      contact: telemetry.wheels.map((wheel) => wheel.contact),
      slip: telemetry.wheels.map((wheel) => [
        Number(wheel.longitudinalSlip.toFixed(2)),
        Number(wheel.lateralSlip.toFixed(2)),
      ]),
      tracks: trackDebug,
      stage: devAutodriveStage,
    });
  }
}

function formatGear(gear: number) {
  if (gear < 0) {
    return "R";
  }
  if (gear === 0) {
    return "N";
  }
  return String(gear);
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
  }, 4200);
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

function orientStrut(strut: THREE.Mesh, start: THREE.Vector3, end: THREE.Vector3) {
  const midpoint = start.clone().lerp(end, 0.5);
  const direction = end.clone().sub(start);
  const length = Math.max(direction.length(), 0.08);
  strut.position.copy(midpoint);
  strut.scale.set(1, length, 1);
  strut.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize());
}

function createNoiseBuffer(context: AudioContext) {
  const durationSeconds = 1.5;
  const buffer = context.createBuffer(
    1,
    Math.floor(context.sampleRate * durationSeconds),
    context.sampleRate,
  );
  const data = buffer.getChannelData(0);
  let previous = 0;

  for (let index = 0; index < data.length; index += 1) {
    const white = Math.random() * 2 - 1;
    previous = previous * 0.86 + white * 0.14;
    data[index] = previous;
  }

  return buffer;
}

function smoothStep(edge0: number, edge1: number, value: number) {
  const x = THREE.MathUtils.clamp((value - edge0) / Math.max(edge1 - edge0, 0.001), 0, 1);
  return x * x * (3 - 2 * x);
}

window.addEventListener("resize", resize);
start().catch((error: unknown) => {
  loading.textContent = "Failed to start prototype. Check the console.";
  console.error(error);
});
