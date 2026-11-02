"""Check source preservation, transforms and the active revision's phase contract."""
import hashlib
import json
import runpy
from pathlib import Path
import bpy

REVISION=globals().get('REVISION',bpy.context.scene.get('revision','v12'))
OUT=Path(__file__).resolve().parents[1] / 'reviews'/REVISION
report=json.loads((OUT/'luminous-preview.json').read_text())
scene=bpy.data.scenes[report['preview_scene']]


def signature(obj):
    data={'vertices':[list(v.co) for v in obj.data.vertices],
          'polygons':[(list(p.vertices),p.material_index) for p in obj.data.polygons]}
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()


source=bpy.data.scenes[report['source_scene']]
bpy.context.window.scene=source;bpy.context.view_layer.update()
coll={k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}
originals=[o for k in ('body','lights','interior') for o in coll[k].objects if o.type=='MESH']
source_transforms={o.name:o.matrix_world.copy() for o in originals+list(coll['studio'].objects)}
bpy.context.window.scene=scene;bpy.context.view_layer.update()
copies=[o for o in scene.objects if o.type=='MESH']
assert len(originals)==len(copies)==71
for original in originals:
    assert signature(original)==report['source_geometry_hashes'][original.name]
    copied=next(o for o in copies if o.get('asset_id')==original.get('asset_id'))
    assert signature(original)==signature(copied)
    assert max(abs(a-b) for ra,rb in zip(source_transforms[original.name],copied.matrix_world) for a,b in zip(ra,rb))<1e-6
for original in coll['studio'].objects:
    if original.type not in ('LIGHT','CAMERA') or original.name.startswith('VIEW_Turntable'):continue
    copied=next(o for o in scene.objects if o.name.split('.')[0]==original.name.split('.')[0])
    assert max(abs(a-b) for ra,rb in zip(source_transforms[original.name],copied.matrix_world) for a,b in zip(ra,rb))<1e-6
cues=report['cues']
assert min(c['start'] for c in cues if c['object'].startswith('Seed_Control'))==2
assert min(c['start'] for c in cues if c['phase']=='refinement')>=15
overlap=report.get('overlapping_materialization',False)
if overlap:assert max(c['complete'] for c in cues)>report['solid_start_seconds']*24+1
else:assert max(c['complete'] for c in cues)<report['solid_start_seconds']*24+1
assert scene.frame_end==72 and scene.render.fps==24
sample_frames=(1,6,18,34,50,59,72)
temporary=[o for o in scene.objects if o.get('phase') in ('guides','refinement','contours','solid_interior')]
for frame in sample_frames:
    scene.frame_set(frame)
    assert all(o.hide_render==o.hide_viewport for o in temporary)
scene.frame_set(72)
assert all(bpy.data.objects[c['object']].hide_render for c in cues)
assert all(o.hide_render for o in copies if o.get('phase')=='solid_interior')
assert all(not o.hide_render for o in copies if o.get('phase') in ('solid_body','solid_lights'))
# Arrival is capped; the last frame is past both the cutoff and its luminous transition.
body=next(o for o in copies if o.get('phase')=='solid_body')
front_counts={}
if overlap:
    fronts=runpy.run_path(str(Path(__file__).resolve().parent/'overlap_order.py'))['arrival_fronts']
    values=[fronts(v.co) for v in body.data.vertices]
    front_counts={'perimeter_first_vertices':sum(a<b for a,b in values),'center_first_vertices':sum(b<=a for a,b in values)}
    assert min(front_counts.values())>0
    assert min(min(a,b) for a,b in values)<1.7
    assert max(min(a,b) for a,b in values)>2.1
for mat in body.data.materials:
    assert any(n.type=='MATH' and n.operation=='MINIMUM' and abs(n.inputs[1].default_value-2.8)<1e-5
               for n in mat.node_tree.nodes)
result={'passed':True,'unchanged_source_and_copy_meshes':71,'curve_objects':len(cues),
        'materialization_overlaps_wire_refinement':overlap,'final_frame_temporary_geometry_hidden':True,
        'solid_reveal_settled_by_seconds':2.855,'fps':24,'frames':72,
        'viewport_visibility_matches_render_at_sample_frames':list(sample_frames),
        'source_and_studio_transforms_preserved':True,'competing_fronts':front_counts,
        'scope':'Geometry hashes and Blender phase contract; no Unity import or runtime verification'}
(OUT/'validation.json').write_text(json.dumps(result,indent=2)+'\n')
scene.frame_set(50)
print('LUMINOUS_VALIDATION',json.dumps(result),flush=True)
