# Folder guide

| Folder | Intended contents |
| --- | --- |
| `Assets/Scenes` | Prototype, then menu/level scenes when needed |
| `Art/Materials` | Shared material assets |
| `Art/Shaders` | Project shaders |
| `Art/Models` | Imported or saved geometry |
| `Art/Textures` | Textures when needed |
| `Audio/Music`, `Audio/SFX` | Music and sound effects |
| `Prefabs/Player`, `Enemies`, `Environment`, `UI` | Reusable prefabs as systems develop |
| `Scripts/Core` | Game state and coordination |
| `Scripts/Input` | Keyboard and Arduino input adapters |
| `Scripts/Player` | Flight, throttle, and camera behavior |
| `Scripts/Combat` | Weapons, targets, and damage |
| `Scripts/UI` | UI behavior and presentation binding |
| `Scripts/Presentation` | Geometry and visual-only components |
| `Settings` | Shared configuration assets |
| `UI` | UI documents, fonts, sprites, and styles as needed |
| `Editor` | Tools that run in the Unity editor |
| `Docs` at project root | Design and handoff documentation |
| `Arduino` at project root | Controller sketches, wiring, enclosure and cockpit model |

Paths below `Scenes` in this table are relative to `Assets`. Empty folders contain `.gitkeep` placeholders so Git retains the structure. They are organizational placeholders, not implemented systems.
