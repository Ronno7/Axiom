# UI

For a visual tour, see the [UI showcase](https://ronno7.github.io/Axiom/) (source in `docs/`).

**Approach:** UI Toolkit (UXML + USS) for menus and the HUD; uGUI world-space canvases are
reserved for later spatial markers (target brackets). Each scene has one `UIDocument` using
`Assets/UI/AxiomPanelSettings` (1920×1080 reference, match 0.5) and an `EventSystem` with
`InputSystemUIInputModule`, so menus work with mouse and keyboard (arrows, Enter, Esc).

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
invert vertical, controller buzzer, **Arcade mode** (see below), and **Calibrate controller** (`CalibrationPanel`, a step wizard in `CalibrationWizard`). Volume is left out until the game has audio. Screen shake is left out because shake doesn't exist yet.

**Quit** exits a desktop build; in the editor it stops Play Mode.

**Buttons** are `SlantButton`: drawn as a forward-leaning chevron via Painter2D, styled with
`--slant-*` USS properties (focus = white fill). The title column leans to match.

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

**Menu look** (`Presentation/Menu/MenuLook` on the MainMenu `MenuLook` object).
One Inspector, live in the editor:

| Section | What it controls |
| --- | --- |
| Grade | URP volume on the menu camera: neutral tonemapping, exposure, contrast, bloom, vignette, film grain |
| Atmosphere | Haze (fog and sky colour, fog range), horizon band, stars, drifting dust (generated, not saved; `Additive Glow` shader) |
| Interface | Scrim, corner frame, status line (live cockpit link, red LED when not linked), key hints, red focus marker |
| Glitch | Signal glitch every 8–12 s while idle: tears, red echo, blocks, scanlines, fringe pulse, roll bar, title tear, construct snap/drop, status scramble. **Hold Peak** freezes it for tuning; the component menu has **Play glitch now** |
| Arcade mode | The shared `ArcadeLook` asset, applied while the player has Arcade Mode on (**Preview Arcade** shows it in the editor) |

**Level look** (`Presentation/LevelLook`; Prototype: `00_CameraAndLighting/LevelLook`) gives a level the menu's
grade and atmosphere, plus Arcade Mode, with none of the menu-only parts (no glitch or interface details). Its
Grade and Atmosphere sections match the menu's, tuned per level: in the Prototype the haze runs 45–185 m and the
horizon sits at 200 m, inside the camera's 240 m far clip. The horizon band and stars travel with the camera; dust
stays put in the world and wraps around the camera, so it streams past in flight (leave **Dust Drift** at 0). The
floor is 420 m wide and runs past the far clip, so it fades into the sky like the menu's. Four of the menu's
constructs (white ones only; red stays for danger) float high beside the course as scenery
(`20_EnvironmentBlockout/Backdrop`) and emerge from the haze as you approach. Both look components share
`GradeSettings`/`AtmosphereSettings` and the runtime pieces `LookVolume` and `LookAtmosphere`.

`Foundation Flat` works out fog per pixel: the floor is two huge triangles, and per-vertex fog left its far end
brighter than the sky.

**Arcade mode** (Options toggle, `GameSettings.ArcadeMode`) gives the menu and the levels a vintage vector-monitor
look. Its settings live in one asset, `Settings/ArcadeLook` (`ArcadeProfile`), used by `MenuLook` on the menu and
`LevelLook` in each level. Each part can be switched off: phosphor glow (replaces the normal bloom), colour
fringing in the accent red only, CRT (curvature, scanlines, monochrome grille, tube edge), colour (exposure and
contrast added to the scene's grade, saturation, colour filter, hue shift, white balance), a lighter vignette, and
vintage extras (rounded tube corners, flicker, static, a rolling band, coarser grain). The scene is mostly black
and white, so saturation mainly sets how hard the accent red burns; hue shift moves the red too.

The glitch, fringing, CRT and vintage extras run in `ScreenEffectsFeature` (on `PC_Renderer`, shader
`Screen Effects`), only for the registered camera and only while one is non-zero; the UI draws afterwards and is never processed. The title's
glitch is a separate blit of its render texture (`Menu Title Glitch`). The scrim, frame, footer and roll bar
are elements in `MainMenu.uxml`/`Axiom.uss` (gradients in `UI/Textures`); red stays reserved for focus and
problems. Depth of field and URP's chromatic aberration are deliberately not used: the first blurs the thin
wireframes, the second splits light into off-palette rainbow colours.

**UI classification so far:** diegetic: shield segments on the hull and throttle-responsive engine exhaust; non-diegetic: HUD
(time, targets, throttle gauge with zone band and hint), menus, results with par; spatial:
aim reticles and blue floor throttle cues; meta: angular screen-edge damage feedback.

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
angular edge pulse. Dark red facets and pale inner lines provide contrast on both light and
dark scenery; geometry and timing carry the warning as well as color. The centre stays clear.
Invulnerability-blocked hits produce no pulse. The pulse decays using unscaled time, clears
when a menu opens, and is slightly stronger on the final hit. It adds no camera shake or
persistent low-shield flashing. Event subscriptions are removed when the level UI disables.

Verified 2026-10-19 in Unity 6000.3.24f1: all 91 existing EditMode tests passed. A live
Play Mode check exercised minimum/maximum throttle through the keyboard adapter's setpoint,
pause freeze, accepted versus invulnerability-blocked hits, pulse expiry, menu clearing,
fatal-hit exhaust removal, and retry reset (one exhaust component, 240 vertices, no active
pulse, three shield segments). Game-view captures checked cruise/full-throttle exhaust and
impact feedback at 16:9. Console reported no project errors. Physical controller operation,
other aspect ratios, and a standalone build were not tested in this pass.
