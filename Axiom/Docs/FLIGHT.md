# Flight

The ship flies the same way on the rail and in the arena of the [opening](OPENING.md). A rechargeable boost on the
throttle's final travel is in test (below). A broader keyboard/mouse control scheme is still to come; Q/E support
doesn't complete it.

## Controls

The ship always flies forward. W/S or Up/Down and A/D or Left/Right work the stick's axes. Hold Q to lower throttle
or E to raise it. Releasing both retains the selected setting; holding both makes no adjustment. Starting Play Mode
resets to cruise. Hold Space to fire (see `COMBAT.md`); Escape pauses. The Arduino controller supplies the same
input through `CombinedFlightInput` (keyboard steering keys override the stick while held).

For a cockpit without its throttle lever, enable **Options > Keyboard throttle with controller (Q / E)**.
Q/E then set and hold throttle while the cockpit retains steering, twist and buttons. Zero remains cruise;
releasing the keys does not hand throttle back to the sensor. The setting is saved. Turn it off when using the
physical throttle. Hardware mode retains its existing sensor-loss hold/ease to cruise; it never silently switches
to a stale keyboard setpoint.

`KeyboardFlightInput` exposes throttle change rate (default 1: one second from
cruise to either limit) and Q/E/Space/Escape binding paths in the Inspector.
Set bindings before entering Play Mode. Throttle uses a signed absolute setpoint:
-1 is minimum, 0 is cruise, +1 is maximum. The controller path supplies
its calibrated setpoint without simulating key presses. Keyboard throttle resets
to cruise when its component is re-enabled. Missing/disabled input returns neutral
steering and a cruise target; speed returns smoothly to cruise.

`GroundShadow` (child of the player) drops the aircraft silhouette onto the floor directly
below the ship, the lowest surface under it, so it slides beneath obstacles rather than onto
them. It shrinks slightly with height. Its **Light Direction** defaults to straight down; a slanted direction casts
it ahead of the ship, which a chase camera can still see at altitude. The opening's passage doesn't use the shadow.

## Handling

The twist is a flat rudder for small aiming corrections (Z and C on the keyboard), and stick forward dives
(`Push To Dive`; the Options invert setting flips it). The throttle sets speed and how hard the ship turns: quickest
on cruise, smallest circle at the brake end. Until the lever has its physical detent, a soft notch counts the middle
8 % of travel as cruise exactly; the readout says CRUISE there. Escape or MENU pauses.

- **Roll & Pitch** (the opening's mode): the stick rolls (left/right) and pitches (forward dives, back climbs) the ship
  about its own axes, and nothing levels it. Hold it over for a barrel roll, back for a loop, forward for an
  outside loop; to turn, roll and pull. The camera turns with the ship.
- **Bank & Turn** (`ArenaFlight` → Full Rotation off): stick left/right banks the ship and the bank turns it; centred,
  the ship levels out. The camera follows a quarter of the bank.

**The rail** is nose-led: the nose sets travel, within 45° of straight ahead, so the ship can't turn back along the
rail, and steering hard sideways slows progress. The same ship carries straight into the arena with its inputs and
weapons intact. The passage walls are lethal, and a seal closes behind the ship at release. Tuning and curved rails
are still to come.

**The arena** is round, 1,440 m across and 420 m high. Near the edge the ship turns itself back toward the middle; it
can't leave the arena or go below 2 m or above the ceiling.

**Throttle sections.** Slower flight gives more room per metre to steer, and faster flight beats timed obstacles.
Each section is a `ThrottleZone`: the recommended band and a hint on the HUD gauge, a buzzer tone on entry, and blue
floor cues as children. V-chevrons pointing back mean slow down, forward chevrons mean speed up.

**Boost.** The throttle's final eighth is a boost gate; normal full throttle (45 m/s) is reached where the gate begins.
Pushing into the gate with a full charge fires a burst: up to 1.5 s at 70 m/s. The charge is spent when the burst
starts. Pulling back below 0.65 ends it early, and speed then eases back at normal acceleration. The charge refills
over 6 s, starting 1 s after the burst. To fire again the lever must come back below 0.65 first. That also applies
after a pause and at the entry's hand-over, so a lever left forward never fires on its own. On the keyboard, holding
E to the end of travel enters the gate. `ThrottleBoost` holds the rules; the flight state carries the boost, so
proximity prediction includes it.

Hold **Left Shift** or the cockpit's **LOCK button (D11)** to paint missile locks and release to fire the volley.
Space/FIRE fires the gun when LOCK is released. See `COMBAT.md` for acquisition and the missile bank.

**Energy and stall** are built but off (Gravity 0): with Gravity above 0, climbs cost speed, dives build it (up to
1.4× full speed), and a steep climb stalls the ship below 13 m/s (soft controls, nose falls) until speed is back above
17.5 m/s. Off, speed follows the throttle only.

## Tuning

`ArenaFlight` on `10_StudyPlayer`; starting values to tune in Play Mode:

| Setting | Default | Meaning |
| --- | --- | --- |
| Cruise Speed | 30 | m/s on the detent; Throttle Multipliers 0.6–1.5 give 18–45 m/s |
| Roll Rate / Loop Rate / Full Twist Yaw Rate | 220 / 100 / 40 | Roll & Pitch, degrees per second: a barrel roll takes 1.6 s, a loop 3.6 s (radius ≈ 17 m at cruise) |
| Turn Rate / Turn At Throttle Ends | 90 / 0.85, 0.65 | Turn rate on the detent, and relative to it at minimum and maximum throttle (both modes) |
| Max Bank / Bank Response | 65° / 0.15 s | Bank & Turn: how far and how quickly the ship rolls into a turn |
| Pitch Rate / Max Pitch / Pitch Level Time | 75 / 65° / 1.2 s | Bank & Turn: pitch speed, limit, and how quickly a released nose levels |
| Twist Yaw Rate | 27 | Bank & Turn rudder, degrees per second |
| Boost | on | Gate 0.75, release 0.65, 70 m/s at 60 m/s², up to 1.5 s, refill 6 s after 1 s |
| Stick Expo / Twist Expo | 1.7 / 2 | Response curves: finer control near the centre |
| Cruise Snap | 0.08 | The soft cruise notch |
| Gravity / Thrust Response / Drag Response | 0 / 0.7 / 0.3 | Energy (0 = off; try 15) |
| Stall Speed / Stall Recovery / Stall Control / Stall Nose Drop | 13 / 1.35 / 0.2 / 70 | Stall, when energy is on |

`ArenaBounds` (on `21_StudyArena`) sets the radius, lowest height and ceiling, and draws the edge in posts and rings
of light. The edge turn starts when the ship, heading outward, is within about twice the room it needs to turn away,
and is full strength at that room.

`ChaseCamera` (on the Main Camera) sits 7.5 m behind and 2 m above the ship and springs after it (0.15 s, at most
2 m behind its ideal spot), looking 20 m ahead. In Roll & Pitch it turns with the ship, easing after it over
0.06 s; in Bank & Turn it doesn't lag in rotation and rolls with a quarter of the bank. The field of view widens from
62° by up to 10° above cruise.

## Component responsibilities

- `FlightInputSource`: small abstract MonoBehaviour contract returning a `FlightInput`
  snapshot with steering, throttle, held fire, and a pause press edge.
  It is serializable in Unity's Inspector and is the dependency of `ArenaFlight`.
- `KeyboardFlightInput`: Input System adapter with WASD and arrow-key bindings.
  Owns, enables, disables, and disposes its actions without modifying the template
  input asset. The Arduino input path uses the same contract through `CombinedFlightInput`.
- `ShipFlight`: what weapons and presentation read from the flying ship (input snapshot, speed, velocity).
  `ArenaFlight` derives from it, and steps the flight models.
- `ArenaFlightModel`: the stateless flight maths (EditMode tests: loop, barrel roll, edge turn, containment, mode
  switching, energy, stall). `RailFlightModel` adds the rail's heading limit to it, and flies the frame that crosses
  the rail's end: the rail's law up to the plane and the arena's for the rest, with nothing reset or blended.
- `RailRings`: scores the rail's rings from the path the flight reports each frame. `RingScore` holds the once-only
  rule and the checkpoint save.
- `FlightMovement`: stateless target-speed and acceleration calculations. Acceleration integrates the ramp and any
  time at the target speed within a frame.

The flight reads input once per Update. Weapons and UI observe its `CurrentInput` and `CurrentSpeed`; do not read the
input source a second time in the same frame, because keyboard reads advance the throttle setpoint.

This uses component composition and a replaceable input strategy. No service locator, event bus, or
dependency-injection container is needed here. Runtime scripts share `GeometricFlight.Runtime`; the test assemblies
are separate and are excluded from player builds.

`GeometricFlight.EditorTests` (EditMode) covers the movement rules, including the throttle range, the ramp, long
frames and paused time.
