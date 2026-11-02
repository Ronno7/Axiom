"""Stage compact FBX meshes and UV data outside Assets, then verify a round trip.

Run in background Blender on the saved structural review. Does not save its input.
Outputs an editable export scene, two FBXs and a machine-readable validation report.
"""
import json
import math
import runpy
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
source = bpy.context.scene
revision = source['revision']
OUT = HERE.parent / 'reviews'/revision/'export'
OUT.mkdir(parents=True, exist_ok=True)
assert not (OUT/'export-manifest.json').exists(), 'Preserve verified review exports; use a new revision.'
coll = {k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}
has_interior='interior' in coll
assembly_order=runpy.run_path(str(HERE/'assembly_order.py'))['assembly_order'] if has_interior else None
coll['construction'].hide_viewport = False
bpy.context.view_layer.update()
deps = bpy.context.evaluated_depsgraph_get()
buckets = defaultdict(list)
for obj in coll['body'].objects:
    if obj.type=='MESH': buckets['Hull_'+obj['construction_group']].append(obj)
for obj in coll['lights'].objects:
    name=obj['asset_id']
    if name.startswith(('Shield_', 'Drive_Light_', 'Weapon_Aperture_')):
        bucket=name
    else:
        bucket='Permanent_'+obj['construction_group']
    buckets[bucket].append(obj)
for obj in coll['construction'].objects:
    buckets['Construction_'+obj['construction_group']].append(obj)
if has_interior:
    for obj in coll['interior'].objects:
        buckets['Interior_'+obj['construction_group']].append(obj)

# Evaluate in the source scene before creating an isolated compact export scene.
data={}
for name, objects in buckets.items():
    vertices=[]; faces=[]; material_indices=[]; materials=[]
    for obj in objects:
        ev=obj.evaluated_get(deps); m=ev.to_mesh()
        start=len(vertices)
        vertices.extend([tuple(obj.matrix_world@v.co) for v in m.vertices])
        slots=[]
        for mat in m.materials:
            if mat not in materials: materials.append(mat)
            slots.append(materials.index(mat))
        for p in m.polygons:
            faces.append(tuple(start+i for i in p.vertices))
            material_indices.append(slots[p.material_index] if slots else 0)
        ev.to_mesh_clear()
    group=objects[0]['construction_group']
    if has_interior and name.startswith('Construction_'):group='Trace'
    data[name]=(vertices,faces,material_indices,materials,group)
coll['construction'].hide_viewport = True
stage=bpy.data.scenes.new('Swept Dart export | '+revision)
bpy.context.window.scene=stage
stage.unit_settings.system='METRIC';stage.unit_settings.scale_length=1
root=bpy.data.objects.new('SweptDart',None);stage.collection.objects.link(root)
root['revision']=revision
root['axis_contract']='Blender -Y forward/+Z up; FBX baked -Z/+Y conversion -> numeric +Z forward/+Y up'
root['stage']='Staged structural study. Unity importer verification pending.'


def reveal(co, group):
    x, backward, up=co
    forward=-backward
    if has_interior:
        if group=='Trace':
            p=max(0,min(1,(2.5-forward)/4.14));return (.02+.16*p,p)
        if group=='Unified':return assembly_order(co,'Exterior')
        if group in ('Chassis','Cockpit','Services','Engine_L','Engine_R','Spar_L','Spar_R'):
            return assembly_order(co,group)
        return assembly_order(co,'Systems')
    if group=='Unified':
        # Continuous span weights avoid a reveal discontinuity at the welded roots.
        def smooth(a,b,x):
            t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
        core=max(0,min(1,(2.50-forward)/4.0))
        drive=max(0,min(1,(1.10-forward)/2.74))
        wing=max(0,min(1,(abs(x)-.30)/1.70))
        a=smooth(.25,.65,abs(x));b=smooth(1.0,1.4,abs(x))
        u=(.05+.30*core)*(1-a)+(.32+.46*drive)*a
        p=core*(1-a)+drive*a
        return (u*(1-b)+(.23+.52*wing)*b,p*(1-b)+wing*b)
    if group.startswith('Wing'):
        p=max(0,min(1,(abs(x)-.30)/1.70)); return (.23+.52*p,p)
    if group.startswith('Drive'):
        p=max(0,min(1,(.53-forward)/2.18)); return (.32+.46*p,p)
    if group.startswith('Weapon'):
        p=max(0,min(1,(1.10-forward)/.74)); return (.51+.22*p,p)
    p=max(0,min(1,(2.50-forward)/4.0)); return (.05+.30*p,p)


stats={}
objects=[]
for name,(vertices,faces,indices,materials,group) in data.items():
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    for material in materials:mesh.materials.append(material)
    for p,i in zip(mesh.polygons,indices):p.material_index=i
    obj=bpy.data.objects.new(name,mesh);stage.collection.objects.link(obj);obj.parent=root
    obj['asset_id']=name;obj['construction_group']=group
    obj['role']='interior_reveal' if name.startswith('Interior_') else ('construction_only' if name.startswith('Construction_') else (
        'shield_state' if name.startswith('Shield_') else (
        'exhaust_state' if name.startswith('Drive_Light_') else (
        'hull' if name.startswith('Hull_') else 'permanent_emission'))))
    # A separate layout per renderer is enough for this material-only study. It is
    # not a shared texture atlas or a promise of final lightmap density.
    for selected in bpy.context.selected_objects:selected.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    mesh.uv_layers.new(name='SurfaceUV')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012,
                             margin_method='FRACTION',correct_aspect=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    lightmap=mesh.uv_layers.new(name='LightmapReserved')
    for original,copy in zip(mesh.uv_layers[0].data,lightmap.data):copy.uv=original.uv
    uv=mesh.uv_layers.new(name='RevealOrder')
    for loop in mesh.loops:
        uv.data[loop.index].uv=reveal(mesh.vertices[loop.vertex_index].co,group)
    mesh.uv_layers.active_index=0
    mesh.calc_loop_triangles()
    assert all(math.isfinite(c) and -.00001<=c<=1.00001
               for layer in mesh.uv_layers for item in layer.data for c in item.uv),name
    stats[name]={'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),
                 'uv_layers':[u.name for u in mesh.uv_layers], 'group':group,
                 'role':obj['role'],'source_objects':[o['asset_id'] for o in buckets[name]]}
    objects.append(obj)

for original in source.objects:
    if original.type=='EMPTY' and 'unity_local_position' in original:
        obj=bpy.data.objects.new(original['asset_id'],None)
        stage.collection.objects.link(obj);obj.parent=root;obj.location=original.location
        obj['asset_id']=original['asset_id']
        obj['unity_local_position']=[float(v) for v in original['unity_local_position']]
        obj['forward_blender']=[0.0,-1.0,0.0];obj['up_blender']=[0.0,0.0,1.0]
        objects.append(obj)

bpy.context.view_layer.update()
base_world={o['asset_id']:[tuple(o.matrix_world@v.co) for v in o.data.vertices]
            for o in objects if o.type=='MESH'}
socket_world={o['asset_id']:tuple(o.matrix_world.translation) for o in objects if o.type=='EMPTY'}
for obj in objects:obj.select_set(True)
root.select_set(True)
# Save a separately editable export scene; original authoring file stays active in UI Blender.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SweptDart_export.blend'),copy=True)
common=dict(use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
            use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=False,
            use_triangles=True,bake_anim=False,use_custom_props=True,add_leaf_bones=False)
for obj in objects:obj.select_set(not obj.name.startswith(('Construction_','Interior_')))
bpy.ops.export_scene.fbx(filepath=str(OUT/'SweptDart.fbx'),**common)
for obj in objects:obj.select_set(obj.name.startswith('Construction_') or obj.type=='EMPTY')
bpy.ops.export_scene.fbx(filepath=str(OUT/'SweptDart_Construction.fbx'),**common)
if has_interior:
    for obj in objects:obj.select_set(obj.name.startswith('Interior_') or obj.type=='EMPTY')
    bpy.ops.export_scene.fbx(filepath=str(OUT/'SweptDart_Interior.fbx'),**common)

# Reimport into a clean temporary scene. Compare world-space bounds, socket positions,
# per-vertex reveal values and triangle totals; Blender assigns different display names.
check=bpy.data.scenes.new('FBX round trip | '+revision);bpy.context.window.scene=check
report={}
for filename in ('SweptDart.fbx','SweptDart_Construction.fbx')+(('SweptDart_Interior.fbx',) if has_interior else ()):
    before=set(check.objects)
    bpy.ops.import_scene.fbx(filepath=str(OUT/filename),use_custom_props=True)
    bpy.context.view_layer.update()
    imported=[o for o in check.objects if o not in before]
    for obj in imported:
        name=obj.get('asset_id',obj.name.split('.')[0])
        if obj.type=='MESH':
            expected=stats[name]
            obj.data.calc_loop_triangles()
            assert len(obj.data.loop_triangles)==expected['triangles'],name
            assert [u.name for u in obj.data.uv_layers]==expected['uv_layers'],name
            actual=[obj.matrix_world@v.co for v in obj.data.vertices]
            original=base_world[name]
            error=max(abs(fun(v[i] for v in actual)-fun(v[i] for v in original))
                      for i in range(3) for fun in (min,max))
            assert error<1e-5,(name,error)
            reveal_error=0
            for loop in obj.data.loops:
                world=obj.matrix_world@obj.data.vertices[loop.vertex_index].co
                want=reveal(world,expected['group'])
                got=obj.data.uv_layers['RevealOrder'].data[loop.index].uv
                reveal_error=max(reveal_error,max(abs(a-b) for a,b in zip(want,got)))
            assert reveal_error<1e-5,(name,reveal_error)
            report[name]={'bounds_error_metres':error,'reveal_error':reveal_error,
                          'triangles_preserved':True,'uv_channels_preserved':True}
        elif name in socket_world:
            error=(obj.matrix_world.translation-Vector(socket_world[name])).length
            assert error<1e-5,(name,error)
            report[name]={'position_error_metres':error}
expected_names=set(stats)|set(socket_world)
assert expected_names.issubset(report),expected_names-set(report)
manifest={'revision':revision,'meshes':stats,'round_trip':report,
          'surface_mesh_count':sum(not n.startswith(('Construction_','Interior_')) for n in stats),
          'interior_mesh_count':sum(n.startswith('Interior_') for n in stats),
          'construction_mesh_count':sum(n.startswith('Construction_') for n in stats),
          'hull_triangles':sum(v['triangles'] for n,v in stats.items() if n.startswith('Hull_')),
          'surface_triangles':sum(v['triangles'] for n,v in stats.items() if not n.startswith(('Construction_','Interior_'))),
          'interior_triangles':sum(v['triangles'] for n,v in stats.items() if n.startswith('Interior_')),
          'construction_triangles':sum(v['triangles'] for n,v in stats.items() if n.startswith('Construction_')),
          'uv_contract':{'0':'SurfaceUV: provisional smart projection per renderer',
                         '1':'LightmapReserved: copy of UV0; no lightmap bake',
                         '2':'RevealOrder: U normalized arrival; V normalized branch progress'},
          'verified':'Blender FBX round trip only; no Unity import, shader or runtime verification'}
(OUT/'export-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('EXPORT_VERIFIED',json.dumps({k:v for k,v in manifest.items() if k.endswith('_count') or k.endswith('_triangles')}))
