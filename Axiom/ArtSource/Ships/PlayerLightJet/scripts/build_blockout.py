"""Create a new, non-destructive Swept Dart review scene in the current Blender file.

Run from Blender's Python console via exec(compile(...)), or in background Blender.
Existing scenes/objects are retained. A repeated revision is refused, not overwritten.
All geometry coordinates below are Unity-local metres; B() converts to Blender axes.
This is a proportion study, not a Unity-ready asset or a final material/reveal system.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REVISION = globals().get('REVISION', 'v02')
NAME = "Swept Dart | " + REVISION
if NAME in bpy.data.scenes:
    raise RuntimeError("This revision already exists; increment REVISION to preserve it.")
if bpy.context.mode != 'OBJECT':
    raise RuntimeError("Finish the current edit operation before generating a review scene.")


def B(point):
    """Unity +Z forward/+Y up -> Blender -Y forward/+Z up (proper rotation)."""
    x, y, z = point
    return Vector((x, -z, y))


scene = bpy.data.scenes.new(NAME)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.render.film_transparent = False
scene.render.fps = 24
scene.frame_end = 96


def collection(name):
    c = bpy.data.collections.new(name + ' | ' + REVISION)
    scene.collection.children.link(c)
    return c


body = collection('01 Hull')
lights = collection('02 Light placement study')
sockets = collection('03 Integration anchors')
reference = collection('04 Placeholder reference')
guides = collection('05 Collider guide')
studio = collection('06 Review cameras and lights')
palette = {c['name']: c['hex'] for c in json.loads(
    (HERE.parents[2] / 'Palette/palette.json').read_text())['colors']}


def rgba(token):
    v = palette[token].lstrip('#')
    rgb = [int(v[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
                 for c in rgb) + (1,)


def material(name, token, metal=0, roughness=.4, emission=0):
    m = bpy.data.materials.new(name + ' | ' + REVISION)
    m.diffuse_color = rgba(token)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = rgba(token)
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Emission Color'].default_value = rgba(token)
    bsdf.inputs['Emission Strength'].default_value = emission
    m['palette_token'] = token
    return m


hull = material('Hull', 'Structure', .35, .33)
facet = material('Secondary facet', 'Panel', .3, .38)
edge = material('Chamfer', 'Edge', .45, .32)
inset = material('Inset', 'Void', .2, .25)
cyan = material('Permanent light placement', 'Cyan', .1, .3, 2)
white = material('Shield and drive core placement', 'Light', .1, .3, 2.5)
clay = material('Clay inspection', 'Muted', 0, .6)
wire = material('Collider guide', 'Cyan', 0, .5, .7)

root = bpy.data.objects.new('SweptDart_ROOT', None)
body.objects.link(root)
root.empty_display_type = 'PLAIN_AXES'
root.empty_display_size = .25
root['purpose'] = 'Stage 1 proportion blockout; no gameplay or reveal animation'
root['coordinate_contract'] = 'Source Blender -Y forward/+Z up; Unity +Z forward/+Y up'


def mesh(name, points, faces, mat=hull, coll=body, bevel=.008, parent=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata([B(p) for p in points], [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    if parent:
        obj.parent = root
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Small inspectable edge bevel', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.affect = 'EDGES'
        mod.limit_method = 'ANGLE'
    obj['stage'] = 'proportion blockout'
    return obj


def loft(name, sections, mat=hull, cx=0, bevel=.008):
    # sections: (forward z, half width, top y, bottom y); octagonal cross-sections.
    pts = []
    for z, w, top, bottom in sections:
        mid = (top + bottom) / 2
        pts.extend([(cx - w * .62, top, z), (cx + w * .62, top, z),
                    (cx + w, mid + (top - mid) * .45, z),
                    (cx + w, mid - (mid - bottom) * .45, z),
                    (cx + w * .62, bottom, z), (cx - w * .62, bottom, z),
                    (cx - w, mid - (mid - bottom) * .45, z),
                    (cx - w, mid + (top - mid) * .45, z)])
    faces = [tuple(range(7, -1, -1))]
    for i in range(len(sections) - 1):
        for j in range(8):
            a = i * 8 + j
            b = i * 8 + (j + 1) % 8
            faces.append((a, b, b + 8, a + 8))
    faces.append(tuple(range(len(pts) - 8, len(pts))))
    return mesh(name, pts, faces, mat, bevel=bevel)


def prism(name, outline, depth, mat=hull, bevel=.008):
    # outline follows its top perimeter; a second layer closes its underside.
    n = len(outline)
    pts = outline + [(x, y - depth, z) for x, y, z in outline]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return mesh(name, pts, faces, mat, bevel=bevel)


def tube(name, points, radius, mat, coll=lights, closed=False, parent=True):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 0
    curve.use_fill_caps = True
    spl = curve.splines.new('POLY')
    spl.points.add(len(points) - 1)
    for p, co in zip(spl.points, points):
        p.co = (*B(co), 1)
    spl.use_cyclic_u = closed
    obj = bpy.data.objects.new(name, curve)
    coll.objects.link(obj)
    if parent:
        obj.parent = root
    curve.materials.append(mat)
    return obj


loft('Hull_Central', [(2.5, .045, .025, -.025), (1.85, .13, .13, -.065),
     (1.05, .24, .23, -.12), (.3, .36, .34, -.19),
     (-.55, .41, .35, -.22), (-1.44, .33, .23, -.18)])
loft('Dorsal_Spine', [(1.1, .11, .25, .18), (.52, .2, .48, .25),
     (-.2, .24, .5, .29), (-.9, .22, .4, .27), (-1.38, .18, .25, .18)], facet)
loft('Canopy_Inset', [(.99, .09, .28, .22), (.48, .165, .492, .31),
     (-.18, .18, .51, .34), (-.45, .15, .46, .33)], inset, bevel=.006)

for sign, side in [(-1, 'L'), (1, 'R')]:
    def mirror(points):
        return [(sign * x, y, z) for x, y, z in points]

    # Cross-sections along span: tapered, faceted wing with a low structural ridge.
    stations = [(.30, .98, -1.32, .17, -.13),
                (.80, .42, -1.36, .16, -.10),
                (1.40, -.40, -1.30, .10, -.075),
                (1.94, -1.12, -1.42, .035, -.045)]
    pts = []
    for x, front, back, top, bottom in stations:
        pts += mirror([(x, top - .055, front), (x, top, front * .45 + back * .55),
                       (x, top - .045, back), (x, bottom, back), (x, bottom, front)])
    faces = [tuple(range(4, -1, -1))]
    for i in range(len(stations) - 1):
        for j in range(5):
            faces.append((i * 5 + j, i * 5 + (j + 1) % 5,
                          (i + 1) * 5 + (j + 1) % 5, (i + 1) * 5 + j))
    faces.append(tuple(range(len(pts) - 5, len(pts))))
    wing = mesh('Wing_' + side, pts, faces, facet)
    wing.data.materials.append(hull)
    for polygon in wing.data.polygons:
        if polygon.normal.z < .2:
            polygon.material_index = 1

    prism('Tip_Chine_' + side, mirror([(1.84, .085, -.97), (2, .09, -1.12),
          (1.95, .035, -1.49), (1.80, .025, -1.32)]), .07, hull, .005)
    loft('Drive_Housing_' + side, [(.53, .16, .23, -.09),
         (.13, .26, .36, -.16), (-.65, .31, .35, -.22),
         (-1.56, .29, .24, -.20)], hull, cx=sign * .85)
    # Closed dark drive-end plate and bright recessed aperture indication.
    loft('Drive_Recess_' + side, [(-1.565, .22, .15, -.13),
         (-1.625, .21, .14, -.12)], inset, cx=sign * .85, bevel=.003)
    tube('Drive_Light_' + side, mirror([(.70, .01, -1.638), (1, .01, -1.638)]), .024, white)
    tube('Wing_Channel_' + side, mirror([(.99, .139, -.78),
         (1.43, .087, -1.04), (1.78, .039, -1.27)]), .013, cyan)
    tube('Nose_Channel_' + side, mirror([(.055, .058, 2.22),
         (.14, .154, 1.65), (.205, .238, 1.12)]), .009, cyan)
    # Short forward sockets integrated into the wing roots, not concept-tip gun relocation.
    loft('Weapon_Fairing_' + side, [(1.1, .07, .055, -.055),
         (.77, .11, .10, -.08), (.36, .16, .16, -.09)], hull, cx=sign * .75, bevel=.005)
    tube('Weapon_Aperture_' + side, mirror([(.71, .0, 1.103), (.79, .0, 1.103)]), .012, cyan)

for i, x in enumerate([-.1, 0, .1], 1):
    tube('Shield_Segment_' + str(i), [(x, .367, -1.04), (x, .30, -1.26)], .018, white)

anchor_data = {'Muzzle_L': (-.75, 0, 1.1), 'Muzzle_R': (.75, 0, 1.1),
               'Exhaust_L': (-.85, 0, -1.64), 'Exhaust_R': (.85, 0, -1.64)}
for name, co in anchor_data.items():
    obj = bpy.data.objects.new(name, None)
    sockets.objects.link(obj)
    obj.parent = root
    obj.location = B(co)
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = .12
    obj['unity_local_position'] = list(co)

# Reference geometry is a reconstruction of PrototypeMesh.BuildAircraft, not a Unity asset edit.
old = mesh('REFERENCE_PrototypeHull', [(-2, 0, -1.4), (0, 0, 2.5), (2, 0, -1.4),
           (0, .5, -.5), (0, -.22, -.5)],
           [(0, 1, 3), (1, 2, 3), (2, 0, 3), (0, 4, 1), (1, 4, 2), (2, 4, 0)],
           clay, reference, 0, False)
reference.hide_render = True
reference.hide_viewport = True

# BoxCollider center (0,.1,.3), size (2.4,.6,2.6), intentionally smaller than visual mesh.
corners = [(x, y, z) for x in (-1.2, 1.2) for y in (-.2, .4) for z in (-1, 1.6)]
for i, p in enumerate(corners):
    for j, q in enumerate(corners[i + 1:], i + 1):
        if sum(a != b for a, b in zip(p, q)) == 1:
            tube('ColliderEdge_%d_%d' % (i, j), [p, q], .006, wire, guides, parent=False)
guides.hide_render = True
guides.hide_viewport = True
sockets.hide_viewport = True

world = bpy.data.worlds.new('Review neutral world | ' + REVISION)
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = rgba('Panel')
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
scene.world = world


def aim(obj, point):
    obj.rotation_euler = (B(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def camera(name, pos, target, ortho=None, vfov=62):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = B(pos)
    aim(obj, target)
    if ortho:
        data.type = 'ORTHO'
        data.ortho_scale = ortho
    else:
        data.sensor_fit = 'VERTICAL'
        data.sensor_height = 24
        data.lens = 12 / math.tan(math.radians(vfov) / 2)
    data.clip_start = .01
    data.clip_end = 500
    return obj


cam = camera('VIEW_Front_ThreeQuarter', (5, 4.4, 7), (0, .05, .25), 6)
camera('VIEW_Rear_ThreeQuarter', (-4.6, 3.3, -6.5), (0, .06, .15), 5.8)
camera('VIEW_Top', (0, 10, .35), (0, 0, .35), 7.2)
camera('VIEW_Side', (10, .15, .35), (0, .15, .35), 5.2)
camera('VIEW_Underside', (4, -4.2, 5), (0, 0, .25), 5.6)
camera('VIEW_Rear', (0, 1.5, -8), (0, .1, .1), 5.2)
camera('VIEW_Chase_Cruise', (0, 2, -9.5), (0, 0, 20))
camera('VIEW_Chase_Boost', (0, 2, -9.5), (0, 0, 20), vfov=72)
turn = camera('VIEW_Turntable', (0, 2.6, 6.5), (0, 0, .25), 5.7)
turn.rotation_mode = 'QUATERNION'
previous_rotation = None
for frame in range(1, 98, 4):
    angle = (frame - 1) / 96 * math.tau
    turn.location = B((math.sin(angle) * 6.5, 2.6, .25 + math.cos(angle) * 6.5))
    rotation = (B((0, .05, .25)) - turn.location).to_track_quat('-Z', 'Y')
    if previous_rotation is not None and rotation.dot(previous_rotation) < 0:
        rotation.negate()
    turn.rotation_quaternion = rotation
    previous_rotation = rotation.copy()
    turn.keyframe_insert('location', frame=frame)
    turn.keyframe_insert('rotation_quaternion', frame=frame)

for name, pos, energy, size in [('Key', (1, 5, 3), 400, 5),
                               ('Fill', (-4, 2, 0), 250, 4),
                               ('Rim', (0, 3, -5), 500, 3),
                               ('Underside', (1, -4, 2), 300, 5)]:
    data = bpy.data.lights.new('STUDIO_' + name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new('STUDIO_' + name, data)
    obj.hide_render = name == 'Underside'
    studio.objects.link(obj)
    obj.location = B(pos)
    aim(obj, (0, 0, 0))

scene.camera = cam
scene['revision'] = REVISION
scene['render_note'] = 'Blender studio preview, not Unity URP/CRT verification'
scene['collections'] = json.dumps({key: value.name for key, value in
    [('body', body), ('lights', lights), ('reference', reference), ('guides', guides), ('studio', studio)]})
scene.frame_set(1)
bpy.context.view_layer.update()

# Leave the visible model framed in Layout, with neutral studio shading for immediate inspection.
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in body.objects:
    if obj.type == 'MESH':
        obj.select_set(True)
bpy.context.view_layer.objects.active = next(o for o in body.objects if o.name.startswith('Hull_Central'))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'SOLID'
            space.shading.color_type = 'MATERIAL'
            space.shading.light = 'STUDIO'
            space.shading.show_cavity = True
            space.overlay.show_floor = False
            space.overlay.show_extras = False
            space.region_3d.view_rotation = cam.rotation_euler.to_quaternion()
            space.region_3d.view_location = B((0, .05, .25))
            space.region_3d.view_distance = 6.8

output = HERE.parent / 'reviews' / REVISION
output.mkdir(parents=True, exist_ok=True)
manifest = {'revision': REVISION, 'stage': 'proportion blockout',
            'unity_local_sockets': anchor_data,
            'chase_preview': {'distance_with_cruise_lag': 9.5, 'height': 2,
                              'look_ahead': 20, 'vertical_fov': 62},
            'palette_source': '../../../../Palette/palette.json',
            'source_axes': 'Blender -Y forward / +Z up; coordinate inputs in Unity frame',
            'mesh_objects': [], 'checks': {}}
all_points = []
for obj in body.objects:
    if obj.type != 'MESH':
        continue
    obj.data.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    manifold = all(e.is_manifold for e in bm.edges)
    volume = bm.calc_volume(signed=True)
    bm.free()
    assert manifold and volume > 0, obj.name
    all_points += [v.co for v in obj.data.vertices]
    manifest['mesh_objects'].append({'name': obj.name, 'vertices': len(obj.data.vertices),
        'base_triangles': len(obj.data.loop_triangles), 'closed_manifold': manifold, 'volume': volume})
mins = [min(p[i] for p in all_points) for i in range(3)]
maxs = [max(p[i] for p in all_points) for i in range(3)]
manifest['hull_bounds_unity'] = {'min': [mins[0], mins[2], -maxs[1]],
                                'max': [maxs[0], maxs[2], -mins[1]]}
manifest['checks']['closed_hull_sections'] = True
manifest['checks']['unity_import_or_play_mode'] = False
(output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf8')
print(json.dumps({'scene': scene.name, 'output': str(output), 'meshes': len(manifest['mesh_objects'])}))
