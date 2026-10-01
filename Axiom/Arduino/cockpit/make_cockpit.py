"""Axiom cockpit controller (v2), built in Blender from the design dimensions.

Run with Blender 5.x:
  blender -b --factory-startup --python make_cockpit.py                        (compact: build, save .blend + .glb)
  blender -b --factory-startup --python make_cockpit.py -- --roomy --render    (roomy preset, plus preview renders)

All sizes are millimetres. Panel coordinates: x from the left edge, y from the front edge measured
along the sloped top, z up from the top surface. Change the numbers below and rerun.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
RENDER = "--render" in sys.argv
PRESET = "roomy" if "--roomy" in sys.argv else "compact"

# ---------------- Design dimensions ----------------
# The footprint is set by two things: the throttle's front-to-back travel (depth) and the spacing
# between the throttle hand and the stick hand (width).
PRESETS = {
    "compact": dict(W=420.0, D=245.0, PALM=65.0, THR_X=72.0, TRAVEL=100.0, NEAR=40.0, SLED=40.0,
                    KNOB=(42.0, 50.0), STICK=(345.0, 130.0), CENTRE=222.0, BIG_Y=100.0, BTN_Y=160.0,
                    ARM=(140.0, 185.0), LCD_Y=200.0, ANN_Y=230.0, WORD_Y=215.0),
    "roomy": dict(W=500.0, D=280.0, PALM=80.0, THR_X=90.0, TRAVEL=120.0, NEAR=50.0, SLED=50.0,
                  KNOB=(46.0, 56.0), STICK=(405.0, 150.0), CENTRE=250.0, BIG_Y=125.0, BTN_Y=185.0,
                  ARM=(172.0, 205.0), LCD_Y=228.0, ANN_Y=262.0, WORD_Y=252.0),
}
globals().update(PRESETS[PRESET])
H_FRONT = 60.0                 # front height (top of the panel), back is higher by the slope
SLOPE = 8.0                    # degrees
T_TOP, T_WOOD = 3.0, 6.0       # acrylic top, Baltic birch body
FEET = 5.0

# Throttle: HC-SR04 at the front of the tunnel facing back; the plate travels NEAR..NEAR+TRAVEL
# from the transducers; the stem sits 25 behind the plate and runs in the slot.
SENSOR_FACE_Y = 44.0
STEM_MIN = SENSOR_FACE_Y + NEAR + 25.0
STEM_MAX = STEM_MIN + TRAVEL
SLOT_W = 8.0
SLOT_Y0, SLOT_Y1 = STEM_MIN - SLOT_W / 2, STEM_MAX + SLOT_W / 2
LEVER_Y = (STEM_MIN + STEM_MAX) / 2                        # knob shown at cruise
LED_X = THR_X + KNOB[0] / 2 + 15.0                         # LED bar just clear of the knob
LED_YS = [STEM_MIN + TRAVEL / 16 + TRAVEL / 8 * i for i in range(8)]   # LED 1 (min) at the front

BIG = (CENTRE, BIG_Y)          # EMP: 40 mm red mushroom, 22 mm hole (menus: confirm / launch)
MENU = (CENTRE, BTN_Y)         # 30 mm: pause in flight, back in menus
LCD = (CENTRE, LCD_Y)
SHIELD_YS, SHIELD_XS = LCD_Y, [CENTRE + 48.0 + 13.0 * i for i in range(3)]
ANNUNCIATORS = [("LINK", CENTRE - 50.0, (0.1, 1.0, 0.25), True), ("SLOW", CENTRE - 25.0, (0.15, 0.45, 1.0), True),
                ("FAST", CENTRE, (1.0, 0.55, 0.05), False), ("PROX", CENTRE + 25.0, (1.0, 0.08, 0.05), False),
                ("EMP", CENTRE + 50.0, (1.0, 0.75, 0.05), True)]   # EMP lit = still available this round
NAME = "axiom_cockpit" if PRESET == "compact" else "axiom_cockpit_roomy"

A = math.radians(SLOPE)
D_SLOPE = D / math.cos(A)      # panel length along the slope
MM = 0.001


# ---------------- Scene ----------------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "MILLIMETERS"

COLL = {}
for name in ("Cockpit", "Internals", "Studio"):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    COLL[name] = c


# ---------------- Materials ----------------
def material(name, color, rough=0.5, metal=0.0, emit=None, strength=0.0, alpha=1.0, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if coat:
        p.inputs["Coat Weight"].default_value = coat
    if emit:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = strength
    if alpha < 1:
        p.inputs["Alpha"].default_value = alpha
    return m


def plywood():
    m = material("Baltic birch", (0.72, 0.56, 0.38), rough=0.65)
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.inputs["Scale"].default_value = 70.0
    wave.inputs["Distortion"].default_value = 1.5
    wave.inputs["Detail"].default_value = 3.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.66, 0.5, 0.33, 1)
    ramp.color_ramp.elements[1].color = (0.76, 0.6, 0.42, 1)
    nt.links.new(tc.outputs["Object"], wave.inputs["Vector"])
    nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    return m


def leatherette():
    m = material("Palm rest", (0.035, 0.035, 0.04), rough=0.85)
    nt = m.node_tree
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 900.0
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], nt.nodes["Principled BSDF"].inputs["Normal"])
    return m


M = {
    "acrylic": material("Black acrylic", (0.006, 0.006, 0.008), rough=0.16),
    "ply": plywood(),
    "palm": leatherette(),
    "rubber": material("Rubber", (0.02, 0.02, 0.02), rough=0.75),
    "metal": material("Metal", (0.72, 0.72, 0.74), rough=0.3, metal=1.0),
    "dark_metal": material("Dark metal", (0.12, 0.12, 0.13), rough=0.35, metal=1.0),
    "red": material("Red plastic", (0.75, 0.02, 0.02), rough=0.22, coat=0.4),
    "yellow": material("Yellow legend plate", (0.95, 0.72, 0.02), rough=0.35),
    "ink": material("Black print", (0.01, 0.01, 0.01), rough=0.5),
    "red_cover": material("Red cover", (0.85, 0.03, 0.02), rough=0.2, coat=0.5),
    "white": material("White plastic", (0.88, 0.88, 0.86), rough=0.3),
    "black_plastic": material("Black plastic", (0.03, 0.03, 0.03), rough=0.35),
    "label": material("Label print", (0.82, 0.82, 0.8), rough=0.5),
    "led_off": material("LED off", (0.18, 0.18, 0.2), rough=0.1),
    "led_white": material("LED white", (1, 1, 1), emit=(1, 0.95, 0.9), strength=12),
    "led_band": material("LED band", (0.2, 0.6, 1), emit=(0.15, 0.55, 1.0), strength=14),
    "led_blue": material("LED shield", (0.1, 0.3, 1), emit=(0.1, 0.35, 1.0), strength=14),
    "lcd_glass": material("LCD backlight", (0.02, 0.05, 0.3), emit=(0.05, 0.18, 0.9), strength=2.5),
    "lcd_text": material("LCD text", (1, 1, 1), emit=(0.9, 0.95, 1.0), strength=6),
    "ann_back": material("Annunciator back", (0.015, 0.015, 0.015), rough=0.4),
    "ann_off": material("Annunciator legend off", (0.1, 0.1, 0.1), rough=0.5),
    "foam": material("Foam board", (0.9, 0.9, 0.88), rough=0.9),
    "pcb_blue": material("PCB blue", (0.02, 0.18, 0.45), rough=0.4),
    "pcb_green": material("PCB green", (0.03, 0.3, 0.12), rough=0.4),
    "terminal": material("Screw terminals", (0.05, 0.45, 0.2), rough=0.4),
    "sled": material("Sled", (0.83, 0.75, 0.6), rough=0.8),
    "desk": material("Desk", (0.08, 0.075, 0.07), rough=0.55),
}


# ---------------- Mesh helpers (inputs in mm) ----------------
def link(ob, coll, parent):
    COLL[coll].objects.link(ob)
    if parent is not None:
        ob.parent = parent


def mesh_obj(name, bm, mat, loc=(0, 0, 0), rot=(0, 0, 0), parent=None, coll="Cockpit", smooth=False):
    bmesh.ops.scale(bm, vec=(MM, MM, MM), verts=bm.verts)
    if smooth:
        for f in bm.faces:
            f.smooth = abs(f.normal.z) < 0.99
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    if mat:
        me.materials.append(mat)
    ob.location = Vector(loc) * MM
    ob.rotation_euler = rot
    link(ob, coll, parent)
    return ob


def rounded(ob, radius, segments=5):
    bev = ob.modifiers.new("Bevel", "BEVEL")
    bev.width = radius * MM
    bev.segments = segments
    bev.limit_method = "ANGLE"
    ob.modifiers.new("Normals", "WEIGHTED_NORMAL").keep_sharp = True
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def box(name, cx, cy, cz, sx, sy, sz, mat, parent=None, coll="Cockpit", bevel=0.0, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    ob = mesh_obj(name, bm, mat, (cx, cy, cz), rot, parent, coll)
    return rounded(ob, bevel) if bevel else ob


def cyl(name, cx, cy, z0, r, h, mat, parent=None, coll="Cockpit", r2=None, segs=48, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                          radius2=r if r2 is None else r2, depth=h,
                          matrix=Matrix.Translation((0, 0, h / 2)))
    return mesh_obj(name, bm, mat, (cx, cy, z0), rot, parent, coll, smooth=True)


def dome(name, cx, cy, z0, r, h, mat, parent=None, coll="Cockpit"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context="VERTS")
    bmesh.ops.scale(bm, vec=(r, r, h), verts=bm.verts)
    return mesh_obj(name, bm, mat, (cx, cy, z0), (0, 0, 0), parent, coll, smooth=True)


def prism(name, x0, x1, profile, mat, coll="Cockpit"):
    """Extrudes a (y, z) polygon from x0 to x1 (used for the sloped side walls)."""
    bm = bmesh.new()
    left = [bm.verts.new((x0, y, z)) for y, z in profile]
    right = [bm.verts.new((x1, y, z)) for y, z in profile]
    bm.faces.new(list(reversed(left)))
    bm.faces.new(right)
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((left[i], left[j], right[j], right[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_obj(name, bm, mat, coll=coll)


FONTS = {}


def font(file):
    if file not in FONTS:
        path = Path("C:/Windows/Fonts") / file
        FONTS[file] = bpy.data.fonts.load(str(path)) if path.exists() else None
    return FONTS[file]


TEXTS = []


def text(name, body, cx, cy, size, mat, parent, z=0.05, rot_z=0.0, face="segoeuib.ttf", align="CENTER",
         spacing=1.0, coll="Cockpit"):
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = body
    f = font(face)
    if f:
        cu.font = f
    cu.size = size * MM
    cu.align_x = align
    cu.align_y = "CENTER"
    cu.extrude = 0.04 * MM
    cu.space_character = spacing
    cu.materials.append(mat)
    ob = bpy.data.objects.new(name, cu)
    ob.location = Vector((cx, cy, z)) * MM
    ob.rotation_euler = (0, 0, rot_z)
    link(ob, coll, parent)
    TEXTS.append(ob)
    return ob


# ---------------- Body (world coordinates) ----------------
def under_panel_z(y):
    """Height of the panel's underside above the bottom of the box, at plan depth y."""
    return FEET + H_FRONT - T_TOP / math.cos(A) + y * math.tan(A)


base = FEET
box("Bottom", W / 2, D / 2, base + T_WOOD / 2, W, D, T_WOOD, M["ply"])
side_profile = [(0, base + T_WOOD), (D, base + T_WOOD), (D, under_panel_z(D)), (0, under_panel_z(0))]
prism("Side left", 0, T_WOOD, side_profile, M["ply"])
prism("Side right", W - T_WOOD, W, side_profile, M["ply"])
prism("Front wall", T_WOOD, W - T_WOOD,
      [(0, base + T_WOOD), (T_WOOD, base + T_WOOD), (T_WOOD, under_panel_z(T_WOOD)), (0, under_panel_z(0))], M["ply"])
prism("Back wall", T_WOOD, W - T_WOOD,
      [(D - T_WOOD, base + T_WOOD), (D, base + T_WOOD), (D, under_panel_z(D)), (D - T_WOOD, under_panel_z(D - T_WOOD))],
      M["ply"])
for i, (fx, fy) in enumerate(((25, 25), (W - 25, 25), (25, D - 25), (W - 25, D - 25))):
    cyl(f"Foot {i + 1}", fx, fy, 0, 10, FEET, M["rubber"])
# Panel-mount USB-B on the back wall
box("USB-B socket", W / 2, D + 1.5, base + 40, 26, 3, 20, M["dark_metal"])
box("USB-B port", W / 2, D + 3.1, base + 40, 12, 0.4, 11, M["black_plastic"])

# ---------------- Top panel frame: everything mounted on the slope ----------------
frame = bpy.data.objects.new("Panel frame", None)
frame.location = Vector((0, 0, FEET + H_FRONT)) * MM
frame.rotation_euler = (A, 0, 0)
COLL["Cockpit"].objects.link(frame)

# Top plate with every opening cut out
cut = bmesh.new()


def cut_cyl(cx, cy, r):
    bmesh.ops.create_cone(cut, cap_ends=True, segments=64, radius1=r, radius2=r, depth=20,
                          matrix=Matrix.Translation((cx, cy, 0)))


def cut_box(cx, cy, sx, sy):
    m = Matrix.Translation((cx, cy, 0)) @ Matrix.Diagonal((sx, sy, 20, 1))
    bmesh.ops.create_cube(cut, size=1.0, matrix=m)


cut_cyl(*STICK, 20.0)
for dx in (-16.25, 16.25):
    for dy in (-16.25, 16.25):
        cut_cyl(STICK[0] + dx, STICK[1] + dy, 1.4)
cut_box(THR_X, (SLOT_Y0 + SLOT_Y1) / 2, SLOT_W, SLOT_Y1 - SLOT_Y0 - SLOT_W)
cut_cyl(THR_X, SLOT_Y0 + SLOT_W / 2, SLOT_W / 2)
cut_cyl(THR_X, SLOT_Y1 - SLOT_W / 2, SLOT_W / 2)
for y in LED_YS:
    cut_cyl(LED_X, y, 2.55)
for x in SHIELD_XS:
    cut_cyl(x, SHIELD_YS, 2.55)
cut_cyl(*BIG, 11.0)
cut_cyl(*MENU, 14.0)    # Happ-style 30 mm button: 28 mm hole
cut_cyl(*ARM, 6.0)
cut_box(LCD[0], LCD[1], 72.0, 25.0)
for dx in (-37.5, 37.5):
    for dy in (-15.5, 15.5):
        cut_cyl(LCD[0] + dx, LCD[1] + dy, 1.6)
for _, x, _, _ in ANNUNCIATORS:
    cut_box(x, ANN_Y, 22.0, 10.0)
for cx, cy in ((10, 10), (W - 10, 10), (10, D_SLOPE - 10), (W - 10, D_SLOPE - 10), (W / 2, 10), (W / 2, D_SLOPE - 10)):
    cut_cyl(cx, cy, 1.6)
cutter = mesh_obj("Cutter", cut, None, parent=frame)

top = box("Top panel (3 mm acrylic)", W / 2, D_SLOPE / 2, -T_TOP / 2, W, D_SLOPE, T_TOP, M["acrylic"], frame)
boolean = top.modifiers.new("Openings", "BOOLEAN")
boolean.operation = "DIFFERENCE"
boolean.solver = "EXACT"
boolean.object = cutter
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
baked = bpy.data.meshes.new_from_object(top.evaluated_get(dg))
top.modifiers.clear()
top.data = baked
bpy.data.objects.remove(cutter)

# Palm rest
rounded(box("Palm rest", W / 2, PALM / 2 + 1, 3.5, W - 4, PALM - 2, 7, M["palm"], frame), 3.4, 6)

# ---------------- Throttle ----------------
rounded(box("Throttle knob", THR_X, LEVER_Y, 27, KNOB[0], KNOB[1], 30, M["black_plastic"], frame), 9, 6)
box("Throttle stem", THR_X, LEVER_Y, -12.5, 9.6, 9.6, 53, M["black_plastic"], frame)
for i, y in enumerate(LED_YS):
    # Example state: slow-zone band (LEDs 1-2) and the lever's own LED at cruise.
    lit = M["led_band"] if i < 2 else M["led_white"] if i == 4 else M["led_off"]
    cyl(f"Throttle LED {i + 1}", LED_X, y, -3.0, 2.5, 5.6, lit, frame)
    dome(f"Throttle LED {i + 1} dome", LED_X, y, 2.6, 2.5, 2.5, lit, frame)
text("Label THROTTLE", "THROTTLE", LED_X + 11, (LED_YS[0] + LED_YS[-1]) / 2, 5, M["label"], frame, rot_z=math.pi / 2)
text("Label MAX", "MAX", LED_X, SLOT_Y1 + 9, 3.6, M["label"], frame)
text("Label MIN", "MIN", LED_X, SLOT_Y0 - 1, 3.6, M["label"], frame)

# ---------------- Stick: JH-D400X-R4 with button ----------------
sx, sy = STICK
cyl("Stick pressure ring", sx, sy, 0, 26.75, 2.0, M["dark_metal"], frame)
for i, (dx, dy) in enumerate(((-16.25, -16.25), (16.25, -16.25), (-16.25, 16.25), (16.25, 16.25))):
    cyl(f"Stick screw {i + 1}", sx + dx, sy + dy, 2.0, 2.2, 0.8, M["metal"], frame, segs=16)
cyl("Stick boot", sx, sy, 2.0, 19.0, 11.0, M["rubber"], frame, r2=8.5)
cyl("Stick shaft", sx, sy, 12.0, 7.0, 30.0, M["black_plastic"], frame, r2=8.0)
for i in range(4):
    cyl(f"Stick grip ring {i + 1}", sx, sy, 20.0 + i * 6.0, 8.6, 2.2, M["rubber"], frame)
cyl("Stick head", sx, sy, 42.0, 8.5, 22.0, M["black_plastic"], frame, r2=10.0)
cyl("Stick FIRE button", sx, sy, 64.0, 6.0, 2.5, M["red"], frame)
dome("Stick FIRE top", sx, sy, 66.5, 6.0, 1.5, M["red"], frame)

# ---------------- Buttons ----------------
# EMP: emergency-stop style, a 40 mm red mushroom on a yellow legend plate (momentary, not latching)
cyl("EMP legend plate", BIG[0], BIG[1], 0, 30.0, 1.2, M["yellow"], frame)
text("EMP legend", "EMP", BIG[0], BIG[1] + 25.0, 4.6, M["ink"], frame, z=1.25)
cyl("EMP bezel", BIG[0], BIG[1], 1.2, 13.5, 6.0, M["dark_metal"], frame)
cyl("EMP stem", BIG[0], BIG[1], 7.2, 11.0, 8.0, M["red"], frame)
cyl("EMP mushroom", BIG[0], BIG[1], 15.2, 20.0, 5.0, M["red"], frame, r2=20.0)
dome("EMP mushroom top", BIG[0], BIG[1], 20.2, 20.0, 7.0, M["red"], frame)
for name, (bx, by) in (("MENU", MENU),):
    cyl(f"{name} flange", bx, by, 0, 16.65, 4.7, M["black_plastic"], frame)
    cyl(f"{name} plunger", bx, by, 4.7, 11.3, 4.2, M["white"], frame)
    dome(f"{name} plunger top", bx, by, 8.9, 11.3, 0.8, M["white"], frame)
    text(f"Label {name}", name, bx, by + 21.5, 3.6, M["label"], frame)


# ---------------- MASTER ARM (flip-cover toggle) ----------------
ax, ay = ARM
box("ARM plate", ax, ay, 1.0, 28, 40, 2, M["dark_metal"], frame)
rounded(box("ARM cover", ax, ay - 1, 9.0, 15, 34, 14, M["red_cover"], frame), 3, 4)
cyl("ARM hinge", ax - 8.5, ay + 16, 3.5, 2.2, 17, M["metal"], frame, rot=(0, math.pi / 2, 0))
text("Label MASTER ARM", "MASTER ARM", ax, ay - 26, 3.6, M["label"], frame)

# ---------------- LCD, warning lights, shield ----------------
lx, ly = LCD
box("LCD glass", lx, ly, -1.6, 71.5, 24.5, 0.6, M["lcd_glass"], frame)
face = "consola.ttf"
text("LCD row 1", "* AXIOM  STANDBY", lx, ly + 5.2, 6.2, M["lcd_text"], frame, z=-1.2, face=face)
text("LCD row 2", "  CRUISE   100%", lx, ly - 5.6, 6.2, M["lcd_text"], frame, z=-1.2, face=face)
for label, x, color, lit in ANNUNCIATORS:
    box(f"{label} window", x, ANN_Y, -1.6, 21.5, 9.5, 0.6, M["ann_back"], frame)
    mat = material(f"{label} legend", color, emit=color, strength=10) if lit else M["ann_off"]
    text(f"{label} legend", label, x, ANN_Y, 5.2, mat, frame, z=-1.2)
for label, x, color, lit in ANNUNCIATORS:
    # Behind each window: a diffuser with the printed legend, a black light box so neighbours
    # don't bleed into each other, and a NeoPixel at the bottom shining up.
    box(f"{label} diffuser", x, ANN_Y, -3.4, 24, 12, 0.8, M["white"], frame, "Internals")
    for dx, dy, sx_, sy_ in ((-12.5, 0, 1.5, 14), (12.5, 0, 1.5, 14), (0, -6.25, 26.5, 1.5), (0, 6.25, 26.5, 1.5)):
        box(f"{label} light box", x + dx, ANN_Y + dy, -3.8 - 7, sx_, sy_, 14, M["black_plastic"], frame, "Internals")
    pixel = material(f"{label} pixel", color, emit=color, strength=12) if lit else M["led_off"]
    cyl(f"{label} NeoPixel", x, ANN_Y, -3.8 - 14, 2.5, 5.6, pixel, frame, "Internals")
for i, x in enumerate(SHIELD_XS):
    cyl(f"Shield LED {i + 1}", x, SHIELD_YS, -3.0, 2.5, 5.6, M["led_blue"], frame)
    dome(f"Shield LED {i + 1} dome", x, SHIELD_YS, 2.6, 2.5, 2.5, M["led_blue"], frame)
text("Label SHIELD", "SHIELD", SHIELD_XS[1], SHIELD_YS + 11, 3.6, M["label"], frame)
text("Wordmark", "AXIOM", STICK[0], WORD_Y, 13, M["label"], frame, face="segoeuil.ttf", spacing=1.35)

# ---------------- Internals (hang from the panel unless noted) ----------------
IN = "Internals"
under = -T_TOP
tun_y0, tun_y1, tun_depth = 30.0, STEM_MAX - 25.0 + SLED + 6.0, 40.0
for side in (-1, 1):
    box(f"Tunnel wall {'L' if side < 0 else 'R'}", THR_X + side * 27.5, (tun_y0 + tun_y1) / 2,
        under - tun_depth / 2, 5, tun_y1 - tun_y0, tun_depth, M["foam"], frame, IN)
box("Tunnel floor", THR_X, (tun_y0 + tun_y1) / 2, under - tun_depth - 2.5, 60, tun_y1 - tun_y0, 5, M["foam"], frame, IN)
box("Sensor wall", THR_X, SENSOR_FACE_Y - 9.5, under - tun_depth / 2, 50, 5, tun_depth, M["foam"], frame, IN)
box("HC-SR04 board", THR_X, SENSOR_FACE_Y - 14.0, under - tun_depth / 2, 45, 1.6, 20, M["pcb_blue"], frame, IN)
for dx in (-13.0, 13.0):
    cyl(f"HC-SR04 can {'L' if dx < 0 else 'R'}", THR_X + dx, SENSOR_FACE_Y - 12.0, under - tun_depth / 2, 8.0, 12.0,
        M["metal"], frame, IN, rot=(-math.pi / 2, 0, 0))
plate_y = LEVER_Y - 25.0
box("Reflector plate", THR_X, plate_y, under - tun_depth / 2, 48, 3, 36, M["white"], frame, IN)
box("Sled base", THR_X, plate_y + SLED / 2, under - tun_depth + 2, 48, SLED, 4, M["sled"], frame, IN)
box("Rumble motor (in knob)", THR_X, LEVER_Y, 22, 10, 10, 3, M["dark_metal"], frame, IN)
box("Stick body", sx, sy, -3 - 12.75, 40.4, 40.4, 25.5, M["black_plastic"], frame, IN)
box("EMP contact block", BIG[0], BIG[1], -3 - 27, 30, 40, 48, M["black_plastic"], frame, IN)
for name, (bx, by) in (("MENU", MENU),):
    cyl(f"{name} body", bx, by, -3 - 30.3, 14, 30.3, M["black_plastic"], frame, IN)
cyl("ARM body", ax, ay, -3 - 35, 6, 35, M["dark_metal"], frame, IN)
box("LCD module", lx, ly, -3 - 7, 80, 36, 8, M["pcb_green"], frame, IN)
# Uno + screw-terminal shield on the floor near the back (world coordinates)
ux, uy = W / 2, D - T_WOOD - 40
box("Arduino Uno", ux, uy, FEET + T_WOOD + 6, 53.4, 68.6, 1.6, M["pcb_blue"], coll=IN)
box("Screw-terminal shield", ux, uy, FEET + T_WOOD + 20, 53.4, 68.6, 1.6, M["pcb_green"], coll=IN)
for side in (-1, 1):
    box(f"Terminals {'L' if side < 0 else 'R'}", ux + side * 23, uy, FEET + T_WOOD + 25, 7, 60, 9, M["terminal"], coll=IN)
box("Haptic driver", ux - 60, uy, FEET + T_WOOD + 3, 18, 25, 3, M["pcb_blue"], coll=IN)

# ---------------- Text to meshes (so exports keep them) ----------------
dg = bpy.context.evaluated_depsgraph_get()
for ob in TEXTS:
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    new = bpy.data.objects.new(ob.name, me)
    for mat in ob.data.materials:
        if mat and mat.name not in [m.name for m in me.materials if m]:
            me.materials.append(mat)
    coll = ob.users_collection[0]
    coll.objects.link(new)
    new.parent = ob.parent
    new.matrix_parent_inverse = ob.matrix_parent_inverse.copy()
    new.location = ob.location.copy()
    new.rotation_euler = ob.rotation_euler.copy()
    bpy.data.objects.remove(ob)

# ---------------- Studio: desk, lights, cameras ----------------
bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1400)
mesh_obj("Desk", bm, M["desk"], (W / 2, D / 2 - 150, 0), coll="Studio")

world = bpy.data.worlds.new("Studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.022, 0.026, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
scene.world = world


def area_light(name, loc, target, size, power, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, "AREA")
    ld.size = size * MM
    ld.energy = power
    ld.color = color
    ob = bpy.data.objects.new(name, ld)
    ob.location = Vector(loc) * MM
    ob.rotation_euler = (Vector(target) * MM - ob.location).to_track_quat("-Z", "Y").to_euler()
    COLL["Studio"].objects.link(ob)


centre = (W / 2, D / 2, 70)
area_light("Key", (-350, -500, 700), centre, 700, 28, (1.0, 0.96, 0.9))
area_light("Fill", (950, -300, 400), centre, 600, 9, (0.85, 0.9, 1.0))
area_light("Rim", (250, 900, 500), centre, 900, 22)


def camera(name, loc, target, lens=50, ortho=None):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_start = 0.01
    cd.clip_end = 50
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho * MM
    ob = bpy.data.objects.new(name, cd)
    ob.location = Vector(loc) * MM
    ob.rotation_euler = (Vector(target) * MM - ob.location).to_track_quat("-Z", "Y").to_euler()
    COLL["Studio"].objects.link(ob)
    return ob


cams = {
    "hero": camera("Cam hero", (-330, -520, 470), (240, 150, 70), lens=45),
    "player": camera("Cam player view", (W / 2, -430, 420), (W / 2, 150, 80), lens=32),
    "top": camera("Cam top", (250, 140, 1200), (250, 140.01, 0), ortho=560),   # same scale for both presets
    "side": camera("Cam side", (1400, 140, 90), (0, 140, 90), ortho=330),
    "xray": camera("Cam xray", (-250, -380, 520), (250, 150, 40), lens=40),
}
scene.camera = cams["hero"]

scene.render.engine = "CYCLES"
scene.cycles.samples = 96
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
scene.render.image_settings.file_format = "PNG"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            gpus = [d for d in prefs.devices if d.type == kind]
            if gpus:
                for d in prefs.devices:
                    d.use = d.type == kind
                scene.cycles.device = "GPU"
                print("Rendering on", kind)
                break
        except TypeError:
            continue
except Exception as e:  # CPU fallback
    print("GPU setup skipped:", e)

# ---------------- Save ----------------
bpy.context.preferences.filepaths.save_version = 0   # no .blend1 backups next to the file
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / f"{NAME}.blend"))
for ob in bpy.data.objects:
    ob.select_set(ob.users_collection[0].name != "Studio")
bpy.ops.export_scene.gltf(filepath=str(HERE / f"{NAME}.glb"), export_format="GLB",
                          use_selection=True, export_apply=True)
print("Saved", HERE / f"{NAME}.blend", f"({W:g} x {D:g} mm)")

if RENDER:
    out = HERE / "renders" / PRESET
    out.mkdir(parents=True, exist_ok=True)
    top_panel = bpy.data.objects["Top panel (3 mm acrylic)"]
    palm = bpy.data.objects["Palm rest"]
    for key, cam in cams.items():
        scene.camera = cam
        xray = key == "xray"
        top_panel.hide_render = xray
        palm.hide_render = xray
        for ob in COLL["Internals"].objects:
            ob.hide_render = not xray
        scene.render.filepath = str(out / f"{key}.png")
        bpy.ops.render.render(write_still=True)
        print("Rendered", key)
