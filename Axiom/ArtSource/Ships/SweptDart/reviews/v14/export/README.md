# Swept Dart import package

Prepared from the approved v13 construction sequence. v14 changes packaging and runtime data;
the white exterior, low-poly hull topology, interior and three-second choreography are retained.
Everything here remains outside Unity's `Assets` folder. Import is reserved for a future update.

## Contents

| File | Purpose |
| --- | --- |
| `SweptDart.fbx` | Eleven permanent surface meshes, one root and four sockets |
| `SweptDart_Interior.fbx` | Seven temporary interior groups, one root, no duplicate sockets |
| `SweptDart_Construction.fbx` | One temporary mesh containing 91 independently timed traces |
| `HullArrival.exr` | Baked v13 surface arrival field, including cell boundaries and grain |
| `TraceTimeline.exr` | Evaluated draw fraction, emission and visibility at 96 samples/second |
| `Shaders/` | Staged URP shader source; requires compilation and appearance checks after import |
| `material-bindings.json` | Named material recipes using the project palette |
| `package-manifest.json` | Mesh, material, socket, timing and UV contracts |
| `timeline.json` | Trace identities, path centres, radii and phase times |
| `validation.json`, `data-validation.json` | Completed Blender and numerical checks |

[Construction review](../qa/sequence-sheet.jpg), [three-second playback](../qa/runtime-sequence.mp4) and [shield states](../qa/shield-sheet.jpg)
show the staged geometry and baked data in Blender. These are not Unity captures.

## Import settings for the future update

Target Unity 6000.3.24f1 and the installed URP version. Keep the gameplay root, collider, movement,
weapons and health components. Replace only the `Visual` child after an isolated import proof.

1. Create the destination folders through the Editor. Copy the three FBXs, two EXRs and staged shaders
   once, then retain all generated `.meta` files and GUIDs for later revisions. Do not copy the review
   Blender files into `Assets`; avoid implicit Blender imports and duplicate models.
2. Model settings: Scale Factor **1**, Convert Units **on**, Bake Axis Conversion **on**, normals
   **Import**, tangents **None**, Mesh Compression **Off**, Read/Write **off**, Generate Lightmap UVs
   **off**, Generate Colliders **off**, import cameras/lights/animation **off**, rig **None**.
   Keep the hierarchy and renderer names. These settings are a starting recipe, not a verified Unity result.
3. Verify metres and orientation before integration: source is Blender -Y forward/+Z up; the FBX has
   baked axis conversion. Unity must resolve to +Z forward/+Y up with no compensating scale on `Visual`.
   Hull bounds are 4 m wide, 4.14 m long and 0.79 m tall. Nose is Z +2.5; rear is Z -1.64.
4. Import both textures as **Default**, **sRGB off**, **mipmaps off**, **compression None**, wrap **Clamp**,
   NPOT scaling **None**. Keep original dimensions. `HullArrival`: bilinear, 2048 x 2048, preferably
   RHalf (RGBAHalf is a compatible larger fallback). `TraceTimeline`: point, 289 x 128, RGBAHalf or
   RGBAFloat. Never resize the timeline: its row/sample addressing is exact.
5. Create shared palette materials from `material-bindings.json` and remap the FBX source slots using
   the importer's external material remaps. Do not extract or depend on Blender's embedded shader graphs.
   Register any new material names with the existing palette audit during integration.
6. Instantiate one `SweptDart` surface root under `Visual`. Add the interior and construction meshes
   under that same visual frame with identity transforms. Auxiliary FBX roots contain no extra sockets.
   Preserve the existing gameplay collision box; the render hull is not a replacement collider.

The shaders use the project's existing flat facet lighting approach and explicit palette emission.
They do not receive real-time lights or cast shadows. Bloom and CRT stay with the scene's existing
presentation. Blender roughness/metallic values in the manifest are source references; the staged flat
shader intentionally uses its facet shading control instead.
Traces use additive, depth-tested rendering. Overlapping brightness and bloom require a Unity review;
the Blender checks establish geometry and timing, not identical runtime pixels.

## Construction playback

Set `_BuildProgress` consistently on all surface, interior and trace materials. Zero is empty; one is
complete. The shaders use `referenceSeconds = saturate(_BuildProgress) * 3`. The caller controls actual
duration, pause and replay. This does not change the entry's authored window or control handover.

- UV0 is the surface layout used by the baked hull field. UV1 is reserved and unused.
- UV2 (`Mesh.uv3`, `TEXCOORD2`) stores arrival seconds / 3 in U and branch progress in V.
  Interior arrival is constant per original part, even though the 57 parts are grouped into seven meshes.
- UV3 (`Mesh.uv4`, `TEXCOORD3`) stores the trace timeline row centre in U. V is reserved.
  Every triangle belongs to one trace. Keep all four UV channels on import.
- Hull arrival comes from the texture's R channel; the vertex arrival is the underlying regional field.
  The texture preserves detail that the coarse hull cannot represent with vertex data alone.
- The trace shader manually interpolates R/G between neighboring time samples and uses the lower
  sample's B visibility. It preserves local retraces and brightness dips without 91 material instances.
- Interiors retire at 2.9167 reference seconds. Traces retire by their own recorded times. Permanent
  systems arrive at 2.88 seconds. Disable the seven interior renderers and one trace renderer when
  construction completes; restore them before replay and reset progress to zero.

Use a controlled set of material instances for the player, or measured property blocks. Do not mutate
project-wide shared materials every frame. Property blocks can affect SRP Batcher eligibility; profile
the chosen integration. No controller, scene binding or automatic material mutation is included here.

## Shield and damage bindings

Bind `ShieldDisplay.segments` in this order: `Shield_Segment_1`, `Shield_Segment_2`,
`Shield_Segment_3` (left to right at local X -0.1, 0, +0.1). Existing behavior removes the last first.
Lit segments use Light emission 2.5; lost segments use `Shield_Lost_Edge`, non-emissive Edge.
The four review images show 3, 2, 1 and 0 lit segments. Zero is a visual state, not a new survival rule.

Bind only the **eleven permanent surface renderers** listed in `shield.blink_renderers` to the existing
whole-ship damage blink. Do not put temporary internals or traces in that array: the blink must not
re-enable retired construction geometry. Keep the current hit protection, blink cadence and death logic.
All material variants used during construction must receive the same progress; avoid a shield material
swap restoring its default completed state mid-build.

Socket positions are explicit in the manifest. Both muzzle frames use local +Z forward. Exhaust sockets
share the ship's reference frame; exhaust travels toward local -Z. Verify actual imported orientations,
not only the custom properties, before attaching weapons or exhaust.

## Cost and completed checks

Normal flight: **11 meshes, 3,484 triangles, 15 material submeshes**. Construction adds **7 interior meshes /
2,132 triangles** and **1 trace mesh / 18,240 triangles**: **23,856 triangles and 39 material submeshes** in
total. These are geometry/submesh counts, not measured draw calls. All temporary geometry can be disabled
in normal flight. No LOD is supplied for the player; assess it at the actual chase-camera size first.

The surface arrival texture needs about 8 MiB as RHalf; the timeline needs about 289 KiB as RGBAHalf,
without mipmaps. Source EXR sizes are not runtime memory measurements. An RGBAHalf hull fallback needs
32 MiB. Review memory and overdraw along with normal/CRT frame times before accepting the runtime cost.

Completed outside Unity:

- All 71 source mesh hashes and the saved v13 source file remain unchanged.
- All 19 exported meshes round-trip through FBX with triangle positions, winding, material indices and
  all four UV channels preserved within 0.00001 tolerance; four socket positions also pass.
- All 91 trace rows are bound. Empty and completed visibility, per-part arrival and palette values pass.
- Timing interpolation at 96 Hz differs from evaluated Blender animation by at most 0.00528 of a spline's
  draw fraction and 1.034 emission units between samples. Visibility matches exactly at the checked times.
  Authored 24 fps frame times fall directly on the lookup samples.
- The construction and shield sheets were visually inspected using the staged geometry and textures.

Still to verify during the later Unity update: shader compilation, actual FBX axes and UV mapping,
material remaps and palette audit, entry/replay/damage integration, moving chase-camera readability,
normal and CRT appearance, draw calls, overdraw and memory. No Unity import or game testing has occurred.

## Rebuild

The Blender sources are kept locally and aren't in this repository. From `ArtSource/Ships/SweptDart`, run background Blender on `reviews/v13/SweptDart_v13.blend` with
`scripts/prepare_runtime_package.py`. Validated exports are protected unless `-- --rebuild` is explicitly
passed for this staging revision. Run `scripts/validate_runtime_data.py` on the resulting
`reviews/v14/export/SweptDart_export.blend`, followed by `scripts/review_runtime_package.py` for review
images. The accepted v13 source and the working Blender file are never overwritten.

URP implementation reference: [Unity 6.3 custom shaders](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/writing-custom-shaders-urp).
