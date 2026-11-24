"""CPU review renders, source cutaways and two views of the inside-out preview."""
import json
import sys
from pathlib import Path
import bpy

HERE=Path(__file__).resolve().parent
source=bpy.context.scene
OUT=HERE.parent / 'reviews'/source['revision']
coll={k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}


def render(scene,name,camera,size=(1200,800),samples=20):
    bpy.context.window.scene=scene
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=samples
    scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=4
    scene.camera=next(o for o in scene.objects if o.name.split('.')[0]==camera)
    scene.render.resolution_x,scene.render.resolution_y=size;scene.render.resolution_percentage=100
    destination = OUT / 'frames' if name.rsplit('-', 1)[-1].isdigit() and 'stage' not in name else OUT
    destination.mkdir(parents=True, exist_ok=True)
    scene.render.filepath=str(destination/(name+'.png'));bpy.ops.render.render(write_still=True)
    print('ASSEMBLY_SAVED '+name,flush=True)


if '--rear-only' in sys.argv:
    preview=bpy.data.scenes[source['assembly_preview_scene']]
    for frame in (10,18,22,29,37,40):
        preview.frame_set(frame);render(preview,'assembly-stage-%02d'%frame,'VIEW_InteriorRear')
    for frame in range(1,41):
        preview.frame_set(frame);render(preview,'assembly-%03d'%frame,'VIEW_InteriorRear',(720,480),8)
    raise SystemExit(0)
coll['body'].hide_render=True;coll['lights'].hide_render=True
render(source,'interior','VIEW_Interior')
render(source,'interior-rear','VIEW_InteriorRear')
render(source,'cockpit','VIEW_Cockpit')
render(source,'interior-bottom','VIEW_Belly_Close')
coll['body'].hide_render=False;coll['lights'].hide_render=False
coll['interior'].hide_render=True
render(source,'exterior','VIEW_Rear_ThreeQuarter')
coll['interior'].hide_render=False
preview=bpy.data.scenes[source['assembly_preview_scene']]
for frame in (10,18,22,29,37,40):
    preview.frame_set(frame);render(preview,'assembly-stage-%02d'%frame,'VIEW_InteriorRear')
if '--stills' not in sys.argv:
    for camera,prefix in [('VIEW_InteriorRear','assembly'),('VIEW_Chase_Cruise','chase-assembly')]:
        for frame in range(1,41):
            preview.frame_set(frame)
            render(preview,'%s-%03d'%(prefix,frame),camera,(720,480),8)
print('ASSEMBLY_RENDER_COMPLETE',flush=True)
