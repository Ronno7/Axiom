"""Render review views from a saved blockout, without saving scene changes.

blender --background AxiomPlayerShip.blend --python render_review.py -- --quick
Omit --quick for all views and the 24-frame turntable (frames 1,5,...93).
Uses four CPU threads so concurrent game work keeps its GPU and most CPU capacity.
"""
import json
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
scene = bpy.context.scene
revision = scene['revision']
OUT = HERE.parent / 'reviews' / revision
OUT.mkdir(parents=True, exist_ok=True)
coll = {k: bpy.data.collections[v] for k, v in json.loads(scene['collections']).items()}
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.render.threads_mode = 'FIXED'
scene.render.threads = 4
scene.cycles.samples = 24
scene.cycles.use_denoising = True


def render(name, camera, size=(1200, 800), override=None):
    scene.camera = next(o for o in scene.objects if o.name.split('.')[0] == camera)
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.view_layers[0].material_override = override
    scene.render.filepath = str(OUT / (name + '.png'))
    bpy.ops.render.render(write_still=True)
    print('REVIEW_SAVED ' + name, flush=True)


underlight = next((o for o in scene.objects if o.name.startswith('STUDIO_Underside')), None)
if '--structure-quick' in sys.argv:
    underlight.hide_render = False
    render('underside', 'VIEW_Underside')
    render('bottom', 'VIEW_Bottom')
    render('belly-close', 'VIEW_Belly_Close')
    raise SystemExit(0)
render('front', 'VIEW_Front_ThreeQuarter')
if '--quick' in sys.argv:
    raise SystemExit(0)
for name, camera in [('rear-three-quarter', 'VIEW_Rear_ThreeQuarter'),
                     ('top', 'VIEW_Top'), ('side', 'VIEW_Side'),
                     ('underside', 'VIEW_Underside'), ('rear', 'VIEW_Rear')]:
    if underlight: underlight.hide_render = name != 'underside'
    render(name, camera)
    if underlight: underlight.hide_render = True
clay = bpy.data.materials.get('Clay inspection | ' + revision)
render('clay', 'VIEW_Front_ThreeQuarter', override=clay)
if any(o.name.startswith('VIEW_Junction') for o in scene.objects):
    render('junction', 'VIEW_Junction')
    render('junction-clay', 'VIEW_Junction', override=clay)
if 'construction' in coll:
    underlight.hide_render = False
    render('bottom', 'VIEW_Bottom')
    render('belly-rear', 'VIEW_Belly_Rear')
    render('belly-close', 'VIEW_Belly_Close')
    render('belly-clay', 'VIEW_Underside', override=clay)
    underlight.hide_render = True
    coll['body'].hide_render = True
    coll['lights'].hide_render = True
    coll['construction'].hide_render = False
    render('construction-lines', 'VIEW_Construction')
    coll['body'].hide_render = False
    render('construction-overlay', 'VIEW_Construction')
    coll['construction'].hide_render = True
    coll['lights'].hide_render = False
coll['guides'].hide_render = False
render('collider', 'VIEW_Front_ThreeQuarter')
coll['guides'].hide_render = True
render('chase-new', 'VIEW_Chase_Cruise', (1280, 720))
render('chase-boost-fov', 'VIEW_Chase_Boost', (1280, 720))
coll['body'].hide_render = True
coll['lights'].hide_render = True
coll['reference'].hide_render = False
render('chase-placeholder', 'VIEW_Chase_Cruise', (1280, 720))
render('top-placeholder', 'VIEW_Top')
coll['body'].hide_render = False
coll['lights'].hide_render = False
coll['reference'].hide_render = True
scene.cycles.samples = 8
for frame in range(1, 97, 4):
    scene.frame_set(frame)
    render('turntable-%03d' % frame, 'VIEW_Turntable', (640, 432))
print('REVIEW_COMPLETE', flush=True)
