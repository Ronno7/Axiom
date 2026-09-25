# Axiom prototype

Unity 6.3 URP flight-game project for UI coursework. Runtime code retains the
`GeometricFlight` namespace and the existing asset GUIDs.

## Open and play

1. Open this folder (`Axiom`, containing `Assets`, `Packages`, and `ProjectSettings`)
   in Unity Hub with the installed **6000.3.24f1** editor.
2. Open `Assets/Scenes/Prototype.unity`, or use **Tools > Geometric Flight > Open Prototype**.
3. Set the Game view to **16:9**, press Play, and click the Game view.
4. Steer with **WASD or arrow keys**. Forward travel starts automatically.
5. Stop and restart Play Mode to reset the short course.

The current milestone implements constant forward movement, bounded horizontal
and vertical steering, and a camera that follows forward travel. See
[flight controls and design](Docs/FLIGHT.md) for Inspector settings, component
responsibilities, and verification instructions.

The course ends around Z=112; travel currently continues beyond it. Throttle,
combat, collision response, menus, level completion, and Arduino integration
remain future work. Build settings still contain the template SampleScene;
use Prototype directly in the editor for this milestone.

## Scene and assets

The scene contains 66 editable GameObjects, with a triangular aircraft, corridor,
pillars, floor markings, three hexagonal gates, and three target silhouettes.

| Root | Purpose |
| --- | --- |
| `00_CameraAndLighting` | Forward-following camera, audio listener, reserved light |
| `10_PlayerPrototype` | Flight/input components, aircraft visual, decorative shields, muzzle placeholders |
| `20_EnvironmentBlockout` | Corridor, gates, and start/end markers |
| `30_TargetPlaceholders` | Static enemy silhouettes |
| `40_UIReserved` | Future UI scene objects |
| `50_SystemsReserved` | Future game systems |

Forward is **+Z**, up is **+Y**, and right is **+X**. Steering does not rotate the
player root. Keep future visual banking under `AircraftVisual`.

`PrototypeMesh` supplies temporary meshes in Edit and Play Modes. Its serialized
settings are the source of truth; retain the component or bake the meshes before
removing it. `Foundation Flat` is a URP shader with fixed directional shading;
it does not receive realtime lights or shadows. Shields and targets are visual
placeholders without health or collision behavior.

Use **Tools > Geometric Flight > Check Foundation** for scene reference checks.
Run `GeometricFlight.EditorTests` from Unity's EditMode Test Runner for movement
regression checks. See [the handoff](Docs/CODEX_HANDOFF.md) and
[the design brief](Docs/DESIGN_BRIEF.md) before extending the milestone.
