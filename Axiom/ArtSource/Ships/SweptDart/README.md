# Swept Dart

The player ship: a white low-poly exterior, a modelled interior and a three-second materialization sequence.
The import package is prepared for a future Unity update; it is not in the game yet.

## Showcase

- [Finished ship views](reviews/v13/presentation/ship-sheet.jpg), with 4K [front](reviews/v13/presentation/ship-front.png),
  [rear](reviews/v13/presentation/ship-rear.png) and [underside](reviews/v13/presentation/ship-underside.png) stills.
- [Sequence stages](reviews/v13/presentation/sequence-sheet.jpg).
- [1080p sequence](reviews/v13/presentation/sequence-normal.mp4) and [quarter-speed sequence](reviews/v13/presentation/sequence-slow.mp4).

These are Cycles studio renders, not Unity captures.

## Current work

- [Import package](reviews/v14/export/README.md): FBXs, baked reveal data, shader source, material recipes and import checks.
- [Construction review](reviews/v14/qa/sequence-sheet.jpg), [playback](reviews/v14/qa/runtime-sequence.mp4),
  [shield states](reviews/v14/qa/shield-sheet.jpg) and [comparison with the studio sequence](reviews/v14/qa/comparison.jpg).
- [Technical notes](docs/TECHNICAL_NOTES.md): geometry, palette, the animation contract and coordinates.

## Layout

| Location | Purpose |
| --- | --- |
| `reviews/v13/presentation/` | Showcase renders |
| `reviews/v14/export/` | Current import package |
| `reviews/v14/qa/` | Review of the exported data in Blender |
| `scripts/` | Geometry builders, reveal fields, validators, render and composition tools |
| `docs/` | Technical notes |

The scripts run against the Blender source files, which are kept locally along with earlier versions, full
render sets and research material.

The hull uses Light `#E6FFFF` and Text `#B7C9D1`, with dark recesses and selective Cyan/Light emission.
Source units are metres, Blender -Y forward and +Z up.
