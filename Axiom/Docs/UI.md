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
invert vertical. Volume is left out until the game has audio. Screen shake is left out because shake doesn't exist yet.

**Quit** exits a desktop build; in the editor it stops Play Mode.

**Buttons** are `SlantButton`: drawn as a forward-leaning chevron via Painter2D, styled with
`--slant-*` USS properties (focus = white fill). The title column leans to match.

**Menu backdrop** (`MenuBackdrop`, `GeometricConstruct`): a hero construct and three simpler
ones at fixed depths (fog fades the far ones) over a scrolling floor. A construct can combine a
counter-rotating star tetrahedron, a golden-ratio cage tumbling under Euler's rigid-body equations,
a trefoil-knot band, and satellites on Kepler orbits; keep each background one to a single idea.

**UI classification so far:** diegetic: shield segments on the hull; non-diegetic: HUD
(time, targets, throttle gauge with zone band and hint), menus, results with par; spatial:
aim reticles and blue floor throttle cues.
