# UI

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
invert vertical, controller buzzer, and **Calibrate controller** (`CalibrationPanel`, a step wizard in `CalibrationWizard`). Volume is left out until the game has audio. Screen shake is left out because shake doesn't exist yet.

**Quit** exits a desktop build; in the editor it stops Play Mode.

**Buttons** are `SlantButton`: drawn as a forward-leaning chevron via Painter2D, styled with
`--slant-*` USS properties (focus = white fill). The title column leans to match.

**Menu backdrop** (`MenuBackdrop`, `GeometricConstruct`): a hero construct and three simpler
ones at fixed depths (fog fades the far ones) over a scrolling floor. A construct can combine a
counter-rotating star tetrahedron, a golden-ratio cage tumbling under Euler's rigid-body equations,
a trefoil-knot band, and satellites on Kepler orbits; keep each background one to a single idea.

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
