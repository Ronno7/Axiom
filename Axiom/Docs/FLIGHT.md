# Forward flight and bounded steering

Open `Assets/Scenes/Prototype.unity`, use a 16:9 Game view, and press Play.
Click the Game view to give it keyboard focus. The aircraft travels along world +Z.
W/S or Up/Down steer vertically; A/D or Left/Right steer horizontally.
Releasing the keys stops lateral movement without recentering the aircraft.
Stop and restart Play Mode to restart the short blockout course.

## Tuning

Select `10_PlayerPrototype` and adjust `PlayerFlight`:

| Setting | Default | Meaning |
| --- | --- | --- |
| Forward Speed | 6 | World units per second along +Z |
| Steering Speed | 5 | Maximum lateral/vertical speed in units per second |
| Horizontal Limits | -6 to 6 | X offsets from the starting position |
| Vertical Limits | -2 to 4 | Y offsets from the starting position |

Bounds constrain the player root, not the mesh surface. Selected gizmos show the
allowed rectangle at the aircraft's current Z. Diagonal input is normalized;
partial analog input retains its magnitude. Bounds are relative to the spawn
position, remain fixed in world X/Y, and work regardless of the player's rotation.
The route is currently straight. Transform movement is intentional: collision
handling and a physics response are a later milestone.

The camera retains its authored position relative to the course and follows only
forward travel in `LateUpdate`. Steering therefore moves the aircraft across the
screen. Camera rotation and aircraft rotation are not changed.

## Component responsibilities

- `FlightInputSource`: small abstract MonoBehaviour contract returning steering.
  It is serializable in Unity's Inspector and is the dependency of `PlayerFlight`.
- `KeyboardFlightInput`: Input System adapter with WASD and arrow-key bindings.
  Owns, enables, disables, and disposes its action without modifying the template
  input asset. A future Arduino adapter can implement the same contract.
- `FlightMovement`: stateless position calculation. It has no device, scene,
  frame clock, or camera dependencies and is covered by deterministic tests.
- `PlayerFlight`: reads the assigned input, supplies delta time and configuration,
  and applies the calculated position to its Transform. A disabled or removed
  input source produces neutral steering; an unassigned source at startup logs
  a setup error and disables flight.
- `ForwardFollowCamera`: follows the assigned target's Z displacement only.

This uses component composition and a replaceable input strategy. No singleton,
service locator, event bus, or dependency-injection container is needed here.
Runtime scripts share `GeometricFlight.Runtime`; the Editor test assembly is
separate and is excluded from player builds.

## Verification

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
default speed); forward travel continues beyond it. Level completion, throttle,
banking, collisions, combat, and hardware are not implemented by this milestone.
