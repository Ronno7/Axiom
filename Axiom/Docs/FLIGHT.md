# Forward flight, throttle, and bounded steering

Open `Assets/Scenes/Prototype.unity`, use a 16:9 Game view, and press Play.
Click the Game view to give it keyboard focus. The aircraft travels along world +Z.
W/S or Up/Down steer vertically; A/D or Left/Right steer horizontally.
Releasing the keys stops lateral movement without recentering the aircraft.
Hold Q to lower throttle or E to raise it. Releasing both retains the selected
setting; holding both makes no adjustment. Starting Play Mode resets to cruise.
Hold Space to fire (see `COMBAT.md`). Escape is captured as pause intent but has no effect yet.
Stop and restart Play Mode to restart the short blockout course.

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

Throttle update, 2026-09-28: runtime and test assemblies compile with zero warnings
or errors using the generated .csproj files and installed Unity references.
All 25 movement cases passed outside the editor in a temporary .NET reflection
harness against Unity's math types. The 11 added cases cover throttle endpoints,
acceleration and deceleration at 30/60/144 simulated FPS, gradual acceleration,
overshoot prevention, paused time, and steering independence.
This is not a Unity Test Runner run. The MCP connection was unavailable; current
keyboard bindings, camera framing at different speeds, and Play Mode restart
behavior still need live verification. Earlier editor results below predate throttle.

Acceptance pass after reconnecting:

1. Run `GeometricFlight.EditorTests` in the Unity EditMode Test Runner.
2. Start Prototype at 16:9. Hold Q for at least one second, release, and confirm
   the slower speed persists. Hold E for two seconds and confirm faster travel.
3. Hold Q+E together: the selected throttle should remain unchanged.
4. Steer to every bound at both speeds; check stable camera framing and response.
5. Compare at 30, 60, and 144 FPS where available; stop and restart Play Mode twice
   and confirm the authored spawn, camera position, and cruise speed return.

Verified in Unity 6000.3.24f1 on 2026-09-24: all 14 EditMode tests passed. Live
Play Mode checks used a temporary virtual keyboard to exercise WASD, arrow keys,
neutral and opposing input, both opposite corners, and forward camera tracking.
All five phases passed; the virtual device was removed afterward. The initial
view and upper-right boundary were visually inspected at 16:9. No compilation
or gameplay errors were reported. The scene was returned to Edit Mode.

Run `GeometricFlight.EditorTests` in Unity's EditMode Test Runner. Cases cover
automatic forward travel, diagonal speed, analog magnitude, all four bounds and
two corners, long frames, immediate reversal at an edge, paused time, frame-rate
independence, and bounds away from world origin.

In Play Mode, check both key sets, release and opposing keys, every edge, and
camera framing. The existing course ends around Z=112 (about 19 seconds at the
default speed); forward travel continues beyond it. Level completion,
banking, collisions, combat, and hardware are not implemented by this milestone.

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
