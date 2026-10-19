# Axiom

Unity 6.3 URP rail-shooter for UI coursework. Runtime code uses the `GeometricFlight`
namespace.

## Open and play

1. Open this folder (`Axiom`, containing `Assets`, `Packages`, and `ProjectSettings`)
   in Unity Hub with the installed **6000.3.24f1** editor.
2. Open `Assets/Scenes/MainMenu.unity`, set the Game view to **16:9**, press Play,
   and click the Game view. Menus work with mouse or keyboard (arrows, Enter, Esc).
3. Controls: **WASD/arrows** steer, **Q/E** throttle (it holds when released),
   **Space** (hold) fire, **Esc** pause. The Arduino controller is optional and connects
   on its own: see [Arduino/README.md](Arduino/README.md).

`Prototype.unity` is the first course and can also be played directly.

## Scenes

| Scene | Contents |
| --- | --- |
| `MainMenu` | Title, Options, Help, Quit (build index 0) |
| `Prototype` | Course 1: player, blockout, targets, `LevelFlow` (50_Systems), `LevelUi` (40_UI) |

Forward is **+Z**, up is **+Y**, right is **+X**. `PrototypeMesh` generates the blockout
meshes in Edit and Play Mode; keep the component or bake the meshes before removing it.

## Docs

- [Flight](Docs/FLIGHT.md): movement, throttle, input.
- [Combat](Docs/COMBAT.md): weapons, targets, shield, enemies, run flow.
- [UI](Docs/UI.md): screens, theme, settings.
- Controller: [kit controller (v1)](Arduino/README.md) and [cockpit box (v2)](Arduino/cockpit/README.md).
- [Development plan](Docs/DEVELOPMENT_PLAN.md) and [design brief](Docs/DESIGN_BRIEF.md).

Run `GeometricFlight.EditorTests` in the EditMode Test Runner. **Tools > Geometric Flight >
Check Foundation** checks scene references.
