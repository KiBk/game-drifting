# Two-Player Convoy Rally

## Stage Result

- Both players share one running stage timer.
- The official team time is recorded when the second car crosses the finish.
- Separation alone never eliminates the team and there is no individual race
  winner.
- Formation quality is a secondary rating based on the proportion of the stage
  spent within approximately 15 metres of track progress.
- Sector pace, overtakes, drift duration, and clean-driving statistics are
  shown per player after the stage, but never replace the shared result.

## Leadership and Catch-Up

- Either player can lead at any time; sectors do not force role swaps.
- In the current prototype, Shift directly requests a smoothly ramped maximum
  1.65x drive-torque boost with no charge meter or gap controls.
- Boost holds the full requested multiplier regardless of wheelspin and does
  not add artificial tire grip.
- Track-progress-based catch-up eligibility remains available as a deferred
  multiplayer rule, but is intentionally disabled for the one-car demo.
- Catch-up assistance is explicit and player-controlled; there is no hidden
  automatic engine-power rubber-banding.

## Multiplayer Contract

Networking and track logic will eventually calculate an authoritative state
for each car containing:

- Whether progress data is valid.
- Whether the car is currently trailing.
- The positive distance deficit measured along the validated checkpoint route.

World-space distance is not used because it produces incorrect results around
hairpins, shortcuts, and overlapping track sections. The future multiplayer
implementation will supply this state after checkpoints exist.

## Deferred Work

- Networking and synchronization.
- Track construction and checkpoint validation.
- Shared timer and finish processing.
- Formation and personal-stat scoring.
- Collision rules, respawning, and off-course recovery.
