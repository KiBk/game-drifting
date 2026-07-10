# Convoy Rally Unity Prototype

Unity 6 project for the next driving prototype. The committed browser app remains
at the repository root as a reference, while this folder contains the
WheelCollider-based rally-car version intended for WebGL. The Unity project and
namespace retain their original `HeavySuvPrototype` names to avoid unrelated
asset churn while the gameplay-facing prototype moves to the rally format.

## Scope

- Flat plane with grid markings.
- Universal Render Pipeline 17.5 with WebGL-compatible quality assets.
- Procedural low, wide rally hatch with four visible wheels.
- 1,550 kg Rigidbody chassis plus four WheelColliders.
- Single-speed electric drive with rear-biased AWD and an RWD development toggle.
- Torque-biasing limited-slip differentials between axles and across both the
  front and rear axles, with bounded transfer to retain predictable handling.
- Standard WheelCollider spring/friction behavior with compliant suspension,
  progressive grip loss, and traction-aware torque delivery.
- Arrow-key throttle, brake/reverse, and steering.
- Landscape phone controls with left-side steering and right-side gas, brake,
  and boost; touch boost also applies gas without changing keyboard Shift behavior.
- Space handbrake.
- `R`, `N`, `D`, and `A` remain as direction/drive selectors; there are no gears.
- Shift directly activates a fixed full-power 1.65x boost while held.
- `D` toggles AWD/RWD.
- Manual steering without countersteer assistance.
- Per-wheel ABS reduces service-brake pressure when a tire begins locking;
  the rear handbrake remains unassisted for drift initiation.
- Layered open-source electric-motor, tire-rolling, wheelspin, and locked-tire audio.
- HUD sound-effects volume control, manual respawn, and automatic respawn after falling from the platform.
- Lower rally-car body and center of mass for sustained-corner rollover resistance.
- Fresh Unity Relay rooms with shareable invite links and up to eight owner-driven network cars.
- Random active-car preview camera while a browser finishes joining the realtime network.
- A 50 Hz low-latency vehicle profile with unreliable compressed transform deltas and modern interpolation.
- Multiplayer RTT, network tick rate, and browser frame-rate telemetry in the connection HUD.
- Random synchronized car colors and ghosted car-to-car collisions.
- Chase camera and compact control HUD with selector, ABS, and boost status.
- PlayMode coordinate tests for steering signs, straight driving, reverse,
  settling, controlled oversteer, rollover resistance, and turbo behavior.

See `CONVOY_RALLY.md` for the planned two-player stage format and the future
multiplayer progress contract.

Audio sources and licenses are listed in `Assets/Resources/Audio/CREDITS.md`.
Unity Cloud linking instructions are in `UGS_SETUP.md`.

## Multiplayer

- Packages: Universal Render Pipeline `17.5.0`, Netcode for GameObjects `2.13.0`,
  and Multiplayer Services `2.2.4`.
- Opening the game without an invite creates a fresh Relay allocation with eight participant slots.
- The host copies the displayed `?join=<relay-code>` link for other drivers.
- Opening an invite link joins that room; expired Relay join codes stop with an option to create a fresh room instead of retrying forever.
- Relay allocations currently target `europe-north1`.
- Spawning waits for both Relay setup and Netcode's local-client-ready callback.
- Netcode's protocol version rejects incompatible cached builds.
- When the host leaves, its Relay code expires and invited players can create a fresh room.
- Web builds use Relay client-host networking over secure WebSockets.
- Phone WebGL builds block portrait driving and expose separate toolbar toggles
  for touch driving controls, the `CAR` panel, and the host invite link.
- Touch-capable desktop browsers start with touch driving controls hidden.
- Without a linked Unity Cloud project the build runs a local one-player fallback.

## Batch Commands

Unity executable used during setup:

```sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity
```

Regenerate the prototype scene:

```sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/kibk/game-drifting/unity/HeavySuvPrototype \
  -executeMethod HeavySuvPrototype.Editor.PrototypeSceneBuilder.BuildPrototypeScene \
  -logFile /Users/kibk/game-drifting/unity-scene-build.log
```

Run PlayMode physics tests. Do not pass `-quit`; Unity's test runner exits on
completion.

```sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/kibk/game-drifting/unity/HeavySuvPrototype \
  -runTests -testPlatform PlayMode \
  -testResults /Users/kibk/game-drifting/unity-playmode-results.xml \
  -logFile /Users/kibk/game-drifting/unity-playmode.log
```

Build WebGL:

```sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/kibk/game-drifting/unity/HeavySuvPrototype \
  -executeMethod HeavySuvPrototype.Editor.PrototypeWebGlBuilder.BuildWebGl \
  -logFile /Users/kibk/game-drifting/unity-webgl-build.log
```

Serve the built WebGL player locally:

```sh
python3 /Users/kibk/game-drifting/scripts/serve_unity_webgl.py
```

Generated Unity folders such as `Library/`, `Logs/`, `UserSettings/`, and
`Builds/` are intentionally ignored.
