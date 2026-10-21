"""Axiom title: the game name in a display font, extruded, sheared to the menu's forward lean, with
chamfered edges.

Run with Blender 5.x from this folder. The two titles in the MainMenu scene:
  blender -b --factory-startup --python make_title.py
      (current: Earth Orbiter Bold, "aXiom" -> AxiomTitle_EarthOrbiter)
  blender -b --factory-startup --python make_title.py -- --font Furore.otf --text AXIOM --spacing 1.35 \
      --name AxiomTitle_Furore
Each writes <name>.blend here and Assets/Art/Models/<name>.fbx for Unity. --out-dir puts the FBX in a
subfolder of Assets/Art/Models (for trials); --no-blend skips the .blend.

Units are metres. The text stands upright facing -Y (Blender's front view), centred on the origin, and is
scaled to WIDTH so the menu's title camera frames any font the same way.
"""
import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

HERE = Path(__file__).resolve().parent
MODELS = HERE.parents[1] / "Assets" / "Art" / "Models"

parser = argparse.ArgumentParser()
parser.add_argument("--font", default="../earth-orbiter/earthorbiterbold.ttf", help="font file, relative to this folder")
parser.add_argument("--text", default="aXiom")
parser.add_argument("--name", default="AxiomTitle_EarthOrbiter", help="output name (.blend and .fbx)")
parser.add_argument("--out-dir", default="", help="subfolder of Assets/Art/Models for the FBX")
parser.add_argument("--spacing", type=float, default=1.15, help="letter spacing (1 = the font's own)")
parser.add_argument("--depth", type=float, default=0.28, help="extrusion, front to back, at WIDTH")
parser.add_argument("--chamfer", type=float, default=0.025, help="one flat bevel on every edge, at WIDTH")
parser.add_argument("--lean", type=float, default=15.0, help="forward lean in degrees (menu buttons: 15)")
parser.add_argument("--no-blend", action="store_true", help="skip saving the .blend")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

WIDTH = 3.7         # final width of the title in metres
FONT = (HERE / args.font).resolve()
FBX = MODELS / args.out_dir / f"{args.name}.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"


def build(size, solid=True):
    """The text at a font size; solid=False is the flat outline, for measuring."""
    curve = bpy.data.curves.new(args.name, "FONT")
    curve.body = args.text
    curve.font = bpy.data.fonts.load(str(FONT))
    curve.size = size
    curve.space_character = args.spacing
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    if solid:
        curve.extrude = args.depth / 2 - args.chamfer    # the chamfer adds its depth back on both faces
        curve.bevel_depth = args.chamfer
        curve.bevel_resolution = 0                       # 0 = a single flat chamfer, not a fillet
        curve.offset = -args.chamfer                     # keep the outline at the font's size
    ob = bpy.data.objects.new(args.name + " text", curve)
    scene.collection.objects.link(ob)
    mesh = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    return mesh


# Measure the flat outline at size 1, then build at the size that gives WIDTH (depth and chamfer stay true).
probe = build(1.0, solid=False)
xs = [v.co.x for v in probe.vertices]
size = WIDTH / (max(xs) - min(xs))
bpy.data.meshes.remove(probe)
mesh = build(size)
mesh.name = args.name
stand_up = Matrix.Rotation(math.radians(90), 4, "X")         # text plane XY -> XZ, front face -> -Y
lean = Matrix.Identity(4)
lean[0][2] = math.tan(math.radians(args.lean))               # x += z * tan: the tops move right
mesh.transform(lean @ stand_up)
mesh.validate()
for poly in mesh.polygons:
    poly.use_smooth = False                                  # flat facets, like the rest of the game

ob = bpy.data.objects.new(args.name, mesh)
scene.collection.objects.link(ob)
dims = ob.dimensions
print(f"{args.name}: '{args.text}' in {FONT.name}, {dims.x:.3f} x {dims.z:.3f} m, {len(mesh.polygons)} faces")

if not args.no_blend:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE / f"{args.name}.blend"))
FBX.parent.mkdir(parents=True, exist_ok=True)
# Turned to face +Y for export only: with the default axes Unity then gets the text facing -Z (toward a camera
# looking along +Z) and no rotation on the imported root. (axis_forward="Z" instead puts a 180° turn on the root.)
mesh.transform(Matrix.Rotation(math.pi, 4, "Z"))
bpy.ops.export_scene.fbx(filepath=str(FBX), object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                         add_leaf_bones=False, use_mesh_modifiers=True)
print("Wrote", FBX)
