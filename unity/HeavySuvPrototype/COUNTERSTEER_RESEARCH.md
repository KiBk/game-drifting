# Countersteer assist research

## Findings

- CarX Drift Racing Online added a selectable steering assist for steering
  wheels, gamepads, and keyboards. Its official update history also raised the
  steering lock of several drift cars to 56–60 degrees. This supports treating
  larger physical steering lock and input assistance as separate settings.
  Source: https://store.steampowered.com/news/posts/?appids=635260&enddate=1545146434
- SAE paper 2005-01-3472 models countersteering from vehicle body slip angle
  and slip-angle velocity, then evaluates steering-angle-velocity feedback as
  additional compensation. This is a useful lightweight basis for game assist.
  Source: https://saemobilus.sae.org/papers/effect-differential-steering-assist-drift-running-performance-2005-01-3472
- Unity exposes lateral tire slip through `WheelHit.sidewaysSlip`, while
  `WheelCollider.steerAngle` directly sets front-wheel angle. WheelCollider tire
  friction is slip-based and independent of ordinary PhysicMaterial friction,
  so an asphalt surface needs tire-curve handling rather than only a new floor
  PhysicMaterial.
  Sources: https://docs.unity3d.com/ScriptReference/WheelHit-sidewaysSlip.html,
  https://docs.unity3d.com/ScriptReference/WheelCollider-steerAngle.html,
  https://docs.unity3d.com/ScriptReference/WheelCollider.html

CarX's exact assist algorithm is proprietary. The implementation here uses the
published behavior only as a design reference rather than claiming to reproduce
its internal physics.

## Implemented approach

- Physical front-wheel lock increases from 23 to 58 degrees.
- With assist enabled, direct player input retains a 27-degree manual range;
  extra opposite lock is generated only after minimum speed and body-slip
  thresholds are crossed.
- The target countersteer combines body slip angle, slip-angle rate, and yaw-rate
  damping, then moves through a 280-degree-per-second rate limiter to avoid
  instant lock-to-lock snapping.
- The menu exposes `Countersteer ON/OFF`. Turning it off restores direct access
  to the complete 58-degree steering range.
- Existing handling, rollover, RWD oversteer, and coordinate-direction tests
  remain the acceptance boundary.

## Asset choice

The optional `SPORT` body is `hatchback-sports.fbx` from Kenney's Car Kit 3.1,
licensed CC0. It was selected because its proportions are closest to the current
wheelbase. Its bundled wheel meshes are hidden so body selection remains
cosmetic and does not alter WheelCollider physics.

Source: https://kenney.nl/assets/car-kit
