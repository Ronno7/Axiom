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

## Arena flight (grey box)

Open `Assets/Scenes/Arena.unity` and press Play: a round arena 960 m across and 240 m high over a lit grid
floor. Glass towers outlined in light, a light gate in the middle to fly through, and a slow-patrolling
Recognizer-style construct overhead give it landmarks and scale; white light columns ring the edge and a city
skyline stands beyond it. Sixteen turrets and four drones (see `COMBAT.md`). No score, waves or results yet:
the scene tests whether flying and shooting are fun on their own.

**Controls.** The ship always flies forward. **M** switches flight mode in flight:

- **Full rotation** (default): the stick rolls (left/right) and pitches (forward dives, back climbs) the ship
  about its own axes, and nothing levels it. Hold it over for a barrel roll, back for a loop, forward for an
  outside loop; to turn, roll and pull. The camera turns with the ship.
- **Arcade:** stick left/right banks the ship and the bank turns it; centred, the ship levels out. The camera
  follows a quarter of the bank.

In both, the twist is a flat rudder for small aiming corrections (Z and C on the keyboard), and stick forward
dives (`Push To Dive`; the Options invert setting flips it). The throttle sets speed and how hard the ship turns:
quickest on cruise, smallest circle at the brake end. Until the lever has its physical detent, a soft notch counts
the middle 8 % of travel as cruise exactly; the readout at the bottom left says CRUISE there. Near the edge the
ship turns itself back toward the middle; it can't leave the arena or go below 2 m or above the ceiling. Escape or
MENU pauses.

**Energy and stall** are built but off (Gravity 0): with Gravity above 0, climbs cost speed, dives build it (up to
1.4× full speed), and a steep climb stalls the ship below 13 m/s (soft controls, nose falls, buzzer beeps) until
speed is back above 17.5 m/s. Off, speed follows the throttle only.

**Tuning** (`ArenaFlight` on `10_Player`; starting values to tune in Play Mode):

| Setting | Default | Meaning |
| --- | --- | --- |
| Cruise Speed | 30 | m/s on the detent; Throttle Multipliers 0.6–1.5 give 18–45 m/s |
| Roll Rate / Loop Rate / Full Twist Yaw Rate | 220 / 100 / 40 | Full rotation, degrees per second: a barrel roll takes 1.6 s, a loop 3.6 s (radius ≈ 17 m at cruise) |
| Turn Rate / Turn At Throttle Ends | 90 / 0.85, 0.65 | Turn rate on the detent, and relative to it at minimum and maximum throttle (both modes) |
| Max Bank / Bank Response | 65° / 0.15 s | Arcade: how far and how quickly the ship rolls into a turn |
| Pitch Rate / Max Pitch / Pitch Level Time | 75 / 65° / 1.2 s | Arcade: pitch speed, limit, and how quickly a released nose levels |
| Twist Yaw Rate | 27 | Arcade rudder, degrees per second |
| Stick Expo / Twist Expo | 1.7 / 2 | Response curves: finer control near the centre |
| Cruise Snap | 0.08 | The soft cruise notch |
| Gravity / Thrust Response / Drag Response | 0 / 0.7 / 0.3 | Energy (0 = off; try 15) |
| Stall Speed / Stall Recovery / Stall Control / Stall Nose Drop | 13 / 1.35 / 0.2 / 70 | Stall, when energy is on |

`ArenaBounds` (on `20_Arena`) sets the radius, lowest height and ceiling, and draws the edge: posts, a floor and a
top ring, and a translucent wall (`ArenaWall` material) that is faint from afar and lights up with horizontal scan
bands within about 200 m of the ship. The edge turn starts when the ship, heading outward, is within about twice
the room it needs to turn away, and is full strength at that room.

`ChaseCamera` (on the Main Camera) sits 7.5 m behind and 2 m above the ship and springs after it (0.15 s, at most
2 m behind its ideal spot), looking 20 m ahead. In full rotation it turns with the ship, easing after it over
0.06 s; in arcade it doesn't lag in rotation and rolls with a quarter of the bank. The field of view widens from
62° by up to 10° above cruise.

**Components.** `ShipFlight` is what weapons and presentation read from any flying ship (input snapshot, speed,
velocity); `PlayerFlight` (rail) and `ArenaFlight` both derive from it. `ArenaFlightModel` holds the stateless
flight maths (EditMode tests: loop, barrel roll, edge turn, containment, mode switching, energy, stall).
`ArenaSession` handles pause, the M switch, target respawns, the restart after the ship is destroyed and the stall
beep, and draws temporary off-screen markers and the readout.

**Verified** (2026-10-28): EditMode 138/138; Play Mode with the simulated controller (both modes, loops, firing,
autopilot combat) and a clean Console. Flown by the owner on the cockpit: arcade (passed the five-minute test),
then full rotation with lasers and energy (energy then switched off). The light-sheet wall and target brackets
have not been flown yet.
