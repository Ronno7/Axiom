# Codex handoff

## Current milestone

The original static foundation now has **forward flight with smooth throttle and bounded
keyboard steering**, plus a camera that follows forward travel. Read `README.md`,
`Docs/DESIGN_BRIEF.md`, and `Docs/FLIGHT.md`. Develop in small, requested milestones.

The Unity project is the nested `Axiom` folder within the repository. Assets live
directly below `Assets`; the original `Assets/GeometricFlight` layout is obsolete.
Use Unity **6000.3.24f1**, URP **17.3.0**, and Input System **1.20.0**. Preserve the
installed package versions, including the user's Unity MCP installation.

## Current files

| Path | Contents |
| --- | --- |
| `Assets/Scenes/Prototype.unity` | Editable blockout with input, flight, and camera components wired |
| `Assets/Scripts/Input` | Shared steering/throttle/fire/pause snapshot and keyboard Input System adapter |
| `Assets/Scripts/Player` | Stateless movement rules, player component, forward-following camera |
| `Assets/Scripts/Presentation/PrototypeMesh.cs` | Edit/Play Mode geometry |
| `Assets/Scripts/GeometricFlight.Runtime.asmdef` | Small runtime assembly referencing Input System |
| `Assets/Tests/Editor` | Deterministic movement tests in a separate Editor test assembly |
| `Assets/Art/Shaders/FoundationFlat.shader` | Fixed-shading URP shader |
| `Assets/Art/Materials` | Five monochrome materials |
| `Assets/Editor/FoundationTools.cs` | Open-scene and reference-check commands |
| `Arduino/README.md` | Scope marker for future controller work |

## Conventions

- Namespace remains `GeometricFlight`; Unity product name is Axiom.
- Forward is world +Z, vertical is +Y, horizontal is +X. This milestone is a straight course.
- `PlayerFlight` depends on the abstract `FlightInputSource`, not keyboard hardware.
  Add a replacement adapter for Arduino when that milestone starts.
- Keep visual banking below the player root so movement, collision, and aiming stay stable.
- Preserve scene edits, `.meta` files, and GUIDs. No runtime scene regeneration.
- Keep editor utilities in `Editor` and avoid import-time project-settings changes.
- Use a few understandable components; introduce patterns only for concrete needs.
- Built-in primitives remain visual meshes without colliders.
- The monochrome shader uses fixed shading; lighting and effects are later work.

## Validation and remaining work

2026-09-28 throttle update: generated runtime and test projects compile with zero
warnings/errors. All 25 movement cases passed via a temporary .NET harness using
installed Unity math types, including acceleration travel at 30/60/144 simulated
FPS. This was not a Unity Test Runner run. Unity MCP was unavailable, so live
Q/E/both-key/release checks, steering/camera at different speeds, and two Play Mode
restarts remain pending. Run the acceptance pass in `FLIGHT.md` before marking M1
complete. Space/Escape only populate input intent; firing and pausing are future work.
The following Unity results belong to the earlier flight implementation.

All 14 deterministic EditMode tests passed in Unity 6000.3.24f1. Five live virtual
keyboard phases also passed, covering both key sets, opposite corners, neutral
and opposing input, forward travel, and camera tracking. Initial and upper-right
framing were visually checked. No compilation or gameplay errors were reported.
See `FLIGHT.md` for details and limitations. Recheck Console and Game view after changes.

The prototype course ends around Z=112; automatic forward flight continues beyond
it. Restart Play Mode for another pass. Baseline commit `27890f4` contains the
earlier verified flight prototype. Build settings have user changes; preserve them.

Still to implement: visual banking, shooting/aim assist, health and
collision, enemy behavior, progression, menus/UI, sound, Arduino firmware/serial
transport and calibration, and finished levels. Placeholder folders and object
names are not evidence of implemented systems.
