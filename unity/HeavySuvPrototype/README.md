# Heavy SUV Unity Prototype

Unity 6 project for the next driving prototype. The committed browser app remains
at the repository root as a reference, while this folder contains the
WheelCollider-based version intended for WebGL.

## Scope

- Flat plane with grid markings.
- Simple SUV body with four visible wheels.
- Rigidbody chassis plus four WheelColliders.
- Arrow-key throttle, brake/reverse, and steering.
- Space handbrake.
- `D` toggles AWD/RWD.
- Chase camera and telemetry HUD.
- PlayMode coordinate tests for steering signs, straight driving, reverse,
  settling, and AWD/RWD slip behavior.

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
