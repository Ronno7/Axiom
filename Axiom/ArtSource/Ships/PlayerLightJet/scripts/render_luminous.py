"""Render the active luminous revision's stages and rear/chase sequences; never save the blend."""
import sys
from pathlib import Path
import bpy

revision=bpy.context.scene.get('revision','v12')
OUT=Path(__file__).resolve().parents[1] / 'reviews'/revision
scene=bpy.data.scenes['Swept Dart luminous assembly | '+revision]
bpy.context.window.scene=scene
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.use_denoising=True
scene.render.threads_mode='FIXED';scene.render.threads=4


def render(frame,camera,prefix,large=False):
    scene.frame_set(frame)
    scene.camera=next(o for o in scene.objects if o.name.split('.')[0]==camera)
    scene.render.resolution_x,scene.render.resolution_y=(1200,800) if large else (720,480)
    scene.cycles.samples=16 if large else 8
    destination = OUT if prefix == 'stage' else OUT / 'frames'
    destination.mkdir(parents=True, exist_ok=True)
    scene.render.filepath=str(destination/('%s-%03d.png'%(prefix,frame)))
    bpy.ops.render.render(write_still=True)
    print('LUMINOUS_RENDERED',prefix,frame,flush=True)


if '--animation' not in sys.argv:
    frames=(6,28,40,46,54,62,72) if revision=='v13' else (6,18,34,50,59,65,72)
    for frame in frames:render(frame,'VIEW_InteriorRear','stage',True)
if '--stills' not in sys.argv:
    for camera,prefix in [('VIEW_InteriorRear','assembly'),('VIEW_Chase_Cruise','chase')]:
        for frame in range(1,73):render(frame,camera,prefix)
print('LUMINOUS_RENDER_COMPLETE',flush=True)
