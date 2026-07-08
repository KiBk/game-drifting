# Convoy Rally Unity Prototype

Unity 6 project for the next driving prototype. The committed browser app remains
at the repository root as a reference, while this folder contains the
WheelCollider-based rally-car version intended for WebGL. The Unity project and
namespace retain their original `HeavySuvPrototype` names to avoid unrelated
asset churn while the gameplay-facing prototype moves to the rally format.

## Scope

- Flat plane with grid markings.
- Procedural low, wide rally hatch with four visible wheels.
- 1,550 kg Rigidbody chassis plus four WheelColliders.
- Single-speed electric drive with rear-biased AWD and an RWD development toggle.
- Standard WheelCollider spring/friction behavior with compliant suspension,
  progressive grip loss, and traction-aware torque delivery.
- Arrow-key throttle, brake/reverse, and steering.
- Space handbrake.
- `R`, `N`, `D`, and `A` remain as direction/drive selectors; there are no gears.
- Shift directly activates traction-aware 1.65x boost while held.
- `D` toggles AWD/RWD.
- Manual steering without countersteer assistance.
- Per-wheel ABS reduces service-brake pressure when a tire begins locking;
  the rear handbrake remains unassisted for drift initiation.
- Layered open-source electric-motor, tire-rolling, wheelspin, and locked-tire audio.
- HUD sound-effects volume control and a button to respawn at the assigned start.
- Lower rally-car body and center of mass for sustained-corner rollover resistance.
- Automatic Unity Sessions/Distributed Authority connection with two owner-driven network cars.
- Six-person FIFO spectator queue with follow-camera switching.
- Random synchronized car colors and ghosted car-to-car collisions.
- Chase camera and compact control HUD with selector, ABS, and boost status.
- PlayMode coordinate tests for steering signs, straight driving, reverse,
  settling, controlled oversteer, rollover resistance, and turbo behavior.

See `CONVOY_RALLY.md` for the planned two-player stage format and the future
multiplayer progress contract.

Audio sources and licenses are listed in `Assets/Resources/Audio/CREDITS.md`.
Unity Cloud linking instructions are in `UGS_SETUP.md`.

## Multiplayer

- Packages: Netcode for GameObjects `2.13.0` and Multiplayer Services `2.2.4`.
- Session: `convoy-rally-public-v1`, eight total participants, two drivers.
- Web builds use Unity Distributed Authority over secure WebSockets because WebGL cannot act as a Unity Transport server.
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
