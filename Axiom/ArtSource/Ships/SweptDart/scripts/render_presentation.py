"""Render approved v13 presentation media without saving or modifying the source file.

blender --background reviews/v13/SweptDart_v13.blend --python scripts/render_presentation.py
Use -- --stills or -- --animation to render only one set.
"""
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / 'reviews/v13/presentation'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes['Swept Dart luminous assembly | v13']
bpy.context.window.scene = scene
source = Path(bpy.data.filepath)
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
scene.render.engine = 'CYCLES'
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'
prefs.get_devices()
for device in prefs.devices:
    device.use = device.type == 'OPTIX'
scene.cycles.device = 'GPU'
scene.cycles.use_denoising = True
scene.cycles.denoising_use_gpu = True
scene.render.compositor_device = 'GPU'
scene.cycles.use_adaptive_sampling = True
scene.cycles.adaptive_threshold = 0.005
scene.render.threads_mode = 'FIXED'
scene.render.threads = 4
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
scene.render.image_settings.color_depth = '8'
scene.render.resolution_percentage = 100
scene.render.fps = 24
scene.render.fps_base = 1
scene.render.use_persistent_data = True
scene.frame_set(72)
bpy.context.view_layer.update()
points = [o.matrix_world @ v.co for o in scene.objects
          if o.type == 'MESH' and not o.hide_render for v in o.data.vertices]
underlight = next(o for o in scene.objects if o.name.startswith('STUDIO_Underside'))
records = []


def camera(name):
    obj = next(o for o in scene.objects if o.name.split('.')[0] == name)
    rotation = obj.matrix_world.to_quaternion()
    inv = rotation.inverted()
    local = [inv @ (p - obj.location) for p in points]
    lo = Vector((min(p.x for p in local), min(p.y for p in local), 0))
    hi = Vector((max(p.x for p in local), max(p.y for p in local), 0))
    obj.location += rotation @ ((lo + hi) * 0.5)
    obj.data.ortho_scale = max(hi.x - lo.x, (hi.y - lo.y) * 16 / 9) * 1.18
    scene.camera = obj


def render(name, frame, size, samples, underside=False):
    scene.frame_set(frame)
    underlight.hide_render = not underside
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.cycles.samples = samples
    destination = OUT / 'frames' if name.startswith('sequence-') else OUT
    destination.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(destination / (name + '.png'))
    if '--resume' not in sys.argv or not Path(scene.render.filepath).exists():
        bpy.ops.render.render(write_still=True)
    records.append(dict(file=str(Path(scene.render.filepath).relative_to(OUT)), frame=frame, resolution=size,
                        samples=samples, camera=scene.camera.name))
    print('PRESENTATION_RENDERED', name, flush=True)


if '--animation' not in sys.argv:
    for name, view in [('ship-front', 'VIEW_Front_ThreeQuarter'),
                       ('ship-rear', 'VIEW_Rear_ThreeQuarter'),
                       ('ship-underside', 'VIEW_Underside')]:
        camera(view)
        render(name, 72, (3840, 2160), 256, name == 'ship-underside')
    camera('VIEW_InteriorRear')
    for frame in (6, 28, 40, 46, 54, 62, 72):
        render('stage-%03d' % frame, frame, (3840, 2160), 192)
if '--stills' not in sys.argv:
    camera('VIEW_InteriorRear')
    for frame in range(1, 73):
        render('sequence-%03d' % frame, frame, (1920, 1080), 96)
assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
suffix = 'animation' if '--animation' in sys.argv else 'stills' if '--stills' in sys.argv else 'all'
(OUT / ('render-manifest-' + suffix + '.json')).write_text(json.dumps({
    'source': str(source), 'source_sha256': source_hash, 'source_unchanged': True,
    'revision': 'v13', 'engine': 'Cycles OptiX', 'view_transform': scene.view_settings.view_transform,
    'fps': 24, 'sequence_seconds': 3, 'records': records}, indent=2))
print('PRESENTATION_COMPLETE', flush=True)
