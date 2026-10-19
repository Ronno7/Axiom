# Forward flight, throttle, and bounded steering

Open `Assets/Scenes/Prototype.unity`, use a 16:9 Game view, and press Play.
Click the Game view to give it keyboard focus. The aircraft travels along world +Z.
W/S or Up/Down steer vertically; A/D or Left/Right steer horizontally.
Releasing the keys stops lateral movement without recentering the aircraft.
Hold Q to lower throttle or E to raise it. Releasing both retains the selected
setting; holding both makes no adjustment. Starting Play Mode resets to cruise.
Hold Space to fire (see `COMBAT.md`); Escape pauses. The Arduino controller supplies the same
input through `CombinedFlightInput` (keyboard steering keys override the stick while held).

## Tuning

Select `10_PlayerPrototype` and adjust `PlayerFlight`:

| Setting | Default | Meaning |
| --- | --- | --- |
| Forward Speed | 6 | Cruising speed in world units per second along +Z |
| Throttle Multipliers | 0.6, 1.5 | Minimum/maximum relative to cruise (3.6 to 9 units/second) |
| Acceleration | 6 | Speed change in world units/second squared |
| Steering Speed | 5 | Maximum lateral/vertical speed in units per second |
| Horizontal Limits | -6 to 6 | X offsets from the starting position |
| Vertical Limits | -2 to 4 | Y offsets from the starting position |

Bounds constrain the player root, not the mesh surface. Selected gizmos show the
allowed rectangle at the aircraft's current Z. Diagonal input is normalized;
partial analog input retains its magnitude. Bounds are relative to the spawn
position, remain fixed in world X/Y, and work regardless of the player's rotation.
The route is currently straight. Transform movement is intentional: collision
handling and a physics response are a later milestone.

`KeyboardFlightInput` exposes throttle change rate (default 1: one second from
cruise to either limit) and Q/E/Space/Escape binding paths in the Inspector.
Set bindings before entering Play Mode. Throttle uses a signed absolute setpoint:
-1 is minimum, 0 is cruise, +1 is maximum. A future hardware adapter can return
its calibrated setpoint without simulating key presses. Keyboard throttle resets
to cruise when its component is re-enabled. Missing/disabled input returns neutral
steering and a cruise target; speed returns smoothly to cruise.

`GroundShadow` (child of the player) drops the aircraft silhouette onto the floor directly
below the ship, the lowest surface under it, so it slides beneath obstacles rather than onto
them. It shrinks slightly with height. It is the main cue for position and altitude.

The camera retains its authored position relative to the course and follows only
forward travel in `LateUpdate`. Steering therefore moves the aircraft across the
screen. Camera rotation and aircraft rotation are not changed.

## Component responsibilities

- `FlightInputSource`: small abstract MonoBehaviour contract returning a `FlightInput`
  snapshot with steering, throttle, held fire, and a pause press edge.
  It is serializable in Unity's Inspector and is the dependency of `PlayerFlight`.
- `KeyboardFlightInput`: Input System adapter with WASD and arrow-key bindings.
  Owns, enables, disables, and disposes its actions without modifying the template
  input asset. A future Arduino adapter can implement the same contract.
- `FlightMovement`: stateless position, target-speed, and acceleration calculations.
  Acceleration integrates the ramp and any time at the target speed within a frame.
  It has no device, scene,
  frame clock, or camera dependencies and is covered by deterministic tests.
- `PlayerFlight`: reads the assigned input, supplies delta time and configuration,
  and applies the calculated position to its Transform. A disabled or removed
  input source produces neutral steering and cruise throttle; an unassigned source at startup logs
  a setup error and disables flight.
- `ForwardFollowCamera`: follows the assigned target's Z displacement only.

`PlayerFlight` reads input once per Update. Future weapons/UI can observe its
`CurrentInput` and `CurrentSpeed`; do not read the input source a second time in
the same frame, because keyboard reads advance the throttle setpoint.

This uses component composition and a replaceable input strategy. No singleton,
service locator, event bus, or dependency-injection container is needed here.
Runtime scripts share `GeometricFlight.Runtime`; the Editor test assembly is
separate and is excluded from player builds.

## Verification

`GeometricFlight.EditorTests` (EditMode) covers forward travel, diagonal and analog steering, every bound,
long frames, paused time, frame-rate independence and the throttle ramp. Keyboard flight, camera framing
and Play Mode restarts were checked in the editor (2026-09-29).

## Throttle sections (level 1)

Throttle matters because steering speed is fixed: slower flight gives more lateral
room per metre, faster flight beats timed obstacles. Each section is a `ThrottleZone`
(recommended band on the HUD gauge, a banner on entry) with blue floor cues as children:
V-chevrons pointing back mean slow down, forward chevrons mean speed up.

| Section | Z | Rule |
| --- | --- | --- |
| Lattice (slow) | 100–152 | 3 see-through walls 12 apart (solid collision, blue post on each gap edge). At 60–65% cruise a weave has ≥0.3 s slack; 80%+ takes hits |
| Iris (fast) | 152–212 | `ClosingIris` shuts over 5.2 s from the trigger line at 180; ~125%+ cruise gets through |

Par (`LevelFlow`, 35 s) sits between "follow the cues" (~38 s) and "also boost in open
stretches" (~33 s). Gates are sized so the whole flight area passes through them.
