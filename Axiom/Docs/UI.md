# UI

The current HUD work happens in the [shared entry study](SHARED_ENTRY_STUDY.md); earlier experiments are
summarized in [studies](STUDIES.md).

## Entry and flight HUD

During the shared entry, `EntryTerminal` (top centre) prints real state and the geometry being built, and
`EntryCodeTerminals` streams pseudo-code windows at the screen edges (Share Tech Mono, OFL, in
`Assets/UI/Fonts`). Both wait until the menu has flown past. Over the last beats before control the HUD
draws itself in turn (`LevelUi.SetEntryAssembly`): the time and targets, the throttle gauge, the missile/EMP
panel, then the input tag. Each readout's thin cyan rule traces out, then its real text types in
(`InstrumentBuild`); the missile panel's edge draws up, its sockets appear one by one before their contents
show, and it stays silent until control. Nothing counts up, and everything is readable at hand-over.
On the tutorial rail a RINGS readout sits under the targets (`LevelUi.SetRingsReadout`, set only by
`TutorialCourse`); other courses never show it. The original HUD, corner brackets and
menu footer stay through flight, and both UI documents share the CRT composition.

The throttle gauge is the lever's travel in eight segments, like the cockpit's eight lights. The cruise
notch is at the centre and the bright marker shows the lever. The quieter fill shows actual speed on the
same scale (`ThrottleGauge`), and the readout gives it in m/s with LOW, CRUISE, FULL or BOOST. With boost
on, the final segment is marked BOOST and a thin reserve bar below the track reads ACTIVE, CHARGING,
PULL BACK or READY. The cockpit LCD shows the same scale and speed in m/s. `LevelUi` initializes after
gameplay state, so fresh loads and retries can read the shield safely.

All current art and UI use the shared [art palette](ART_PALETTE.md), with generated C#/USS tokens,
shader defaults and an explicit Editor audit. Existing menu/HUD layouts are preserved.

For a visual tour, see the [UI showcase](https://ronno7.github.io/Axiom/) (source in `docs/`).
Images can be enlarged; Standard / Arcade comparisons and layer views are interactive. See
[`docs/README.md`](../../docs/README.md) for editing and capture guidance.

**Approach:** UI Toolkit (UXML + USS) for menus, the HUD, missile brackets and guidance. The gun's aim
brackets are world-space lines (`AimAssistMarker`). Original menu/level scenes each have one `UIDocument`;
SharedEntryStudy keeps separate menu and level documents under one look/CRT component. They use
`Assets/UI/AxiomPanelSettings` (1920×1080 reference, match 0.5) and an `EventSystem` with
`InputSystemUIInputModule`, so menus work with mouse and keyboard (arrows, Enter, Esc). The cockpit drives
them too (`CockpitMenuNavigation`, created at start-up): the stick moves focus like the arrow keys (one step
per push, repeating while held) and FIRE presses the focused button. It acts only while a visible menu control
has focus, so flight is unaffected, and a FIRE held into a menu must be released first.

| File (`Assets/UI`) | Contents |
| --- | --- |
| `Axiom.uss` | The whole theme. Focused buttons invert (white fill, black text) and shift right |
| `AxiomTheme.tss` | Imports Unity's default runtime theme (needed for sliders/toggles) |
| `MainMenu.uxml` | Title panel + Options/Help instances |
| `LevelUi.uxml` | HUD, pause, results + Options/Help instances |
| `OptionsPanel.uxml`, `HelpPanel.uxml` | Shared templates used by both screens |

Scripts (`Scripts/UI`): `MainMenu` and `LevelUi` bind buttons and observe game state;
`OptionsPanel` binds the shared options template to `GameSettings`; `UiScreens` holds
panel switching, quit, and time formatting. UI never owns gameplay rules: `LevelUi` reads
`LevelFlow`/`PlayerFlight` and calls `Resume`, `Retry`, `Continue`, `ExitToMenu`.

**Options** (`Core/GameSettings`, saved in PlayerPrefs): steering sensitivity (50–150%) and
invert vertical, controller buzzer, **CRT display** (see below), and **Calibrate controller** (`CalibrationPanel`, a step wizard in `CalibrationWizard`). The game has an EMP sound effect and temporary entry voice clips. General volume controls are not implemented. Expanded sound and presentation settings remain planned.

**Keyboard throttle with controller (Q / E)** keeps the cockpit stick/buttons while using the keyboard's
latched throttle. The rail HUD identifies this combination and keeps Q/E zone hints visible. Sensor-loss
HUD/LCD warnings and the sonar-loss chirp are suppressed when the sensor is not the selected throttle.
Calibration continues to show the actual sensor readings.

**Planned UI work:** an overhaul of the flight HUD. It gives each kind of information one place (targets,
instructions, pointers), replaces the temporary missile panel and brings back the original scenes' working
UI. Settings and control guidance follow, along with a broader keyboard/mouse scheme and an Xbox mapping.
Startup reports and arena announcements need sound controls and captions.

**Quit** exits a desktop build; in the editor it stops Play Mode.

**Buttons** are `SlantButton`: drawn as a forward-leaning chevron via Painter2D, styled with
`--slant-*` USS properties (focus = white fill). Hovering with the mouse focuses a button, so mouse and
keyboard light it the same way and only one button is lit at a time. The title column leans to match.
The Orange focus marker follows the drawn left edge: its rotation is calculated from the button's current
height, lean and stroke inset, so PLAY and the shorter secondary buttons each remain parallel.

**Title** is a 3D mesh: "aXiom" in Earth Orbiter Bold, extruded, leaned 15° to match the buttons, with
flat chamfers (`Art/Models/AxiomTitle_EarthOrbiter.fbx`). The earlier Furore "AXIOM"
(`AxiomTitle_Furore.fbx`) is kept as an alternative: in MainMenu, `MenuTitle` holds `Title_EarthOrbiter`
(active) and `Title_Furore` (inactive); swap which is active. Both are built by
`ArtSource/Title/make_title.py` in Blender (commands in its header); its options build other fonts, texts
and shapes, always at the same 3.7 m width so the framing holds. Earth Orbiter is free for non-commercial
use only (`ArtSource/earth-orbiter/earthorbiter.txt`); Furore's file carries no license. In
MainMenu, `MenuTitle/TitleCamera` renders only the `Title` layer into `UI/MenuTitle.renderTexture`
(2048×512, transparent); `MainMenu` sets it as the background of the `game-title` element, so it
follows the UI layout at any resolution. The main camera excludes the `Title` layer. Shading comes
from `Title.mat` (`Title Facets` shader: key light from the upper right, a front fill focused on the face, and a
shadow floor, so the extrusion's visible left walls sit in shadow; values are linear, so a floor of 0.015
displays as about 12% grey). The titles are extruded 0.28 m; framing comes from the title camera's position and
field of view.

**Menu backdrop** (`MenuBackdrop`, `GeometricConstruct`): a hero construct and three simpler
ones at fixed depths (fog fades the far ones) over a scrolling floor. The floor is 420 m wide and the sky is
the fog colour, so the floor fades into the background with no visible edge; the horizon glow also lights the
far floor (depth-faded, beyond about 85 m) to hide the horizon line. A construct can combine a
counter-rotating star tetrahedron, a golden-ratio cage tumbling under Euler's rigid-body equations,
a trefoil-knot band, and satellites on Kepler orbits; keep each background one to a single idea.
MainMenu's floor and dust drift at 5 m/s. The shared entry study replaces the scrolling floor with one
endless grid (`InfiniteGround`) whose lines glide toward the camera at the same 5 m/s (**Glide Speed**, a
shader offset), with dust drifting alongside and floating slightly (**Dust Float**). At PLAY both stop as the
camera starts moving at that speed; the dust clears at the passage threshold. Its hero trefoil uses the existing bright Orange light material with a
0.025 m tube radius; the narrower line keeps the glow from dominating the title.

**Menu look** (`Presentation/Menu/MenuLook` on the MainMenu `MenuLook` object).
One Inspector, live in the editor:

The footer distinguishes **SYS ONLINE** (the Grid's fictional operating status) from the actual
**COCKPIT LINKED / OFFLINE** connection. Searching is explicitly labelled offline. The footer and corner brackets
stay through flight; only the menu key hints hide.

| Section | What it controls |
| --- | --- |
| Grade | URP volume on the menu camera: neutral tonemapping, exposure, contrast, bloom, vignette, film grain |
| Atmosphere | Haze (fog and sky colour, fog range), horizon band, stars, drifting dust (generated, not saved; `Additive Glow` shader) |
| Interface | Scrim, corner frame, status line (live cockpit link, Ember LED when not linked), key hints, Orange focus marker |
| Glitch | Signal glitch every 8–12 s while idle: tears, Orange echo, blocks, scanlines, fringe pulse, roll bar, title tear, construct snap/drop, status scramble. **Hold Peak** freezes it for tuning (editor only); the component menu has **Play glitch now** |
| Arcade mode | The shared `ArcadeLook` asset, applied while the player has Arcade Mode on (**Preview Arcade** shows it in the editor; builds ignore it) |

**Level look** (`Presentation/LevelLook`; Prototype: `00_CameraAndLighting/LevelLook`) gives a level the menu's
grade and atmosphere, plus Arcade Mode, with none of the menu-only parts (no glitch or interface details). Its
Grade and Atmosphere sections match the menu's, tuned per level: in the Prototype the haze runs 45–185 m and the
horizon sits at 200 m, inside the camera's 240 m far clip. The horizon band and stars travel with the camera; dust
stays put in the world and wraps around the camera, so it streams past in flight (leave **Dust Drift** at 0). The
floor is 420 m wide and runs past the far clip, so it fades into the sky like the menu's. Four of the menu's
constructs (pale neutral ones) float high beside the course as scenery
(`20_EnvironmentBlockout/Backdrop`) and emerge from the haze as you approach. Both look components share
`GradeSettings`/`AtmosphereSettings` and the runtime pieces `LookVolume` and `LookAtmosphere`.

`Foundation Flat` works out fog per pixel: the floor is two huge triangles, and per-vertex fog left its far end
brighter than the sky. The horizon glow's depth fade reads the camera depth texture, which only holds
`Foundation Flat` geometry when URP copies depth after the opaque pass. Anything that adds a depth prepass
(for example SSAO set to depth-normals, or depth priming) leaves that geometry out, and the glow then paints
over foreground objects. SSAO is therefore off on `PC_Renderer`; it had no visible effect on these unlit
shaders anyway. To use a prepass later, give `Foundation Flat` DepthOnly and DepthNormals passes first.

**CRT display** (Options toggle, `GameSettings.ArcadeMode`) gives the menu and the levels a vintage vector-monitor
look. Its settings live in one asset, `Settings/ArcadeLook` (`ArcadeProfile`), used by `MenuLook` on the menu and
`LevelLook` in each level. Each part can be switched off: phosphor glow (replaces the normal bloom), colour
fringing in palette Orange, CRT (curvature, scanlines, monochrome grille, tube edge), neutral grading
(exposure, contrast and saturation), a lighter vignette, vintage extras (rounded corners, flicker, static,
a rolling band and coarser grain), and the interface on the tube (below). Legacy hue shift, color filter
and white-balance fields remain serialized for compatibility but are neutralized at runtime. The display
setting is separate from the arena's Roll & Pitch / Bank & Turn handling choice.

**Interface on the tube** (`ArcadeProfile` Interface group). In Play Mode with Arcade Mode on, `ArcadeInterface`
renders the scene's UI documents into a screen-sized texture; the Screen Effects feature lays it into the picture
after post-processing (with its own glow) and before the CRT pass. Bloom and grade are skipped on purpose (they
washed out text and shifted the guidance blue). Pointer input follows the curvature
(`ArcadeInterface.ScreenToTexture`) and keyboard focus survives toggling.

The glitch, fringing, CRT and vintage extras run in `ScreenEffectsFeature` (on `PC_Renderer`, shader
`Screen Effects`), only for the registered camera and only while one is non-zero. Outside Arcade Mode's interface
option, the UI draws afterwards and is never processed. The title's
glitch is a separate blit of its render texture (`Menu Title Glitch`). The scrim, frame, footer and roll bar
are elements in `MainMenu.uxml`/`Axiom.uss` (gradients in `UI/Textures`); Orange marks focus and Ember marks
faults; state labels and geometry remain the primary explanation. Depth of field and URP's chromatic aberration are deliberately not used: the first blurs the thin
wireframes, the second splits light into off-palette rainbow colours.

**UI classification so far:**
- **Diegetic:** shield segments on the hull and throttle-responsive engine exhaust.
- **Non-diegetic:** the HUD (time, targets, throttle gauge with zone band and hint, missile panel), the guidance
  card and chips, menus and results.
- **Spatial:** aim reticles, target and aim brackets, the guidance flag, Cyan floor throttle cues, rings and
  portals.
- **Meta:** angular screen-edge damage feedback, and the entry's terminals and HUD assembly.

## Engine thrust and damage feedback

`Presentation/ThrustDisplay` on `AircraftVisual/Thrust` draws paired, faceted exhaust jets
at the existing rear engine ports. A white core, translucent outer facets, and three travelling
diamonds per engine keep the effect geometric. Length, width, and diamond motion increase
with the selected throttle. The response eases over about 0.1 seconds, so it communicates
engine demand promptly while the ship's actual speed catches up. Minimum throttle still has
a small jet because flight never stops. This uses the shared `PlayerFlight.TargetSpeed`, so
keyboard and controller inputs drive the same presentation without an extra input read.

Tune `lengthRange`, `response`, and the two port positions in the Inspector. Geometry uses
one reusable 240-vertex mesh and the `Geometric Exhaust` shader/material; it has no colliders,
lights, textures, or particle allocations per frame. Pause freezes the jets; destroying the
aircraft hides them, and disabling the component releases its generated mesh.

`UI/DamageFeedback` is a non-interactive UI Toolkit element beneath the HUD and menus.
`LevelUi` observes `ShipShield.Changed`: each accepted hit produces a single 0.55-second
angular edge pulse. Void-to-Ember facets and Hot inner lines provide contrast on both light and
dark scenery; geometry and timing carry the warning as well as color. The centre stays clear.
Invulnerability-blocked hits produce no pulse. The pulse decays using unscaled time, clears
when a menu opens, and is slightly stronger on the final hit. It adds no camera shake or
persistent low-shield flashing. Event subscriptions are removed when the level UI disables.

## Arena missile display

`MissileHUD` uses UI Toolkit (`MissileDisplay`) and is included in `LevelLook`'s interface documents, so the
bank and brackets follow Arcade Mode's screen curvature and glow. The bottom-right panel, a temporary piece due
for replacement, shows four bank
pips, highlights ammunition reserved by current locks, and gives the hold/release prompt. Empty pips fill
gradually as ammunition regenerates. Pause and destruction replace the prompt with their current state.

Acquiring targets have dim cyan corner brackets and a percentage; they brighten to white-cyan with a lock
number when ready. The ordinary gun-assist marker hides while LOCK is held. Markers project after the chase
camera moves, keeping them aligned during rolls. Enemy attacks use Orange/Ember with threat geometry; decorative Orange alone does not imply hostility.

The cockpit LCD shows the missile bank and painted count; HUD/LCD prompts name the LOCK button when the
connected firmware supports D11, otherwise Left Shift. Standby firmware shows K while the button is held.
Acquisition sends short rising buzzer tones and
launch sends a confirmation tone, respecting the existing buzzer option.

## Guidance

`GuidanceDisplay` draws lesson guidance into the HUD's screen, so Arcade Mode carries it. It renders what a lesson
gives it and knows nothing about lessons:
- **Flag:** marks one object. A short Cyan leader ends beside the object (never on it), under a tab with an action
  and the distance ("LOCK TARGET 368 m") on a Void backing. Off screen, a Cyan pointer at an inset edge, clear of
  the curve and the HUD corners, points the way.
- **Card:** sits under the timer. A Muted lesson line and one Text instruction show at once, while its Cyan rule
  draws down.
- **Key chips:** controls in brackets ("[LEFT SHIFT]") draw as forward-leaning key boxes in the menu buttons' lean.
  A chip fills Light while its control is held, which only means the input arrived.

Lessons take their lines from the systems' real state, so a held key never reads as success, and the flag steps
aside while weapon feedback shows the same object. Guidance is Cyan with Light emphasis; targets keep Orange. The
first lesson to use it is the arena's single LOCK target.

## Arena encounter display

`EncounterHUD` uses UI Toolkit and shares Arcade Mode's interface routing with the missile HUD. It shows the
arrival countdown, round number, remaining target count, and the SECTOR CLEAR/recovery beat. A portal marker
guides the player during the warning, including when the spawn points are behind the camera. World-space
orange octagonal portals grow during the five-second warning and collapse just after enemies arrive; they
have no collision or gameplay logic. Arrival and clear cues use the controller buzzer's existing setting.

Pause freezes portal motion and replaces the objective with PAUSED. Ship destruction shows RECONSTRUCTING,
then a fresh warning for the retry. Disable `ArenaEncounter` before Play Mode to hide this interface and return
to free practice.

The missile panel also shows the 90–650 m lock range and **CLOSE TARGET — USE GUNS** when a nearby target
is in the acquisition cone. Its EMP row shows **EMP READY / DOME or R**, then **EMP SPENT / ONE PER LIFE**.
`EmpDisplay` renders one dominant planar ring with faint spherical arcs and radial fragments. Its 0.22 s
compression beat precedes the 2.8 s expansion; a metallic bass impact and 900 ms rumble pulse begin when
the front starts moving. It also sends the D12 charge-lamp state and discharge LCD message. The EMP row shares the missile
UIDocument, so it follows Arcade Mode routing.
