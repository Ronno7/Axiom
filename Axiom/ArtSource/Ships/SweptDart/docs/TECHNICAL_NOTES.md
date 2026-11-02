# Swept Dart technical notes

The ship is authored in Blender 5.2.1 LTS and isn't imported into Unity yet. One continuous welded hull carries
the recesses and vents. A separate interior of paired drive cores and collars, a reclined seat and controls,
keel, wing supports and power feeds appears before the skin closes. The four cockpit clearance guides are
conceptual volumes, not a validated human fit.

The sequence is three seconds. Control rings start it, simple volumes spread outward and mechanical contours refine,
with parts becoming solid locally while others are still developing. The exterior grows inward from several edges and
outward from the center. The [import package](../reviews/v14/export/README.md) carries this as a baked hull arrival
texture and a sampled trace timeline, with shield states, FBXs, four UV channels, material recipes, staged shader source and
validation. Unity import, shader compilation and runtime checks are still to come.

## Palette and finish

The solid hull uses **Light #E6FFFF** with **Text #B7C9D1** secondary facets, read directly from
`../../Palette/palette.json`. Neither emits light. **Void** recesses, **Edge** vent detail and a **Structure** carrier
behind the three shield marks retain contrast. Only system and construction materials emit **Cyan/Light**. Lighting creates
natural intermediate shades; renders aren't quantized. Blender studio lighting doesn't represent final URP, bloom or CRT output.

## v13 animation contract

The 72 frames at 24 fps represent three seconds. Small control rings begin at frame 2; refined
mechanical contours begin at frame 15. Each interior part becomes solid 0.04 s after its own contour
completion. The exterior begins filling around the middle of the sequence, before all contours are
complete, and its fronts continue spreading through the later frames. There is no global wireframe
hold. Regional fill and its bright front settle by 2.855 s. Permanent systems
arrive at 2.88 s. All temporary curves and interior meshes stop rendering before the last frame.
The slow review uses the same frames at four times their duration.

There are 22 coarse guide objects, 57 feature-contour objects and 12 exterior contour objects.
Refinement excludes coplanar triangulation diagonals. The local brightness dips/retraces are
deterministic animation, not random whole-screen flicker. Curves and shader coordinates travel
with the model, but motion through a Unity scene has not been tested.

The surface study combines perimeter-inward and control-center-outward arrivals, taking whichever
reaches each location first. Several edges start with small timing offsets. Fine procedural cellular
boundaries and static grain break up the advancing fronts. The perimeter is an authored envelope
approximation; these are proposed construction regions, not authored panel seams.
Glare is a Blender compositor effect. The 91 editable curves and separate preview materials are
not a proposed runtime renderer budget; consolidate and encode phases after visual review.

## Geometry and coordinates

One Blender unit is one metre. Source coordinates are Blender -Y forward, +Z up; the generator accepts Unity-local +Z
forward, +Y up and converts `(x, y, z)` to `(x, -z, y)`. The root is at the existing player visual origin. The staged FBX uses
baked -Z forward/+Y up axis conversion. A Blender FBX round trip preserves world bounds and socket positions within 0.00001 m.
Actual Unity importer settings, orientation, materials and socket directions still need checking in the Editor.

The hull is a single connected, closed mesh with 1,564 triangles and exact mirrored vertices, with no subdivision. Bounds are
4 x 4.14 x 0.79 m, with the nose at Z +2.5 m and the exhaust plane at Z -1.64 m. The interior adds 57 editable meshes and
2,132 triangles. All are closed, and checks find no surface intersections between interior parts or with the hull.
These checks don't establish mechanical attachment or ergonomics.

## Scripts

- `build_interior.py`, `build_cohesive.py`, `build_blockout.py`: the generators for the current model.
- `overlap_order.py`, `build_luminous_preview.py`: the reveal fields and the animated scene.
- `prepare_runtime_package.py`, `finalize_runtime_package.py`, `validate_runtime_data.py`, `review_runtime_package.py`: the import package and its checks.
- `render_presentation.py`, `compose_presentation.py`: 4K stills, 1080p video and contact sheets.

The other scripts built and checked earlier stages. All of them run against the Blender source files, which aren't in this repository.
