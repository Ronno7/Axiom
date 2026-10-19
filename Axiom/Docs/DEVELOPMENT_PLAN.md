# Axiom — Development Plan

Planning baseline: September 28, 2026. Target: Unity 6.3, Universal Render Pipeline.

The game is titled **Axiom**; `GeometricFlight` remains the code namespace.

## Instructions for Codex

1. Read this plan, the repository's `AGENTS.md`, and any existing `Docs/DESIGN_BRIEF.md` and `Docs/CODEX_HANDOFF.md`.
2. Inspect the actual repository and connected Unity editor before making changes. This document describes the delivered starting point, not necessarily the current checkout.
3. Work on the milestone requested by the user. If asked simply to continue, complete the next unfinished milestone and report the result. Do not treat the entire roadmap as authorization to build everything in one session.
4. Preserve existing scenes, assets, GUIDs, package versions, and user edits. Do not regenerate the project over existing work.
5. Keep implementations small and understandable. Explain the result, how to try it, and what was actually tested.
6. Update the progress checklist and session log after a milestone. Distinguish code written from behavior verified in Unity.

The user deliberately reduced the first implementation to a foundation so subsequent work could proceed incrementally.

## 1. Product intent

Build a short, approachable third-person rail shooter controlled by an Arduino-based physical controller. The primary coursework goal is **excellent user interface design**: clear menus, teaching, status, feedback, navigation, and deliberate use of diegetic, non-diegetic, spatial, and meta UI.

Gameplay should make those interfaces useful and enjoyable. The intended scope is approximately one month, with three levels initially lasting about two to three minutes each. Extend levels only if playtesting supports it.

### Confirmed decisions

| Area | Direction |
| --- | --- |
| Engine | Fresh Unity 6.3 project using the Universal 3D / URP template |
| Camera | Third-person behind the aircraft |
| Flight | Continuous forward travel along a designed route; horizontal and vertical steering |
| Player | Simple, triangular geometric aircraft, inspired by early Star Fox |
| Environment | Abstract geometric architecture |
| Enemies | Mathematical and sacred-geometry-inspired shapes |
| Palette | Predominantly black, white, and gray; color concentrated in weapon effects and enemy destruction |
| Feel | Satisfying shattering, bursts, and fizzles when targets are destroyed |
| Difficulty | Approachable completion with optional mastery |
| Throttle | Meaningful adjustments at clearly signaled moments |
| Threats | Enemy projectiles and collisions, with forgiving shields |
| Required menus | Play, functional Options, Help, Quit |
| Input coverage | Physical controller wherever practical, with keyboard support available |
| Length | Three short levels, with a simple geometric boss proposed for the finale |

### Proposed defaults, subject to playtesting

- Hold to fire; unlimited ammunition for the initial version.
- Steering also positions the firing line. One joystick handles both movement and aiming.
- A reticle indicates the shot path; mild aim assistance helps with near misses.
- Banking is visual and does not unexpectedly redirect shots or alter the collision body.
- Brief protection after damage and quick retries.
- Optional performance assessment using accuracy, damage taken, and completion time.
- Throttle initially spans roughly 0.6–1.5 times cruising speed, with smooth acceleration and no stopping or reversing.
- Three shield segments are a starting visual idea, not a fixed balance requirement.

Do not add ammunition inventories, weapon trees, complex flight physics, branching campaigns, or procedural level generation to the baseline scope.

## 2. Delivered foundation and verification boundary

The original foundation ZIP contains:

- `Assets/GeometricFlight/Scenes/Prototype.unity`: static, editable prototype scene.
- Triangular ship, rear camera, monochrome corridor, gates, and three target placeholders.
- `Scripts/Presentation/PrototypeMesh.cs`: editor/play-mode geometry for aircraft, octahedra, and polygon rings.
- `Art/Shaders/FoundationFlat.shader` and five grayscale materials.
- `Editor/FoundationTools.cs`: open-scene and reference-check commands.
- Reserved folders, documentation, and Git setup files.

**Flight, combat, working UI, physics, progression, and Arduino communication were not implemented in that delivery.** Placeholder names do not indicate working systems.

Source syntax and asset-reference checks passed during generation. Unity Editor was unavailable, so import, shader compilation, Game view appearance, and Play Mode still require verification in the real project.

Coordinate convention: **+Z forward, +Y up, +X right**. The player root starts at the origin. The current shader uses fixed facet shading and does not respond to realtime lights or shadows. Generated prototype meshes are temporary and depend on their `PrototypeMesh` component remaining enabled.

## 3. Architecture and working conventions

- Keep gameplay independent of the input device. A small shared input representation should carry steering, throttle, fire, and pause intent. Keyboard and Arduino supply the same values.
- Read the project's installed input setup before choosing or changing input APIs. Preserve the existing template settings wherever possible.
- Keep player movement, weapons, damage, camera, and UI in focused components. Introduce shared abstractions when a real second use appears.
- Keep visual banking and aircraft art beneath the player root.
- Expose useful tuning values in the Inspector. Add configuration assets only where shared values warrant them.
- UI should observe state and events; avoid having HUD code own combat rules.
- Use ordinary editable scene objects and prefabs. Preserve the ability to inspect and adjust the course in Unity.
- Prefer Unity's existing UI facilities; choose the UI approach when implementing the first functional screens and record that choice.
- Keep editor utilities under `Editor`. Avoid automatic import-time changes to project settings.
- Preserve `.meta` files. Keep generated caches and build output out of the source repository unless the user specifically requests otherwise.

## 4. Milestones

### M0 — Verify and establish the baseline

**Work:** Inspect the repository, confirm Unity 6.3 and URP, open `Prototype.unity`, run the foundation reference check, and correct import/rendering issues. Inspect framing at 16:9 and confirm the scene hierarchy is understandable.

**Done when:** Console has no project errors; ship, targets, and corridor render correctly; Play Mode enters and exits cleanly; the scene remains editable. Record the exact Unity version and verification results. Create a baseline commit according to the user's repository workflow.

### M1 — Keyboard flight and rear camera

**Work:** Add bounded horizontal/vertical movement, continuous forward travel, adjustable throttle, smooth acceleration, and a stable following camera. Establish the shared input representation. Proposed keyboard mapping: WASD/arrows to steer, Q/E to lower/raise throttle, Space to fire later, Escape to pause later. Keep these mappings easy to change.

**Done when:** The ship can traverse the blockout without leaving the intended flight area; low/high throttle visibly changes forward speed; steering remains responsive across speeds; camera behavior is stable; restarting Play Mode resets position and speed. Compare behavior at different frame rates available in the editor.

**Scope:** No combat or complete menu system in this milestone.

### M2 — Shooting, aiming, and target feedback

**Work:** Add hold-to-fire, forward projectiles, an aiming reticle, target damage, and one satisfying destruction effect. Start with simple targets; tune shot alignment before adding mild aim assistance. Keep projectile and target lifetime cleanup explicit.

**Done when:** Moving the ship moves the firing line predictably; projectiles line up with the reticle at useful engagement distances; nearby obstacles block shots appropriately; successful hits are clear; targets are destroyed once; projectiles and effects do not accumulate indefinitely. Verify fast projectiles cannot routinely pass through thin targets without hits.

**Scope:** One weapon and one target behavior are sufficient.

### M3 — First complete UI and gameplay loop

**Work:** Build Title → Play → short first course → Results → Retry/Continue, plus Pause and failure/retry. Add shield damage from obstacles and one simple enemy attack. Implement the required Options, Help, and Quit screens. Make the existing first course playable before adding more levels.

**Done when:** A player can start, learn controls, take damage, finish or fail, retry, and return to the menu. Pausing actually suspends gameplay; resuming does not cause a damage or movement jump. Repeated retries do not duplicate actors, listeners, or UI. Quit works in a desktop build; its editor behavior is explained.

**Working options:** Volume, steering sensitivity, invert vertical steering, and screen-shake enable/disable if shake exists. Save settings between launches. Add buzzer and hardware controls in M4. Do not display controls that have no implemented effect.

### M4 — Physical controller and preflight check

**Work:** Assemble and test the Arduino controls, define a small serial protocol, add the Arduino input adapter, and create calibration/connection feedback. Reuse the flight and combat systems through the input abstraction. Add a short preflight interaction that demonstrates steering, throttle, and firing.

**Done when:** Each control works independently and together; sensor readings are calibrated and smoothed; button input is debounced; communication does not block rendering; loss of valid input produces a clear safe response; reconnect and keyboard fallback are understandable. Test unplug/replug, invalid or incomplete packets, missing ultrasonic readings, and a port already in use. Close the connection cleanly on exit and repeated Play Mode runs.

**Controller scope:** HC-SR04 physical throttle, two-axis joystick, firing button, buzzer. The parts are currently unassembled. Do not assume a final pinout, spare pause button, or working serial connection. Confirm the actual board and available joystick switch before wiring. Buzzer warnings must have matching visual cues and a mute option.

**Scheduling:** Perform a small hardware reading/serial feasibility check during week one, even though complete integration is M4. Resolve connection uncertainty before late polish.

### M5 — Expand to three short levels

**Work:** Reuse the verified first-level systems to build two more courses. Introduce a small number of clearly differentiated enemy shapes and behaviors. Add a simple geometric boss using established damage and feedback systems.

| Level | Suggested emphasis |
| --- | --- |
| 1 | Open architecture; teach steering, shooting, and one clear throttle adjustment |
| 2 | Tighter geometric structures; combine dodging and enemy fire |
| 3 | Combine learned skills, then a readable geometric core encounter |

**Done when:** All levels have clear starts, objectives, endings, and retries; completion is achievable for a new player; UI conventions remain consistent. The boss works with variable forward speed and does not depend on unexplained controls. Decide its encounter movement deliberately rather than trapping the player against the end of the course.

### M6 — UI polish, feedback, and user testing

**Work:** Refine visual hierarchy, input prompts, navigation, target readability, hit/destruction feedback, audio, and transitions. Tune throttle decisions and approachable difficulty. Add optional mastery results only after basic completion works well.

**Done when:** A new tester can understand the controls and complete the first course without verbal coaching; selected menu items are unmistakable; Help matches the actual bindings; text is readable at the intended display size; explosions do not obscure imminent threats; options genuinely affect behavior. Test controller navigation and keyboard fallback. Adjust effects for reduced-motion settings.

### M7 — Build and presentation handoff

**Work:** Produce and test the intended desktop build, complete setup/help documentation, and prepare a concise explanation of the UI categories using actual implemented examples. Record remaining issues and the hardware setup required for a demonstration.

**Done when:** The build launches independently of Unity, all three levels and required menus work, settings persist, hardware and fallback controls are tested, Quit exits correctly, and a fresh checkout can be set up using the README.

## 5. UI implementation map

| Category | Planned example | Acceptance question |
| --- | --- | --- |
| Diegetic | Aircraft-mounted shield/status segments | Can the player read actual ship condition from the craft? |
| Non-diegetic | Menus, score, throttle readout, and results | Is information precise, readable, and consistently placed? |
| Spatial | Target brackets and route cues positioned relative to world objects | Is it obvious which object or location the cue refers to? |
| Meta | Restrained screen-edge impact/damage feedback | Does it communicate urgency without hiding the flight path? |

The present white aircraft segments are decorative until bound to shield state. A marker is not automatically diegetic because it occupies 3D space. Explain classification according to the final fiction and implementation.

Use shape, position, animation, and sound alongside any color signals. Preserve the monochrome identity in ordinary UI; concentrate saturated color in combat effects. Keep aiming and nearby hazards visually dominant.

## 6. Four-week pacing guide

This is a flexible scope guide, not a fixed deadline.

| Week | Main outcome |
| --- | --- |
| 1 | M0–M2: verified foundation, flight, shooting; early hardware feasibility check |
| 2 | M3: one complete playable course and functional required screens |
| 3 | M4–M5: controller integration and expansion using existing systems |
| 4 | M6–M7: usability testing, polish, build verification, and presentation |

If time becomes tight, simplify enemy variety, boss complexity, and optional rankings first. Preserve functional menus, clear feedback, reliable controls, and an approachable complete loop.

## 7. Risks to check early

- **Aiming and dodging compete for one stick:** tune target size, projectile alignment, and modest assistance before adding complex enemies.
- **Minimum throttle becomes universally best:** design clearly signaled opportunities to accelerate and evaluate completion-time rewards. Do not simply make slow flight unpleasant.
- **Monochrome objects blend together:** separate silhouettes, brightness, scale, spacing, and motion.
- **Sensor noise or missing echoes move the throttle unexpectedly:** calibrate, smooth, reject invalid data, and define a timeout response.
- **One button limits menu navigation:** provide consistent selectable Back controls and use a joystick switch only if available. Keyboard support remains acceptable.
- **UI grows faster than gameplay:** establish one complete level and shared screens before expanding content.
- **Prototype appearance differs from the concept art:** prioritize an achievable, readable visual direction over reproducing every illustrated detail.

## 8. Progress checklist

- [x] Foundation source and static scene delivered.
- [x] M0 — Import/play/framing checks recorded in `Axiom/Docs/FLIGHT.md`; foundation reference check passed 2026-09-29. Baseline commit: `27890f4`.
- [x] M1 — Flight, camera, throttle, and shared input implemented and verified in the editor (2026-09-29).
- [x] M2 — Shooting, aiming, and target feedback (user-verified 2026-09-29). See `Axiom/Docs/COMBAT.md`.
- [x] M3 — Full loop, required screens, saved options, throttle sections (user playtested). Quit checked in the editor; the desktop-build check moves to M7.
- [ ] M4 — Kit controller (v1) works with calibration and the LCD. Cockpit box (v2): parts in hand except the lights, sketch written, bench test under way; its game features and the preflight check are next.
- [ ] M5 — Three levels and simple finale.
- [ ] M6 — UI polish and user testing.
- [ ] M7 — Tested desktop build and handoff.

## 9. Session log

After each milestone, append: date, milestone, changes, exact verification performed, remaining issues, and the next small task. Record any design changes explicitly so future sessions do not revive rejected ideas.

Initial status: foundation delivered; actual Unity import/play verification pending. Next action is M0 against the user's current repository.

### 2026-09-28 — M1 throttle implementation (editor verification pending)

Inspected the current project and earlier handoff: Unity 6000.3.24f1, URP 17.3.0,
Input System 1.20.0. The earlier flight implementation and its recorded 14 Unity
EditMode tests/five live keyboard phases already exist in baseline commit `27890f4`.
The original foundation-only checklist above was stale.

Added latched Q/E throttle, smooth acceleration, Inspector tuning, and a shared
steering/throttle/fire/pause snapshot. Cruise is 6 units/second, minimum 3.6,
maximum 9; steering speed remains independent of throttle. Fire/pause are input
intent only. Keyboard throttle resets to cruise on enable; Play Mode reloads
reset flight position/speed with the project's current reload settings.

Verification: runtime and EditorTests assemblies compiled using the generated
project files and installed Unity references, with zero warnings/errors. All 25
movement test cases passed through a temporary .NET reflection harness against
the installed Unity math types (14 existing, 11 new), including acceleration
travel at 30/60/144 simulated frames per second. `git diff --check` passed.
These are source/managed checks, not a Unity Test Runner or live Play Mode run.
The editor's MCP connection was unavailable and its log reported connection
failures. No new visual, keyboard, camera, or restart verification is claimed.

Next small task: reconnect Unity MCP, run `GeometricFlight.EditorTests`, verify
Q/E and both-key/release behavior, steering at both speed limits, camera framing,
and two Play Mode restarts. Close M1 after these checks; then begin M2 shooting.
Scenes, asset GUIDs, package versions, and existing build-settings edits were preserved.

### 2026-09-29 — M1 editor verification via Unity MCP

Unity MCP reconnected (the cause was a path-casing mismatch in the Claude Code config).
Verified in the live editor, Unity 6000.3.24f1, `Assets/Scenes/Prototype.unity`:

- `GeometricFlight.EditorTests` in Unity Test Runner (EditMode): 25/25 passed.
- Play Mode, frames stepped manually at 0.02 s: cruise 6 u/s; latched throttle +1 ramps
  smoothly to 9, −1 to 3.6, 0 back to 6 (6 u/s², no overshoot). Forward travel matches speed.
- Camera offset stayed exactly (0, 3, −13) from the ship across all speeds; 16:9 Game view
  framing checked by screenshot at z≈52.
- Three Play Mode entries: each starts at the origin at speed 6 with the latched throttle at 0 and one
  player instance. No console errors.

Not verified: real key presses. Simulated keyboard events did not reach the InputActions
while the editor window was unfocused, so throttle was driven by setting the latched value.
Q/E, both-keys/release, and WASD/arrow steering at min/max speed still need a manual
30-second check in the Game view. The Input System setting touched during the test was restored;
no project files changed.

Manual key check (user, Game view focused): Q/E throttle, both-keys/release latch, and
WASD/arrow steering at min/max speed all behave correctly. `Tools/Geometric Flight/Check
Foundation` reports one camera, URP active, no missing scripts/materials. M0 and M1 closed.

Next small task: begin M2 — hold-to-fire projectiles, reticle, and one target destruction effect.

### 2026-09-29 — M2 shooting, aiming, and target feedback

Added `Scripts/Combat`: `WeaponRules` (cadence, shot velocity, swept hit), `PlayerWeapon`
(8 shots/s hold-to-fire, alternating muzzles, fixed pool of 24), `Projectile` (sphere-cast
per frame), `ShootableTarget` (3 hits, cyan hit flash, break once, `Destroyed` event), and
`ShatterEffect` (orange/magenta tetrahedral shards, 0.9 s, created once then reused).
Shots converge on `AimReticle` (25 units) and inherit ship forward speed; `NearReticle`
(10 units) shows the line. Floor/pillars got box colliders; gates got mesh colliders fed
by `PrototypeMesh`. Three accent materials and `PlayerProjectile.prefab` added. Targets
renamed from `_VisualOnly`; muzzles from `_Reserved`. Unity re-serialized
`Prototype.unity` into its native YAML style; all original object IDs are preserved.

Verification: EditMode 36/36. In Play Mode (stepped frames, `Fire` invoked directly),
Target_01 and Target_02 broke after exactly 3 shots, each with a single `Destroyed` event. The shatter
and reticles were checked in screenshots. Pillar, floor, and gate colliders stop shots, and the pool stays
bounded. Foundation check passes; no console errors. Not verified: Space key in the
Game view, and the hit-flash visual (logic only).

Design decisions: no aim assist yet (alignment first, per plan); any collider blocks
shots; target colliders (r=1.4) are slightly generous. Reticle readability against
white targets should be revisited in M6.

Next small task: user Space check, then M3 (first full loop and required UI).

### 2026-09-29 — M3 pass 1: gameplay loop

Shield + hull collisions, diegetic shield segments, enemy turret shots, two barriers, and
`LevelFlow` (pause, complete, fail, retry, stats). UI decision: UI Toolkit for menus/HUD,
uGUI world space for later spatial markers; separate menu and level scenes.
Editor-verified; awaiting the user's playtest. Next: pass 2 screens (Title, Options, Help,
HUD, Pause, Results, Quit) and the MainMenu scene.

### 2026-09-29 — M3 pass 2: screens

MainMenu scene (Title, Options, Help, Quit) and level UI (HUD, pause with Options/Help,
results with Retry/Next/Menu) in UI Toolkit; saved settings for sensitivity and invert.
See `UI.md`. Editor-verified via UI navigation events; awaiting the user's keyboard/mouse playtest.
Open design issue: throttle is rarely needed. Next: throttle-driven course sections.

### 2026-09-29 — M3 UI feedback and throttle design

Menu: procedural geometric constructs at several depths, fog, slanted buttons; "restart
level" wording; results no longer steer the ship. Throttle: lattice (slow) and closing iris
(fast) sections with floor cues, HUD band/hints, and a 33 s par. See `FLIGHT.md`, `UI.md`.
Editor-verified with an autopilot; awaiting the user's playtest.
Refinement after playtest: calmer menu composition; see-through lattice with gap posts, 12-unit
spacing and 60–65% band; V-chevron slow cue and zone banner; gates enlarged (they could clip
the flight area); targets moved later (40/62/80); par 35 s.

### 2026-09-29 — M4 pass 1: controller software (hardware not yet assembled)

Uno sketch, serial protocol v1 with checksums, background-thread link with port scanning,
reconnect and a simulated controller, sonar filtering (median, smoothing, hold, then ease to cruise),
keyboard fallback, controller-loss pause, HUD status and warnings, buzzer cues and mute. API
compatibility is now .NET Framework (required for System.IO.Ports). Verified with tests and the
simulator in Play Mode; the sketch has not been compiled (no Arduino toolchain on this PC) and
the hardware is untested. Next: calibration screen and preflight.
Calibration screen added (Options → Calibrate controller): stick centre, throttle min/max, live
check with invert switches; tested with the simulator. User wired the hardware; controller reads
correctly on COM3 after fixing a shorted button. Next: preflight check, then enclosure build.

### 2026-09-29 — M4: cockpit LCD

16×2 I²C LCD as a cockpit display: library-free driver in the sketch (auto address, diffed redraw,
8 glyphs, standby self-test), `LT` rows in the protocol, game-drawn screens for menus, calibration,
flight, zones, pause and results (`CockpitDisplay`). EditMode 90/90; screens checked with the simulator
in Play Mode; sketch compiles (arduino-cli). Not yet run on the real LCD. Next: hardware check, preflight.
Added runway lights (74HC595, 5 LEDs by the throttle slot pointing the lever at a zone's band),
RGB underglow, a speed-following fan (L293D, Options toggle, firmware stops it after 1 s of silence)
and parking-sensor proximity beeps with a pulsing HUD tag. Rejected: "ghost hand" best-run LED.
EditMode 102/102; LF packets and beeps checked with the simulator. Hardware not built yet.
Then: underglow removed; a second chained 74HC595 adds a front status strip (3 blue shield, 2 red
caution, 5 green targets) and the light of a 30 mm LED arcade fire button (Adafruit 3491), which
replaces the tactile trigger; the case drawings move the joystick tower back 10 mm to clear it.
Unity MCP was offline: both assemblies build with dotnet and 22/23 tests pass in a .NET harness
(the other needs PlayerPrefs); run EditMode in Unity next.

### 2026-09-30 — Reverted to the LCD baseline

While v2 parts ship, the game and sketch are back to the kit hardware plus the LCD: runway lights,
underglow/status strip and fan (and its Options toggle) were removed from Unity, the sketch, wiring and
README (they return with the cockpit build). Proximity beeps stay: they only use the kit buzzer.
Sketch compiles. PLAY is now a step larger than the other menu buttons (`menu-button--primary`).
Menu: the trefoil knot now also turns its symmetry axis about the vertical (`knotTurn`, 7°/s).

### 2026-09-30 — Controller v2 design (cockpit box), decided, not yet built

One box, compact preset 420 × 245 mm (roomy 500 × 280 kept as an alternative), top sloped 8°
(60 → 94 mm), 65 mm palm rest; laser-cut 3 mm black acrylic top + 6 mm Baltic birch body. 3D model:
`Arduino/cockpit/make_cockpit.py` (Blender). Left: HC-SR04 throttle (100 mm travel, push = faster, cruise detent) with
8 NeoPixels beside it. Right: JH-D400X-R4 analog stick, FIRE on its top button. Centre: LCD, shield ×3
and LINK/SLOW/FAST/PROX/EMP warning lights (16 NeoPixels on one pin), a 40 mm red mushroom EMP button
(in flight: once per round, destroys all enemies near the ship; EMP light = still available; in menus:
confirm/launch — gameplay not implemented yet),
one MENU button (pause in flight, back in menus; a second button was dropped for lack of a real job), flip-cover MASTER ARM (arm to launch, SAFE pauses). DRV2605L engine rumble under the
throttle hand. Uno on a DFRobot Gravity IO shield (GND/5V/signal per pin, no power bus). Dropped: fan, underglow, target tally, button lights,
UltraStik (cost). Next: buy and measure parts, cardboard mock-up from a 1:1 template, then cut files.
Shopping list, wiring (`Arduino/cockpit/wiring-cockpit.svg`) and a part-by-part check against the
manufacturers' docs are in `Arduino/cockpit/README.md`. The check moved the buzzer onto the kit's S8050
(≈30 mA is over the 20 mA pin limit) and set 28 mm holes for the Happ-style buttons.

### 2026-10-07 — Controller v2 simplified (16 solder joints)

The 16 separate NeoPixel boards (94 joints) became one pre-wired string of 10 mm WS2812B modules: same
holes, same pin (D6), 4 joints at its start. The kit buzzer + S8050 became a Gravity buzzer (DFR0032) on
D8, and the DRV2605L + disc a Gravity vibration module (DFR0440) on D5, glued behind the throttle's
reflector plate; the HC-SR04 uses single jumpers. EMP is now a 60 mm red dome (uxcell, 24 mm hole). The
joystick has 5 bare wires (power pair, X, Y, button), spliced to A0/A1/D2. Review fixes in the model: 6 mm
throttle stem (the 9.6 mm one didn't fit the 8 mm slot) and the Uno turned sideways against the back wall
so its plugs clear the MENU microswitch. Parts in hand and to buy: `Arduino/cockpit/README.md`.

### 2026-10-18 — Cockpit parts in hand (lights shipping), v2 sketch, docs consolidated

The JH-D400X-R4's 5 leads are the **twist** pot (red/white/black) and the top button (2 yellow); X and Y are
bare-lug pots. All three are 10 kΩ with a full sweep (metered). Each gets its own row: X → A0, Y → A1, twist → A2
(game use undecided: barrel roll or visual banking); button → D2 G/S. The EMP dome's lamp has its own resistor, so
D12 drives it (lit = EMP available). MASTER ARM terminals as DaierTek names them (+1, +2, −). Build: 24 joints.
v2 sketch `Arduino/AxiomCockpit` (lights not yet): `AX` gains twist/EMP/ARM after the v1 fields (MENU in the pause
slot), `RM` rumble with a 1.5 s keep-alive, `LA` dome lamp, and a standby self-test of every input. Unity parses
6- or 9-field packets and can format `RM`/`LA`; no gameplay uses them yet. Sketch compiles; EditMode 98/98.
Bench test on the shield (user): MENU and EMP + lamp harnesses soldered; FIRE, EMP, MENU, clicks, rumble and the
lamp work in standby. Still to bench: MASTER ARM, joystick, sonar. Docs consolidated from all sessions: what each cockpit control and light does (decided
2026-09-30) and the rejected ideas now live in `Arduino/cockpit/README.md`; handoff, brief and FLIGHT are current.
