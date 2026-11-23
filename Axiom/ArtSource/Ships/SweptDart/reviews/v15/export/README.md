# Swept Dart v15

This import package carries the v13 three-second construction sequence, the v14
runtime data, and three revised FBXs. The v15 FBX measurements are in
[the validation report](validation.json).

| File | Contents |
| --- | --- |
| `SweptDart.fbx` | Hull, permanent lights, shield segments and four sockets |
| `SweptDart_Interior.fbx` | Seven interior groups shown during construction |
| `SweptDart_Construction.fbx` | The 91 construction traces |
| `HullArrival.exr`, `TraceTimeline.exr`, `Shaders/` | Build timing and surface and trace shaders |
| `material-bindings.json`, `timeline.json` | Material recipes and trace timing |

For the isolated reimport, replace the three FBXs in the existing model folder
and retain their .meta files. Keep Scale Factor 1, Convert Units on and Bake Axis
Conversion on. The nose belongs at local Z +2.5 m and the left muzzle at
(−0.75, 0, +1.10) m. Both muzzles face +Z. Use the
[manifest](package-manifest.json) for all socket frames, mesh names and counts.
The [SHA-256 list](checksums.json) covers the files in the export folder.

[Isolated Unity import results](../qa/unity-import-proof.json) for these FBXs.
