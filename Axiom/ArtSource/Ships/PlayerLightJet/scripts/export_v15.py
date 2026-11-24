"""Re-export the accepted runtime meshes for Unity, using background Blender 5.2.

Run with --background --factory-startup --python-exit-code 1 --python export_v15.py.
Only the three FBXs in reviews/v15/export are written.
"""
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix


SHIP = Path(__file__).resolve().parents[1]
BASE = SHIP / "reviews/v14/export"
OUTPUT = SHIP / "reviews/v15/export"
FBX_GROUPS = {
    "SweptDart.fbx": {"hull", "permanent_emission", "shield_state"},
    "SweptDart_Interior.fbx": {"interior_reveal"},
    "SweptDart_Construction.fbx": {"construction_only"},
}


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def export():
    if (OUTPUT / "validation.json").exists() and "--rebuild" not in sys.argv:
        raise FileExistsError("v15 is already validated. Use -- --rebuild to regenerate its FBXs.")
    manifest = json.loads((BASE / "package-manifest.json").read_text(encoding="utf-8"))
    hashes = json.loads((BASE / "checksums.json").read_text(encoding="utf-8"))
    stage_file = BASE / "SweptDart_export.blend"
    if digest(stage_file) != hashes[stage_file.name]:
        raise ValueError("The v14 runtime mesh source differs from its recorded checksum")
    if digest(SHIP / "reviews/v13/SweptDart_v13.blend") != manifest["source_sha256"]:
        raise ValueError("The approved v13 source differs from the package manifest")

    # Load only the prepared runtime scene. Historical objects in the authoring
    # file used the same names and forced suffixes onto the old FBX nodes.
    scene_name = "Swept Dart runtime package | v14"
    with bpy.data.libraries.load(str(stage_file), link=False) as (_, imported):
        imported.scenes = [scene_name]
    scene = imported.scenes[0]
    bpy.context.window.scene = scene
    objects = {obj["asset_id"]: obj for obj in scene.objects}
    sockets = {item["name"]: item for item in manifest["sockets"]}
    expected = {"SweptDart", *manifest["meshes"], *sockets}
    if set(objects) != expected or len(scene.objects) != len(expected):
        raise ValueError("Unexpected runtime hierarchy")

    # With the Y-up v14 header, Unity's baked import maps raw (x,y,z) to
    # (x,y,-z). Authoring coordinates are (x,-forward,up), so write (x,up,y)
    # to the FBX. This swaps handedness; reverse each face with its UV corners.
    to_fbx = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    root = objects["SweptDart"]
    root["revision"] = "v15"
    root["axis_contract"] = "Metres, Unity +Z forward and +Y up with Bake Axis Conversion on."
    for name, obj in objects.items():
        obj.name = name
        if obj.name != name:
            raise ValueError(f"FBX name collision for {name}")
        if obj != root and obj.parent != root:
            raise ValueError(f"Unexpected parent for {name}")
        if name in sockets:
            x, y, z = sockets[name]["unity_local_position"]
            obj.matrix_basis = Matrix.Translation((x, y, -z))
        elif not obj.matrix_basis.is_identity:
            raise ValueError(f"Expected an identity transform for {name}")
        if obj.type == "MESH":
            obj.data.name = name
            obj.data.transform(to_fbx)
            obj.data.flip_normals()
            obj.data.update()
    bpy.context.view_layer.update()

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for filename, roles in FBX_GROUPS.items():
        include_sockets = filename == "SweptDart.fbx"
        for name, obj in objects.items():
            selected = obj == root or obj.get("role") in roles or (include_sockets and name in sockets)
            obj.select_set(selected)
        result = bpy.ops.export_scene.fbx(
            filepath=str(OUTPUT / filename), use_selection=True,
            object_types={"MESH", "EMPTY"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z", axis_up="Y", use_space_transform=False, bake_space_transform=False,
            use_mesh_modifiers=False, use_triangles=True, bake_anim=False,
            use_custom_props=True, add_leaf_bones=False, path_mode="STRIP", embed_textures=False,
        )
        if result != {"FINISHED"}:
            raise RuntimeError(f"Could not export {filename}: {result}")


if __name__ == "__main__":
    if not bpy.app.background:
        raise RuntimeError("Run this exporter in a separate background Blender process")
    export()
