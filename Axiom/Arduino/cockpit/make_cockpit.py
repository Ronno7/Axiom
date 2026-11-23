"""Axiom cockpit controller (v3), built in Blender from the design dimensions.

Run with Blender 5.x:
  blender -b --factory-startup --python make_cockpit.py               (build, save .blend + .glb, cut files)
  blender -b --factory-startup --python make_cockpit.py -- --render    (plus preview renders)
  blender -b --factory-startup --python make_cockpit.py -- --cut       (only the cut files + template)

All sizes are millimetres. Panel coordinates: x from the left edge, y from the front edge measured
along the sloped top, z up from the top surface. Change the numbers below and rerun.

Every flat part (panel, body, posts, knob, light boxes) is one outline, used both for its laser cut file in
cut/ (DXF, inches, one part per file, quantities in cut/parts.csv) and for the model.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
RENDER = "--render" in sys.argv

# ---------------- Design dimensions ----------------
# The footprint is set by two things: the throttle's front-to-back travel (depth) and the spacing
# between the throttle hand and the stick hand (width).
W, D, PALM, THR_X, TRAVEL = 420.0, 245.0, 65.0, 72.0, 100.0
KNOB, STICK, ARM = (30.0, 26.0), (345.0, 130.0), (140.0, 185.0)
CENTRE, BIG_Y, BTN_Y, LCD_Y, ANN_Y, WORD_Y = 222.0, 103.0, 160.0, 200.0, 230.0, 215.0
H_FRONT = 60.0                 # front height (top of the panel), back is higher by the slope
SLOPE = 8.0                    # degrees
T_TOP, T_WOOD = 3.0, 6.35      # 3 mm black acrylic top; SendCutSend's .250" Baltic birch body
FEET = 5.0

# Throttle: a Bourns PTL01-15W0-103B1 fader (100 mm travel) screwed under the panel, its lever up through
# the slot. From its datasheet: M3 tapped holes 120 mm apart, body 128 x 9.3 x 7, lever 2.05 x 6.0 and
# 15 mm above the mounting face. Cruise is the middle of the travel; push away is faster.
STEM_MIN = 109.0                                   # lever centre at minimum, along the slope
STEM_MAX = STEM_MIN + TRAVEL
LEVER_Y = (STEM_MIN + STEM_MAX) / 2                # shown at cruise
FADER_L, FADER_W, FADER_H, FADER_HOLES = 128.0, 9.3, 7.0, 120.0
LEVER_T, LEVER_W, LEVER_L = 2.05, 6.0, 15.0        # across, along the travel, above the mounting face
SLOT_W = 3.5
SLOT_Y0, SLOT_Y1 = STEM_MIN - LEVER_W / 2 - 1.0, STEM_MAX + LEVER_W / 2 + 1.0
KNOB_LAYERS, KNOB_LIFT, KNOB_R = 5, 1.0, 5.0       # 3 mm acrylic layers glued on the lever; 1 mm felt under it
KNOB_SLOT = (2.5, 6.5)                             # the lever's hole, with room for glue
LED_X = THR_X + 36.0                               # LED bar beside the knob
LED_YS = [STEM_MIN + TRAVEL / 16 + TRAVEL / 8 * i for i in range(8)]   # LED 1 (min) at the front
LED_HOLE_R = 2.55

BIG = (CENTRE, BIG_Y)          # EMP: 60 mm red dome arcade button, 24 mm hole (menus: confirm / launch)
MENU = (CENTRE, BTN_Y)         # 30 mm: pause in flight, back in menus
LCD = (CENTRE, LCD_Y)
SHIELD_YS, SHIELD_XS = LCD_Y, [CENTRE + 48.0 + 13.0 * i for i in range(3)]
ANNUNCIATORS = [("LINK", CENTRE - 50.0, (0.1, 1.0, 0.25), True), ("SLOW", CENTRE - 25.0, (0.15, 0.45, 1.0), True),
                ("FAST", CENTRE, (1.0, 0.55, 0.05), False), ("PROX", CENTRE + 25.0, (1.0, 0.08, 0.05), False),
                ("EMP", CENTRE + 50.0, (1.0, 0.75, 0.05), True)]   # EMP lit = still available this round
NAME = "axiom_cockpit"

A = math.radians(SLOPE)
D_SLOPE = D / math.cos(A)      # panel length along the slope
MM = 0.001


def under_h(y):
    """Height of the panel's underside above the bottom of the box (feet excluded), at plan depth y."""
    return H_FRONT - T_TOP / math.cos(A) + y * math.tan(A)


# ---------------- Body joints ----------------
# Sides run the full depth; the front and back walls sit between them. Every wall stands on the bottom with
# tabs down into notches in its edge, and the front and back walls' end tabs fill notches in the sides.
# Thickness errors only change how flush a joint is, never whether it fits.
TW = T_WOOD
CL = 0.15                      # extra length in every notch and mortise, for glue
SLACK = 0.5                    # extra width across the board in mortises, since plywood runs thick
H_FRONT_WALL = under_h(0.0)    # touches the panel along its outer top edge
H_BACK_WALL = under_h(D - TW)  # touches the panel along its inner top edge


def tabs(a, b, width, pitch):
    """Evenly spaced tab spans of one width, centred in a..b."""
    n = max(1, int((b - a - width) / pitch) + 1)
    start = (a + b) / 2 - ((n - 1) * pitch + width) / 2
    return [(start + i * pitch, start + i * pitch + width) for i in range(n)]


SIDE_TABS = tabs(TW + 40, D - TW - 40, 20, 50)              # clear of the corner posts' mortises
WALL_TABS = tabs(3 * TW + 10, W - 3 * TW - 10, 20, 60)
FRONT_END_TABS = tabs(TW + 6, H_FRONT_WALL - 6, 12, 22)
BACK_END_TABS = tabs(TW + 6, H_BACK_WALL - 6, 12, 22)

# Posts carry the panel screws: two layers each, parallel to the sides so their tops follow the slope exactly,
# standing in a mortise in the bottom. Each screw is centred in one layer.
POST_LEN, POST_TAB = 30.0, (8.0, 22.0)
POSTS = [  # name, x of the left layer, y of the front end
    ("front left", TW, TW), ("front right", W - 3 * TW, TW), ("front middle", W / 2 - TW, TW),
    ("back left", TW, D - TW - POST_LEN), ("back right", W - 3 * TW, D - TW - POST_LEN)]
SCREW_IN, SCREW_UP = 1.5 * TW, 15.0
PANEL_SCREWS = [(SCREW_IN, SCREW_UP), (W - SCREW_IN, SCREW_UP), (SCREW_IN, D_SLOPE - SCREW_UP),
                (W - SCREW_IN, D_SLOPE - SCREW_UP), (W / 2 - TW / 2, SCREW_UP)]

# Uno + Gravity IO shield on M3 standoffs near the back wall, USB jack to the right (towards the panel-mount
# socket). Its four holes, rotated 180 degrees from Arduino's drawing.
UNO = (W / 2, D - TW - 6 - 53.34 / 2)
UNO_HOLES = [(54.61, 50.80), (53.34, 2.54), (2.54, 45.72), (2.54, 17.78)]

# Warning lights: a stack of 3 mm black acrylic plates under the five windows, each cut with the same five
# windows, so every light has its own tunnel; a floor plate holds the LED modules. A printed paper legend sits
# between the panel and the stack. In front of the windows 4.5 mm of acrylic (SendCutSend's 1.5x thickness for
# acrylic), behind them 3.25 mm, all the sloped back wall leaves: the stack clears it by about 1 mm.
LB_X0, LB_LEN, LB_LAYERS, PAPER = CENTRE - 67.0, 134.0, 3, 0.2
LB_FRONT, LB_BACK, LB_FLOOR_BACK = 9.5, 8.25, 7.5     # from the windows' centre line to each edge
LB_WINDOW = (22.0, 10.0)

# Not yet checked against the real parts (measure before ordering): the USB socket's cutout and screws, so the
# back wall has no USB opening yet; the LED modules (the panel's 5.1 mm holes); the openings in PANEL_TODO.
USB_SOCKET = None              # (x, z, width, height, screw spacing, screw hole diameter) once measured
PANEL_TODO = "joystick, EMP, MENU, MASTER ARM, LCD"


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
    "felt": material("Felt", (0.05, 0.05, 0.055), rough=1.0),
    "metal": material("Metal", (0.72, 0.72, 0.74), rough=0.3, metal=1.0),
    "dark_metal": material("Dark metal", (0.12, 0.12, 0.13), rough=0.35, metal=1.0),
    "red": material("Red plastic", (0.75, 0.02, 0.02), rough=0.22, coat=0.4),
    "yellow": material("Yellow legend plate", (0.95, 0.72, 0.02), rough=0.35),
    "ink": material("Black print", (0.01, 0.01, 0.01), rough=0.5),
    "red_cover": material("Red cover", (0.85, 0.03, 0.02), rough=0.2, coat=0.5),
    "white": material("White plastic", (0.88, 0.88, 0.86), rough=0.3),
    "lever": material("Fader lever", (0.9, 0.92, 0.95), rough=0.25, emit=(0.8, 0.85, 1.0), strength=0.6),
    "paper": material("Legend paper", (0.9, 0.9, 0.88), rough=0.9),
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
    "pcb_blue": material("PCB blue", (0.02, 0.18, 0.45), rough=0.4),
    "pcb_green": material("PCB green", (0.03, 0.3, 0.12), rough=0.4),
    "terminal": material("Screw terminals", (0.05, 0.45, 0.2), rough=0.4),
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


# ---------------- Flat parts: outlines (mm) ----------------
# A loop is a closed list of (u, v, bulge) points; bulge is the DXF arc factor for the edge to the next point
# (0 for a straight edge, 1 for a half circle, positive counter-clockwise). ("circle", cx, cy, r) is a hole.
def rect(u0, v0, u1, v1):
    return [(u0, v0, 0), (u1, v0, 0), (u1, v1, 0), (u0, v1, 0)]


def rounded_rect(u0, v0, u1, v1, r):
    b = math.tan(math.pi / 8)
    return [(u0 + r, v0, 0), (u1 - r, v0, b), (u1, v0 + r, 0), (u1, v1 - r, b), (u1 - r, v1, 0), (u0 + r, v1, b),
            (u0, v1 - r, 0), (u0, v0 + r, b)]


def slot_loop(cu, v0, v1, w):
    r = w / 2
    return [(cu + r, v0 + r, 0), (cu + r, v1 - r, 1), (cu - r, v1 - r, 0), (cu - r, v0 + r, 1)]


def edge(p0, p1, spans, off):
    """The points of a straight, axis-aligned edge from p0 up to (not including) p1, stepping out by the vector
    `off` over each span (a, b) along the edge. A notch steps inwards, a tab outwards."""
    (u0, v0), (u1, v1) = p0, p1
    along_u = v0 == v1
    forward = (u1 > u0) if along_u else (v1 > v0)
    pts = [(u0, v0)]
    for a, b in sorted(spans, reverse=not forward):
        s, e = (a, b) if forward else (b, a)
        q0, q1 = ((s, v0), (e, v0)) if along_u else ((u0, s), (u0, e))
        pts += [q0, (q0[0] + off[0], q0[1] + off[1]), (q1[0] + off[0], q1[1] + off[1]), q1]
    return pts


def outline(*runs):
    out = []
    for p in (p for run in runs for p in run):
        if not out or abs(p[0] - out[-1][0]) > 1e-9 or abs(p[1] - out[-1][1]) > 1e-9:
            out.append(p)
    if abs(out[0][0] - out[-1][0]) < 1e-9 and abs(out[0][1] - out[-1][1]) < 1e-9:
        out.pop()
    return [(u, v, 0) for u, v in out]


def widen(spans, by=CL):
    return [(a - by / 2, b + by / 2) for a, b in spans]


def wall_outline(height, end_tabs):
    """Front or back wall in (x, z): tabs down into the bottom and out into both sides."""
    return outline(edge((TW, TW), (W - TW, TW), WALL_TABS, (0, -TW)),
                   edge((W - TW, TW), (W - TW, height), end_tabs, (TW, 0)),
                   [(W - TW, height)],
                   edge((TW, height), (TW, TW), end_tabs, (-TW, 0)))


def post_outline(y0):
    """One layer of a panel post in (y, z): its top follows the panel, one tab into the bottom."""
    y1 = y0 + POST_LEN
    return outline(edge((y0, TW), (y1, TW), [(y0 + POST_TAB[0], y0 + POST_TAB[1])], (0, -TW)),
                   [(y1, TW), (y1, under_h(y1)), (y0, under_h(y0))])


BOTTOM = outline(edge((0, 0), (W, 0), widen(WALL_TABS), (0, TW)),
                 edge((W, 0), (W, D), widen(SIDE_TABS), (-TW, 0)),
                 edge((W, D), (0, D), widen(WALL_TABS), (0, -TW)),
                 edge((0, D), (0, 0), widen(SIDE_TABS), (TW, 0)))
BOTTOM_HOLES = [rect(x - SLACK / 2, y + POST_TAB[0] - CL / 2, x + 2 * TW + SLACK / 2, y + POST_TAB[1] + CL / 2)
                for _, x, y in POSTS]
BOTTOM_HOLES += [("circle", UNO[0] - 68.58 / 2 + u, UNO[1] - 53.34 / 2 + v, 1.6) for u, v in UNO_HOLES]
SIDE = outline(edge((0, TW), (D, TW), SIDE_TABS, (0, -TW)),
               edge((D, TW), (D, under_h(D)), widen(BACK_END_TABS), (-TW, 0)),
               [(D, under_h(D))],
               edge((0, under_h(0)), (0, TW), widen(FRONT_END_TABS), (TW, 0)))
FRONT = wall_outline(H_FRONT_WALL, FRONT_END_TABS)
BACK = wall_outline(H_BACK_WALL, BACK_END_TABS)
BACK_HOLES = []
if USB_SOCKET:
    ux_, uz_, uw_, uh_, us_, ud_ = USB_SOCKET
    BACK_HOLES = [rect(ux_ - uw_ / 2, uz_ - uh_ / 2, ux_ + uw_ / 2, uz_ + uh_ / 2)]
    BACK_HOLES += [("circle", ux_ + s * us_ / 2, uz_, ud_ / 2) for s in (-1, 1)]

KNOB_LAYER = [rounded_rect(0, 0, *KNOB, KNOB_R),
              rect((KNOB[0] - KNOB_SLOT[0]) / 2, (KNOB[1] - KNOB_SLOT[1]) / 2,
                   (KNOB[0] + KNOB_SLOT[0]) / 2, (KNOB[1] + KNOB_SLOT[1]) / 2)]
KNOB_TOP = [rounded_rect(0, 0, *KNOB, KNOB_R)]
LB_LAYER = [rect(0, 0, LB_LEN, LB_FRONT + LB_BACK)] + [
    rect(x - LB_X0 - LB_WINDOW[0] / 2, LB_FRONT - LB_WINDOW[1] / 2, x - LB_X0 + LB_WINDOW[0] / 2,
         LB_FRONT + LB_WINDOW[1] / 2) for _, x, _, _ in ANNUNCIATORS]
LB_FLOOR = [rect(0, 0, LB_LEN, LB_FRONT + LB_FLOOR_BACK)] + [
    ("circle", x - LB_X0, LB_FRONT, LED_HOLE_R) for _, x, _, _ in ANNUNCIATORS]

# ---------------- Body (world coordinates) ----------------
base = FEET
for i, (fx, fy) in enumerate(((25, 25), (W - 25, 25), (25, D - 25), (W - 25, D - 25))):
    cyl(f"Foot {i + 1}", fx, fy, 0, 10, FEET, M["rubber"])
# Panel-mount USB-B on the back wall, beside the Uno (its jack faces this way)
box("USB-B socket", W / 2 + 70, D + 1.5, base + 40, 26, 3, 20, M["dark_metal"])
box("USB-B port", W / 2 + 70, D + 3.1, base + 40, 12, 0.4, 11, M["black_plastic"])

# ---------------- Top panel frame: everything mounted on the slope ----------------
frame = bpy.data.objects.new("Panel frame", None)
frame.location = Vector((0, 0, FEET + H_FRONT)) * MM
frame.rotation_euler = (A, 0, 0)
COLL["Cockpit"].objects.link(frame)

# Every opening in the top panel, for its cut file, the template and the model.
OPENINGS = []


def cut_cyl(cx, cy, r):
    OPENINGS.append(("circle", cx, cy, r))


def cut_box(cx, cy, sx, sy):
    OPENINGS.append(("rect", cx, cy, sx, sy))


def cut_slot(cx, y0, y1, w):
    """A slot with round ends, y0..y1 overall."""
    OPENINGS.append(("slot", cx, y0, y1, w))


cut_cyl(*STICK, 20.0)
for dx in (-16.25, 16.25):
    for dy in (-16.25, 16.25):
        cut_cyl(STICK[0] + dx, STICK[1] + dy, 1.4)
cut_slot(THR_X, SLOT_Y0, SLOT_Y1, SLOT_W)
for dy in (-FADER_HOLES / 2, FADER_HOLES / 2):
    cut_cyl(THR_X, LEVER_Y + dy, 1.7)                  # M3 screws into the fader's tapped holes
for y in LED_YS:
    cut_cyl(LED_X, y, LED_HOLE_R)
for x in SHIELD_XS:
    cut_cyl(x, SHIELD_YS, LED_HOLE_R)
cut_cyl(*BIG, 12.0)    # uxcell 60 mm dome: 23.5 mm hole
cut_cyl(*MENU, 14.0)    # Happ-style 30 mm button: 28 mm hole
cut_cyl(*ARM, 6.0)
cut_box(LCD[0], LCD[1], 72.0, 25.0)
for dx in (-37.5, 37.5):
    for dy in (-15.5, 15.5):
        cut_cyl(LCD[0] + dx, LCD[1] + dy, 1.6)
for _, x, _, _ in ANNUNCIATORS:
    cut_box(x, ANN_Y, 22.0, 10.0)
# Five screws into the posts. No back-middle one: it would sit ~1 mm from the FAST window.
for cx, cy in PANEL_SCREWS:
    cut_cyl(cx, cy, 1.6)


def opening_loop(o):
    if o[0] == "circle":
        return o
    if o[0] == "rect":
        _, cx, cy, sx, sy = o
        return rect(cx - sx / 2, cy - sy / 2, cx + sx / 2, cy + sy / 2)
    _, cx, y0, y1, w = o
    return slot_loop(cx, y0, y1, w)


ACRYLIC, BIRCH = "Acrylic 0.118 in black (3 mm)", "Baltic birch plywood 0.250 in (6.35 mm)"
PARTS = [  # file name, material, quantity, loops (outline first)
    ("panel", ACRYLIC, 1, [rect(0, 0, W, D_SLOPE)] + [opening_loop(o) for o in OPENINGS]),
    ("knob_layer", ACRYLIC, 2 * (KNOB_LAYERS - 1), KNOB_LAYER),          # two knobs: one spare
    ("knob_top", ACRYLIC, 2, KNOB_TOP),
    ("light_box_layer", ACRYLIC, LB_LAYERS, LB_LAYER),
    ("light_box_floor", ACRYLIC, 1, LB_FLOOR),
    ("bottom", BIRCH, 1, [BOTTOM] + BOTTOM_HOLES),
    ("side", BIRCH, 2, [SIDE]),
    ("front", BIRCH, 1, [FRONT]),
    ("back", BIRCH, 1, [BACK] + BACK_HOLES),
    ("post_front", BIRCH, 2 * 3, [post_outline(TW)]),
    ("post_back", BIRCH, 2 * 2, [post_outline(D - TW - POST_LEN)]),
]


def loop_points(lp, seg_deg=6.0):
    """A loop as plain (u, v) points, arcs expanded."""
    if lp[0] == "circle":
        _, cx, cy, r = lp
        n = 48
        return [(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n)) for i in range(n)]
    pts = []
    for i, (u, v, b) in enumerate(lp):
        pts.append((u, v))
        if not b:
            continue
        u1, v1, _ = lp[(i + 1) % len(lp)]
        chord = math.hypot(u1 - u, v1 - v)
        off = chord / 2 * (1 - b * b) / (2 * b)              # from the chord's middle to the centre, leftwards
        cx = (u + u1) / 2 - (v1 - v) / chord * off
        cy = (v + v1) / 2 + (u1 - u) / chord * off
        sweep = 4 * math.atan(b)
        r, a0 = math.hypot(u - cx, v - cy), math.atan2(v - cy, u - cx)
        n = max(2, int(abs(math.degrees(sweep)) / seg_deg))
        pts += [(cx + r * math.cos(a0 + sweep * k / n), cy + r * math.sin(a0 + sweep * k / n)) for k in range(1, n)]
    return pts


def write_dxf(path, loops):
    """One part as an R12 DXF in inches, shifted so its lower left corner is at the origin."""
    pts = [p for lp in loops for p in loop_points(lp)]
    u0, v0 = min(p[0] for p in pts), min(p[1] for p in pts)
    inch = lambda x: f"{x / 25.4:.6f}"
    out = ["0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1009", "9", "$INSUNITS", "70", "1",
           "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES"]
    for lp in loops:
        if lp[0] == "circle":
            _, cx, cy, r = lp
            out += ["0", "CIRCLE", "8", "0", "10", inch(cx - u0), "20", inch(cy - v0), "30", "0", "40", inch(r)]
            continue
        out += ["0", "POLYLINE", "8", "0", "66", "1", "70", "1", "10", "0", "20", "0", "30", "0"]
        for u, v, b in lp:
            out += ["0", "VERTEX", "8", "0", "10", inch(u - u0), "20", inch(v - v0), "30", "0"]
            if b:
                out += ["42", f"{b:.6f}"]
        out += ["0", "SEQEND", "8", "0"]
    out += ["0", "ENDSEC", "0", "EOF"]
    path.write_text("\n".join(out) + "\n", encoding="ascii")
    return max(p[0] for p in pts) - u0, max(p[1] for p in pts) - v0


def write_cut_files():
    """cut/<part>.dxf for every flat part, and cut/parts.csv with material, quantity and size."""
    out = HERE / "cut"
    out.mkdir(exist_ok=True)
    for old in out.glob("*.dxf"):          # parts that no longer exist
        if old.stem not in {p[0] for p in PARTS}:
            old.unlink()
    rows = ["file,material,quantity,width_in,height_in,width_mm,height_mm"]
    for name, mat, qty, loops in PARTS:
        w, h = write_dxf(out / f"{name}.dxf", loops)
        rows.append(f'{name}.dxf,"{mat}",{qty},{w / 25.4:.3f},{h / 25.4:.3f},{w:.1f},{h:.1f}')
    (out / "parts.csv").write_text("\n".join(rows) + "\n", encoding="utf-8")
    print(f"Wrote {len(PARTS)} cut files to {out}")
    if not USB_SOCKET:
        print("NOTE: the back wall has no USB opening until USB_SOCKET is measured.")
    print(f"NOTE: panel openings from listings, not measured: {PANEL_TODO}; LED holes {2 * LED_HOLE_R} mm.")


# ---------------- Panel cut file and printable template ----------------
def svg_y(y):
    return D_SLOPE - y                     # SVG y runs down the page, so the front edge is at the bottom


def svg_shape(o):
    if o[0] == "circle":
        _, cx, cy, r = o
        return f'<circle cx="{cx:.2f}" cy="{svg_y(cy):.2f}" r="{r:.2f}"/>'
    if o[0] == "rect":
        _, cx, cy, sx, sy = o
        return f'<rect x="{cx - sx / 2:.2f}" y="{svg_y(cy + sy / 2):.2f}" width="{sx:.2f}" height="{sy:.2f}"/>'
    _, cx, y0, y1, w = o
    return (f'<rect x="{cx - w / 2:.2f}" y="{svg_y(y1):.2f}" width="{w:.2f}" height="{y1 - y0:.2f}" '
            f'rx="{w / 2:.2f}"/>')


def svg_text(x, y, body, size=3.5, anchor="middle", rot=0, color="#333"):
    turn = f' transform="rotate({rot} {x:.2f} {svg_y(y):.2f})"' if rot else ""
    return (f'<text x="{x:.2f}" y="{svg_y(y):.2f}" font-size="{size}" text-anchor="{anchor}" '
            f'fill="{color}"{turn}>{body}</text>')


def write_panel_files():
    """panel_cut.svg: 1:1 outline + openings (a preview; the order uses cut/panel.dxf).
    panel_template.svg: the same with part footprints, labels and a 10 mm grid, for a cardboard mock-up.
    panel_template.html: that template tiled over 4 landscape Letter pages for printing at 100 %."""
    shapes = "\n".join(svg_shape(o) for o in OPENINGS)
    outline_ = f'<rect x="0" y="0" width="{W:g}" height="{D_SLOPE:.2f}"/>'
    (HERE / "panel_cut.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{W:g}mm" height="{D_SLOPE:.2f}mm" '
        f'viewBox="0 0 {W:g} {D_SLOPE:.2f}">\n<g fill="none" stroke="#000" stroke-width="0.1">\n'
        f'{outline_}\n{shapes}\n</g>\n</svg>\n', encoding="utf-8")

    grid = "".join(f'<line x1="{x}" y1="0" x2="{x}" y2="{D_SLOPE:.2f}"/>' for x in range(10, int(W), 10))
    grid += "".join(f'<line x1="0" y1="{svg_y(y):.2f}" x2="{W:g}" y2="{svg_y(y):.2f}"/>'
                    for y in range(10, int(D_SLOPE), 10))
    ax_, ay_ = ARM
    feet = [  # footprints of the parts above and below the panel (dashed), from the listings
        ("circle", STICK[0], STICK[1], 26.75), ("rect", THR_X, LEVER_Y, KNOB[0], KNOB[1]),
        ("rect", THR_X, LEVER_Y, FADER_W, FADER_L),
        ("circle", BIG[0], BIG[1], 30.0), ("circle", MENU[0], MENU[1], 16.65),
        ("rect", ax_, ay_, 28.0, 40.0), ("rect", LCD[0], LCD[1], 80.0, 36.0),
    ]
    labels = [
        svg_text(STICK[0], STICK[1] - 33, "STICK: 40 mm hole, M3 on a 32.5 mm square"),
        svg_text(THR_X - KNOB[0] / 2 - 5, LEVER_Y, "THROTTLE: 100 mm fader under a 3.5 mm slot, knob at cruise",
                 rot=-90),
        svg_text(LED_X + 7, (LED_YS[0] + LED_YS[-1]) / 2, "8 throttle lights", rot=-90),
        svg_text(BIG[0], BIG[1] - 20, "EMP: 60 mm dome, 24 mm hole"),
        svg_text(MENU[0] - 20, MENU[1] - 1, "MENU: 28 mm hole", anchor="end"),
        svg_text(ax_, ay_ - 24, "MASTER ARM: 12 mm hole"),
        svg_text(LCD[0], LCD[1] - 1.2, "LCD window 72 × 25", size=3),
        svg_text(SHIELD_XS[1], SHIELD_YS + 5, "SHIELD", size=3),
        svg_text(W - 60, PALM + 2, f"palm rest ({PALM:g} mm)", color="#888"),
        svg_text(W / 2, 3, "FRONT EDGE", size=3, color="#888"),
    ] + [svg_text(x, ANN_Y - 1.2, name, size=3) for name, x, _, _ in ANNUNCIATORS]
    body = (f'<g stroke="#e4e4e4" stroke-width="0.15">{grid}</g>\n'
            f'<line x1="0" y1="{svg_y(PALM):.2f}" x2="{W:g}" y2="{svg_y(PALM):.2f}" stroke="#aaa" '
            f'stroke-width="0.25" stroke-dasharray="3 2"/>\n'
            f'<g fill="none" stroke="#888" stroke-width="0.25" stroke-dasharray="2 1.5">\n'
            + "\n".join(svg_shape(f) for f in feet) + '\n</g>\n'
            f'<g fill="none" stroke="#000" stroke-width="0.3">\n{outline_}\n{shapes}\n</g>\n'
            f'<g font-family="Arial, sans-serif">\n' + "\n".join(labels) + '\n</g>\n')
    (HERE / "panel_template.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{W:g}mm" height="{D_SLOPE:.2f}mm" '
        f'viewBox="0 0 {W:g} {D_SLOPE:.2f}">\n{body}</svg>\n', encoding="utf-8")

    # Four tiles with a 10 mm overlap (shaded), back row first so the pages read like the panel.
    mid_x, mid_y, lap, pad = W / 2, D_SLOPE / 2, 5.0, 5.0
    cols = [(-pad, mid_x + lap), (mid_x - lap, W + pad)]
    rows = [(-pad, mid_y + lap), (mid_y - lap, D_SLOPE + pad)]
    shade = (f'<rect x="{mid_x - lap:.2f}" y="-5" width="{2 * lap:g}" height="{D_SLOPE + 10:.2f}" fill="#000" '
             f'fill-opacity="0.06"/><rect x="-5" y="{mid_y - lap:.2f}" width="{W + 10:g}" height="{2 * lap:g}" '
             f'fill="#000" fill-opacity="0.06"/>')
    names = ["back left", "back right", "front left", "front right"]
    pages = []
    for i, ((y0, y1), (x0, x1)) in enumerate((r, c) for r in rows for c in cols):
        pages.append(
            f'<div class="page"><div class="head">Axiom cockpit panel, compact ({W:g} × {D_SLOPE:.1f} mm): '
            f'page {i + 1} of 4, {names[i]}. Box: {W:g} × {D:g} mm, front {H_FRONT:g} mm, '
            f'back {H_FRONT + D * math.tan(A):.0f} mm tall</div>\n'
            f'<svg width="{x1 - x0:.2f}mm" height="{y1 - y0:.2f}mm" viewBox="{x0:.2f} {y0:.2f} {x1 - x0:.2f} '
            f'{y1 - y0:.2f}">\n{shade}\n{body}</svg>\n'
            f'<div class="foot"><span class="bar"></span> must measure 100 mm (print at 100 % / Actual size). '
            f'Overlap the shaded strips so the grid lines up.</div></div>')
    (HERE / "panel_template.html").write_text(
        '<!doctype html>\n<html><head><meta charset="utf-8"><title>Axiom panel template</title>\n<style>\n'
        '@page { size: 279.4mm 215.9mm; margin: 0 }\n'
        'body { margin: 0; font-family: Arial, sans-serif; background: #fff; color: #222 }\n'
        '.page { width: 279.4mm; height: 215.9mm; position: relative; overflow: hidden; break-after: page }\n'
        '.head { position: absolute; left: 12mm; top: 6mm; font-size: 10pt }\n'
        '.page svg { position: absolute; left: 12mm; top: 13mm }\n'
        '.foot { position: absolute; left: 12mm; bottom: 7mm; font-size: 9pt }\n'
        '.bar { display: inline-block; width: 100mm; height: 2.5mm; background: #000; vertical-align: middle }\n'
        '</style></head><body>\n' + "\n".join(pages) + '\n</body></html>\n', encoding="utf-8")
    print("Wrote panel_cut.svg, panel_template.svg, panel_template.html")

    # The legend strip behind the warning windows: white words on black, so only the words glow. Print at 100 %,
    # cut along the outline (the light box stack's size) and lay it face up between the panel and the stack.
    h = LB_FRONT + LB_BACK
    words = "\n".join(
        f'<text x="{x - LB_X0 + 10:.2f}" y="{LB_BACK + 1.9:.2f}" font-size="5.2" font-weight="bold" '
        f'text-anchor="middle" fill="#fff" font-family="Segoe UI, Arial, sans-serif">{name}</text>'
        for name, x, _, _ in ANNUNCIATORS)
    (HERE / "legends.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{LB_LEN + 20:g}mm" height="{h + 24:g}mm" '
        f'viewBox="0 0 {LB_LEN + 20:g} {h + 24:g}">\n'
        f'<rect x="10" y="10" width="{LB_LEN:g}" height="{h:g}" fill="#000" stroke="#888" stroke-width="0.2"/>\n'
        f'<g transform="translate(0 10)">\n{words}\n</g>\n'
        f'<rect x="10" y="{h + 14:g}" width="50" height="2" fill="#000"/>\n'
        f'<text x="63" y="{h + 16:g}" font-size="3" fill="#555" font-family="Arial, sans-serif">must measure 50 mm. '
        f'Cut on the grey line; face up, this edge towards you.</text>\n</svg>\n',
        encoding="utf-8")
    print("Wrote legends.svg")


write_cut_files()
write_panel_files()
if "--cut" in sys.argv:
    sys.exit(0)


# ---------------- Flat parts in the model ----------------
def flat_part(name, loops, thick, mat, ex, ey, origin, parent=None, coll="Cockpit"):
    """Extrudes a part's loops (holes included) to its thickness. ex and ey are where the outline's u and v
    axes point; origin is where (0, 0) lands, on the board's middle plane."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "2D"
    cu.fill_mode = "BOTH"
    cu.extrude = thick / 2 * MM
    for lp in loops:
        pts = loop_points(lp)
        sp = cu.splines.new("POLY")
        sp.points.add(len(pts) - 1)
        for p, (u, v) in zip(sp.points, pts):
            p.co = (u * MM, v * MM, 0, 1)
        sp.use_cyclic_u = True
    tmp = bpy.data.objects.new(name, cu)
    scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()                      # the caps and sides come out separate: weld them into one solid
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.materials.clear()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    link(ob, coll, parent)
    ez = Vector(ex).cross(Vector(ey))
    m = Matrix((ex, ey, ez)).transposed().to_4x4()
    m.translation = Vector(origin) * MM
    ob.matrix_basis = m
    return ob


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
flat_part("Bottom", [BOTTOM] + BOTTOM_HOLES, TW, M["ply"], X, Y, (0, 0, FEET + TW / 2))
for side, x in (("left", TW / 2), ("right", W - TW / 2)):
    flat_part(f"Side {side}", [SIDE], TW, M["ply"], Y, Z, (x, 0, FEET))
flat_part("Front wall", [FRONT], TW, M["ply"], X, Z, (0, TW / 2, FEET))
flat_part("Back wall", [BACK] + BACK_HOLES, TW, M["ply"], X, Z, (0, D - TW / 2, FEET))
for name, x, y in POSTS:
    for layer in range(2):
        flat_part(f"Post {name} {layer + 1}", [post_outline(y)], TW, M["ply"], Y, Z,
                  (x + TW / 2 + layer * TW, 0, FEET), coll="Internals")

flat_part("Top panel (3 mm acrylic)", PARTS[0][3], T_TOP, M["acrylic"], X, Y, (0, 0, -T_TOP / 2), frame)

# Palm rest
rounded(box("Palm rest", W / 2, PALM / 2 + 1, 3.5, W - 4, PALM - 2, 7, M["palm"], frame), 3.4, 6)

# ---------------- Throttle ----------------
IN = "Internals"
box("Throttle fader", THR_X, LEVER_Y, -T_TOP - FADER_H / 2, FADER_W, FADER_L, FADER_H, M["dark_metal"], frame, IN)
box("Throttle lever", THR_X, LEVER_Y, -T_TOP + LEVER_L / 2, LEVER_T, LEVER_W, LEVER_L, M["lever"], frame)
for i, dy in enumerate((-FADER_HOLES / 2, FADER_HOLES / 2)):
    cyl(f"Fader screw {i + 1}", THR_X, LEVER_Y + dy, 0, 2.75, 1.6, M["dark_metal"], frame, segs=24)
box("Knob felt", THR_X, LEVER_Y, KNOB_LIFT / 2, KNOB[0] - 2, KNOB[1] - 2, KNOB_LIFT, M["felt"], frame)
for i in range(KNOB_LAYERS):
    flat_part(f"Knob layer {i + 1}", KNOB_TOP if i == KNOB_LAYERS - 1 else KNOB_LAYER, T_TOP, M["acrylic"], X, Y,
              (THR_X - KNOB[0] / 2, LEVER_Y - KNOB[1] / 2, KNOB_LIFT + T_TOP / 2 + i * T_TOP), frame)
for i, y in enumerate(LED_YS):
    # Example state: slow-zone band (LEDs 1-2) and the lever's own LED at cruise.
    lit = M["led_band"] if i < 2 else M["led_white"] if i == 4 else M["led_off"]
    cyl(f"Throttle LED {i + 1}", LED_X, y, -3.0, 2.5, 5.6, lit, frame)
    dome(f"Throttle LED {i + 1} dome", LED_X, y, 2.6, 2.5, 2.5, lit, frame)
text("Label THROTTLE", "THROTTLE", LED_X + 11, (LED_YS[0] + LED_YS[-1]) / 2, 5, M["label"], frame, rot_z=math.pi / 2)
text("Label MAX", "MAX", LED_X, SLOT_Y1 + 9, 3.6, M["label"], frame)
text("Label MIN", "MIN", LED_X, SLOT_Y0 - 1, 3.6, M["label"], frame)
# Rumble under the palm rest, where the throttle hand rests: it ticks at cruise and buzzes into boost.
box("Vibration module", THR_X, 38.0, -T_TOP - 4.0, 30, 22, 8, M["pcb_blue"], frame, IN)

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
# EMP: 60 mm red dome arcade button (uxcell, momentary microswitch) on a yellow legend ring
cyl("EMP legend plate", BIG[0], BIG[1], 0, 35.0, 1.2, M["yellow"], frame)
text("EMP legend", "EMP", BIG[0], BIG[1] + 32.0, 3.6, M["ink"], frame, z=1.25)
cyl("EMP bezel", BIG[0], BIG[1], 1.2, 30.0, 5.0, M["black_plastic"], frame, r2=29.0)
cyl("EMP dome skirt", BIG[0], BIG[1], 6.2, 27.5, 3.0, M["red"], frame)
dome("EMP dome", BIG[0], BIG[1], 9.2, 27.5, 13.0, M["red"], frame)
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
# Behind the windows: the printed legend, the stack of window plates that keeps neighbours from bleeding into
# each other, and its floor with an LED module under each window.
lb_top = -T_TOP - PAPER
box("Legend paper", CENTRE, ANN_Y + (LB_BACK - LB_FRONT) / 2, -T_TOP - PAPER / 2, LB_LEN, LB_FRONT + LB_BACK, PAPER,
    M["paper"], frame, IN)
for i in range(LB_LAYERS + 1):
    flat_part(f"Light box {'floor' if i == LB_LAYERS else f'layer {i + 1}'}", LB_FLOOR if i == LB_LAYERS else LB_LAYER,
              T_TOP, M["acrylic"], X, Y, (LB_X0, ANN_Y - LB_FRONT, lb_top - T_TOP / 2 - i * T_TOP), frame, IN)
lb_bottom = lb_top - (LB_LAYERS + 1) * T_TOP
for label, x, color, lit in ANNUNCIATORS:
    pixel = material(f"{label} pixel", color, emit=color, strength=12) if lit else M["led_off"]
    cyl(f"{label} LED module", x, ANN_Y, lb_bottom - 3.0, 5.0, 3.0, pixel, frame, IN)
for i, x in enumerate(SHIELD_XS):
    cyl(f"Shield LED {i + 1}", x, SHIELD_YS, -3.0, 2.5, 5.6, M["led_blue"], frame)
    dome(f"Shield LED {i + 1} dome", x, SHIELD_YS, 2.6, 2.5, 2.5, M["led_blue"], frame)
text("Label SHIELD", "SHIELD", SHIELD_XS[1], SHIELD_YS + 11, 3.6, M["label"], frame)
text("Wordmark", "AXIOM", STICK[0], WORD_Y, 13, M["label"], frame, face="segoeuil.ttf", spacing=1.35)

# ---------------- Internals (hang from the panel unless noted) ----------------
box("Stick body", sx, sy, -3 - 12.75, 40.4, 40.4, 25.5, M["black_plastic"], frame, IN)
cyl("EMP body", BIG[0], BIG[1], -3 - 33, 12, 33, M["black_plastic"], frame, IN)
box("EMP microswitch", BIG[0], BIG[1], -3 - 33 - 9, 20, 10, 18, M["black_plastic"], frame, IN)
for name, (bx, by) in (("MENU", MENU),):
    cyl(f"{name} body", bx, by, -3 - 30.3, 14, 30.3, M["black_plastic"], frame, IN)
cyl("ARM body", ax, ay, -3 - 35, 6, 35, M["dark_metal"], frame, IN)
box("LCD module", lx, ly, -3 - 7, 80, 36, 8, M["pcb_green"], frame, IN)
# Uno + Gravity IO shield on the floor near the back wall, turned sideways so its plugs clear the MENU
# button's microswitch (world coordinates)
ux, uy = UNO
floor = FEET + TW
for i, (u, v) in enumerate(UNO_HOLES):
    cyl(f"Uno standoff {i + 1}", ux - 68.58 / 2 + u, uy - 53.34 / 2 + v, floor, 2.75, 10, M["metal"], coll=IN, segs=6)
box("Arduino Uno", ux, uy, floor + 10.8, 68.58, 53.34, 1.6, M["pcb_blue"], coll=IN)
box("Gravity IO shield", ux, uy, floor + 24, 68.58, 53.34, 1.6, M["pcb_green"], coll=IN)
for side in (-1, 1):
    box(f"Shield headers {'front' if side < 0 else 'back'}", ux, uy + side * 21, floor + 29, 60, 8, 9,
        M["terminal"], coll=IN)
box("Buzzer module", ux - 60, uy, floor + 3, 30, 22, 6, M["pcb_blue"], coll=IN)

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
    out = HERE / "renders"
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
