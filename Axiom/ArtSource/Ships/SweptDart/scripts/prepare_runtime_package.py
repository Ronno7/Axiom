"""Stage v14 runtime geometry/data from the accepted v13 study, outside Assets.

Run in background Blender on reviews/v13/SweptDart_v13.blend. Never saves input.
v14 is packaging, not a silhouette or choreography revision.
"""
import hashlib
import json
import math
import sys
from collections import defaultdict
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / 'reviews/v14/export'
OUT.mkdir(parents=True, exist_ok=True)
assert not (OUT/'validation.json').exists() or '--rebuild' in sys.argv, 'Use --rebuild only for this staging revision; preserve approved revisions.'
INPUT = Path(bpy.data.filepath)
INPUT_HASH = hashlib.sha256(INPUT.read_bytes()).hexdigest()
source = bpy.data.scenes['Swept Dart | v11']
preview = bpy.data.scenes['Swept Dart luminous assembly | v13']
collections = {k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}
originals = [o for k in ('body','lights','interior') for o in collections[k].objects if o.type=='MESH']
copies = {o['asset_id']:o for o in preview.objects if o.type=='MESH' and o.get('phase')}
curves = sorted([o for o in preview.objects if o.type=='CURVE'], key=lambda o:o.name)
assert len(curves)==91

def signature(o):
    return hashlib.sha256(json.dumps({'vertices':[list(v.co) for v in o.data.vertices],
        'polygons':[(list(p.vertices),p.material_index) for p in o.data.polygons]},sort_keys=True).encode()).hexdigest()

before = {o.name:signature(o) for o in originals}
approved = json.loads((HERE.parent/'reviews/v13/luminous-preview.json').read_text())
assert before == approved['source_geometry_hashes']

# Sample evaluated Bezier animation, including brightness dips and retract/retrace.
# One row per trace; time is normalized externally, never baked into vertex positions.
SAMPLES, ROWS, HZ = 289, 128, 96
pixels = np.zeros((ROWS,SAMPLES,4), dtype=np.float32)
cue_records = []
bpy.context.window.scene = preview
for row,o in enumerate(curves):
    cue_records.append(dict(id=o.name.split('.')[0],row=row,phase=o['phase'],
        start_seconds=(o['start_frame']-1)/24,complete_seconds=(o['complete_frame']-1)/24,
        retire_seconds=(o['retire_frame']-1)/24,radius_metres=o.data.bevel_depth,
        paths=[[list(p.co[:3]) for p in sp.points] for sp in o.data.splines]))
for sample in range(SAMPLES):
    frame=1+sample/4
    preview.frame_set(int(frame), subframe=frame-int(frame))
    for row,o in enumerate(curves):
        strength=next(n for n in o.data.materials[0].node_tree.nodes if n.type=='EMISSION').inputs['Strength'].default_value
        pixels[row,sample]=[o.data.bevel_factor_end,max(0,strength),float(not o.hide_render),1]
assert not pixels[:91,-1,2].any()

exr_settings=bpy.data.scenes.new('Runtime data image settings')
exr_settings.render.image_settings.file_format='OPEN_EXR'
exr_settings.render.image_settings.color_mode='RGBA'
exr_settings.render.image_settings.color_depth='32'
exr_settings.render.image_settings.exr_codec='ZIP'
exr_settings.view_settings.view_transform='Raw'

def save_exr(name, values):
    h,w,_=values.shape
    image=bpy.data.images.new(name,width=w,height=h,alpha=True,float_buffer=True)
    image.colorspace_settings.name='Non-Color'
    image.pixels.foreach_set(values.ravel())
    image.filepath_raw=str(OUT/name);image.file_format='OPEN_EXR'
    image.save_render(str(OUT/name),scene=exr_settings)
    return image

lut = save_exr('TraceTimeline.exr',pixels)
timeline = dict(duration_seconds=3,sample_rate=HZ,width=SAMPLES,height=ROWS,
    channels={'R':'draw fraction per spline, RESOLUTION mapping','G':'linear Cyan emission strength',
              'B':'visibility (sample lower texel; do not interpolate)','A':'valid row'},
    interpolation='Interpolate R/G between adjacent time texels; row and B use point samples.',
    curves=cue_records)
(OUT/'timeline.json').write_text(json.dumps(timeline,indent=2)+'\n',encoding='utf8')

stage=bpy.data.scenes.new('Swept Dart runtime package | v14')
bpy.context.window.scene=stage
stage.unit_settings.system='METRIC';stage.unit_settings.scale_length=1
stage.render.fps=24;stage.frame_start=1;stage.frame_end=72
root=bpy.data.objects.new('SweptDart',None);stage.collection.objects.link(root)
root['asset_id']='SweptDart';root['revision']='v14';root['art_revision']='v13'
root['axis_contract']='Metres; Blender -Y forward/+Z up; baked FBX axes; verify Unity +Z/+Y.'
objects=[];materials={};material_records={};buckets=defaultdict(list);source_members={}

def material(original,mode):
    token=original['palette_token'];p=original.node_tree.nodes.get('Principled BSDF')
    role=original.name.split(' | ')[0].replace(' ','_')
    name=f'{mode}_{role}_{token}'
    if name not in materials:
        m=original.copy();m.name=name;materials[name]=m
        material_records[name]=dict(palette_token=token,mode=mode,
            base_linear=list(original.diffuse_color),emission_strength=p.inputs['Emission Strength'].default_value,
            metallic=p.inputs['Metallic'].default_value,roughness=p.inputs['Roughness'].default_value,
            shader='GeometricFlight/Swept Dart Surface',arrival_texture='HullArrival.exr' if mode=='Hull' else None,
            reveal_mode=1 if mode=='Hull' else 0,retire_seconds=70/24 if mode=='Interior' else 4,
            front_seconds=.055 if mode=='Hull' else .05)
    return materials[name]

for category in ('body','lights','interior'):
    for o in collections[category].objects:
        if o.type!='MESH':continue
        asset=o['asset_id'];group=o['construction_group']
        if category=='body':name='Hull_'+group
        elif category=='interior':name='Interior_'+group
        elif asset.startswith(('Shield_','Drive_Light_','Weapon_Aperture_')):name=asset
        else:name='Permanent_'+group
        buckets[name].append(o)

def make_object(name,verts,faces,indices,mats,arrival,progress,identity=None):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    for m in mats:mesh.materials.append(m)
    for p,i in zip(mesh.polygons,indices):p.material_index=i;p.use_smooth=False
    obj=bpy.data.objects.new(name,mesh);stage.collection.objects.link(obj);obj.parent=root
    obj['asset_id']=name
    obj['role']='construction_only' if name=='Construction_Traces' else 'interior_reveal' if name.startswith('Interior_') else 'shield_state' if name.startswith('Shield_') else 'hull' if name.startswith('Hull_') else 'permanent_emission'
    obj['construction_group']='Trace' if name=='Construction_Traces' else buckets[name][0]['construction_group']
    for sel in bpy.context.selected_objects:sel.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    mesh.uv_layers.new(name='SurfaceUV')
    if name!='Construction_Traces':
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.018,margin_method='FRACTION',correct_aspect=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    reserve=mesh.uv_layers.new(name='LightmapReserved')
    for a,b in zip(mesh.uv_layers[0].data,reserve.data):b.uv=a.uv
    reveal=mesh.uv_layers.new(name='RevealOrder')
    trace=mesh.uv_layers.new(name='TraceIdentity')
    for loop in mesh.loops:
        v=loop.vertex_index
        reveal.data[loop.index].uv=(arrival[v]/3,progress[v])
        trace.data[loop.index].uv=((identity[v]+.5)/ROWS,0) if identity is not None else (0,0)
    mesh.uv_layers.active_index=0
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='FIXED',ngon_method='EAR_CLIP')
    bm.to_mesh(mesh);bm.free();mesh.update()
    objects.append(obj)
    return obj

for name,members in buckets.items():
    verts=[];faces=[];indices=[];mats=[];arrival=[];progress=[];delays=[]
    mode='Interior' if name.startswith('Interior_') else 'Hull' if name.startswith('Hull_') else 'Systems'
    source_members[name]=[o['asset_id'] for o in members]
    for o in members:
        offset=len(verts);verts.extend(tuple(o.matrix_world@v.co) for v in o.data.vertices)
        slots=[]
        for m in o.data.materials:
            new=material(m,mode)
            if new not in mats:mats.append(new)
            slots.append(mats.index(new))
        for p in o.data.polygons:
            faces.append(tuple(offset+i for i in p.vertices));indices.append(slots[p.material_index])
        copy=copies[o['asset_id']]
        values=[a.value for a in copy.data.attributes['RegionDelay'].data] if mode=='Hull' else [copy['solid_arrival_seconds'] if mode=='Interior' else 2.88]*len(o.data.vertices)
        delays.extend(values);arrival.extend(values)
        progress.extend([max(0,min(1,(-v.co.y+1.64)/4.14)) for v in o.data.vertices])
    obj=make_object(name,verts,faces,indices,mats,arrival,progress)
    if mode=='Hull':
        attr=obj.data.attributes.new('RegionDelay','FLOAT','POINT')
        for a,value in zip(attr.data,delays):a.value=value
        assert len(verts)==784 and len(faces)==1564

# A single four-sided tube mesh carries all 91 independently timed traces.
# The approved path centres and radii are retained; no line renderer objects required.
verts=[];faces=[];arrival=[];progress=[];identity=[]
for row,o in enumerate(curves):
    for spline in o.data.splines:
        pts=[o.matrix_world@Vector(p.co[:3]) for p in spline.points]
        assert len(pts)>1 and spline.type=='POLY'
        offset=len(verts);side=None
        for i,p in enumerate(pts):
            tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
            if side is None or abs(side.dot(tangent))>.99:
                axis=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((1,0,0))
                side=tangent.cross(axis).normalized()
            else:side=(side-tangent*side.dot(tangent)).normalized()
            up=tangent.cross(side).normalized()
            for j in range(4):
                angle=j*math.pi/2
                verts.append(tuple(p+o.data.bevel_depth*(side*math.cos(angle)+up*math.sin(angle))))
                t=i/(len(pts)-1);progress.append(t);identity.append(row)
                arrival.append((o['start_frame']-1+(o['complete_frame']-o['start_frame'])*t)/24)
        for i in range(len(pts)-1):
            for j in range(4):
                a=offset+i*4+j;b=offset+i*4+(j+1)%4
                faces.append((a,b,b+4,a+4))
trace_mat=bpy.data.materials.new('Construction_Cyan');trace_mat.diffuse_color=next(m.diffuse_color[:] for m in materials.values() if m['palette_token']=='Cyan')
trace_mat['palette_token']='Cyan';trace_mat.use_nodes=True
make_object('Construction_Traces',verts,faces,[0]*len(faces),[trace_mat],arrival,progress,identity)
material_records[trace_mat.name]=dict(palette_token='Cyan',mode='Trace',base_linear=list(trace_mat.diffuse_color),
    shader='GeometricFlight/Swept Dart Trace',timeline_texture='TraceTimeline.exr')

# Only the surface file owns sockets. Auxiliary files contain a root and their meshes.
sockets=[]
for o in source.objects:
    if o.type=='EMPTY' and 'unity_local_position' in o:
        n=bpy.data.objects.new(o['asset_id'],None);stage.collection.objects.link(n);n.parent=root
        n.location=o.matrix_world.translation;n['asset_id']=o['asset_id']
        n['unity_local_position']=list(o['unity_local_position']);sockets.append(n)

# Bake the exact v13 procedural arrival field, including cellular boundaries and grain.
hull=next(o for o in objects if o['role']=='hull')
preview_hull=next(o for o in preview.objects if o.get('phase')=='solid_body')
bake_mat=preview_hull.data.materials[0].copy();bake_mat.name='BAKE_Arrival'
nodes=bake_mat.node_tree.nodes;links=bake_mat.node_tree.links
cap=next(n for n in nodes if n.type=='MATH' and n.operation=='MINIMUM' and abs(n.inputs[1].default_value-2.8)<1e-4)
normalize=nodes.new('ShaderNodeMath');normalize.operation='DIVIDE';normalize.inputs[1].default_value=3
links.new(cap.outputs[0],normalize.inputs[0])
em=nodes.new('ShaderNodeEmission');em.inputs['Strength'].default_value=1
links.new(normalize.outputs[0],em.inputs['Color'])
out=nodes.get('Material Output')
for link in list(out.inputs['Surface'].links):links.remove(link)
links.new(em.outputs[0],out.inputs['Surface'])
arrival_image=bpy.data.images.new('HullArrival.exr',width=2048,height=2048,alpha=True,float_buffer=True)
arrival_image.colorspace_settings.name='Non-Color'
tex=nodes.new('ShaderNodeTexImage');tex.image=arrival_image;nodes.active=tex
saved_mats=list(hull.data.materials)
for i in range(len(saved_mats)):hull.data.materials[i]=bake_mat
for o in stage.objects:o.select_set(False)
hull.select_set(True);bpy.context.view_layer.objects.active=hull
stage.render.engine='CYCLES';stage.cycles.samples=1;stage.render.threads_mode='FIXED';stage.render.threads=4
stage.render.bake.margin=16;stage.render.bake.use_clear=True
print('BAKING_ARRIVAL',flush=True)
bpy.ops.object.bake(type='EMIT')
arrival_image.filepath_raw=str(OUT/'HullArrival.exr');arrival_image.file_format='OPEN_EXR'
arrival_image.save_render(str(OUT/'HullArrival.exr'),scene=exr_settings)
for i,m in enumerate(saved_mats):hull.data.materials[i]=m
arrival_pixels=np.empty(2048*2048*4,dtype=np.float32);arrival_image.pixels.foreach_get(arrival_pixels)
arrival_pixels=arrival_pixels.reshape((-1,4));covered=arrival_pixels[:,3]>.5
assert covered.any() and np.isfinite(arrival_pixels).all()
valid=arrival_pixels[covered,0];assert valid.max()<=2.8/3+1e-5 and valid.max()>.7

def mesh_record(o):
    m=o.data;m.calc_loop_triangles()
    return dict(vertices=len(m.vertices),triangles=len(m.loop_triangles),submeshes=len(m.materials),
        materials=[v.name for v in m.materials],role=o['role'],group=o['construction_group'],
        uv_layers=[u.name for u in m.uv_layers],source_objects=source_members.get(o['asset_id'],[c['id'] for c in cue_records]))

stats={o['asset_id']:mesh_record(o) for o in objects}
assert sum(s['triangles'] for s in stats.values() if s['role'] not in ('construction_only','interior_reveal'))==3484
assert sum(s['triangles'] for s in stats.values() if s['role']=='interior_reveal')==2132
surface=[o for o in objects if o['role'] not in ('construction_only','interior_reveal')]
interior=[o for o in objects if o['role']=='interior_reveal']
traces=[o for o in objects if o['role']=='construction_only']
files={'SweptDart.fbx':surface+sockets,'SweptDart_Interior.fbx':interior,'SweptDart_Construction.fbx':traces}
common=dict(use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_space_transform=True,
    bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,bake_anim=False,
    use_custom_props=True,add_leaf_bones=False,path_mode='STRIP',embed_textures=False)
for filename,selection in files.items():
    for o in stage.objects:o.select_set(o in selection or o==root)
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),**common)

bpy.context.view_layer.update()
for o in stage.objects:o.select_set(False)
for img in (lut,arrival_image):img.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SweptDart_export.blend'),copy=True)

# FBX verification compares actual triangles with UVs/materials, not just counts or bounds.
def triangle_signatures(obj):
    m=obj.data;m.calc_loop_triangles();result=[]
    for tri in m.loop_triangles:
        corners=[]
        for li in tri.loops:
            v=obj.matrix_world@m.vertices[m.loops[li].vertex_index].co
            uv=[c for layer in m.uv_layers for c in layer.data[li].uv]
            corners.append(tuple(float(x) for x in (*v,*uv)))
        result.append((tri.material_index,corners))
    return result

def compare_triangles(expected,actual):
    assert len(expected)==len(actual)
    tree=KDTree(len(expected))
    for i,(_,corners) in enumerate(expected):
        tree.insert(tuple(sum(c[a] for c in corners)/3 for a in range(3)),i)
    tree.balance();used=set();maximum=0
    for mat,corners in actual:
        center=tuple(sum(c[a] for c in corners)/3 for a in range(3))
        best=(float('inf'),None)
        for _,i,_ in tree.find_range(center,1e-4):
            if i in used or expected[i][0]!=mat:continue
            want=expected[i][1]
            error=min(max(abs(a-b) for j in range(3) for a,b in zip(want[j],corners[(j+shift)%3])) for shift in range(3))
            if error<best[0]:best=(error,i)
        assert best[0]<1e-5,('Triangle position/UV/winding mismatch',best)
        used.add(best[1]);maximum=max(maximum,best[0])
    return maximum

expected={o['asset_id']:triangle_signatures(o) for o in objects}
check=bpy.data.scenes.new('Runtime FBX verification');bpy.context.window.scene=check
checked={}
for filename,selection in files.items():
    old=set(check.objects)
    bpy.ops.import_scene.fbx(filepath=str(OUT/filename),use_custom_props=True)
    bpy.context.view_layer.update()
    imported=[o for o in check.objects if o not in old]
    meshes=[o for o in imported if o.type=='MESH']
    assert {o['asset_id'] for o in meshes}=={o['asset_id'] for o in selection if o.type=='MESH'}
    for o in meshes:
        name=o['asset_id'];assert [u.name for u in o.data.uv_layers]==stats[name]['uv_layers']
        actual_triangles=triangle_signatures(o)
        checked[name]=compare_triangles(expected[name],actual_triangles)
    for sock in sockets if filename=='SweptDart.fbx' else []:
        imported_sock=next(o for o in imported if o.get('asset_id')==sock['asset_id'])
        assert (imported_sock.matrix_world.translation-sock.matrix_world.translation).length<1e-5
assert len(checked)==len(objects)
assert all(signature(o)==before[o.name] for o in originals)
assert hashlib.sha256(INPUT.read_bytes()).hexdigest()==INPUT_HASH

# Verify EXR round trips numerically. Float data must never be treated as sRGB.
loaded=bpy.data.images.load(str(OUT/'TraceTimeline.exr'),check_existing=False)
actual=np.empty(pixels.size,dtype=np.float32);loaded.pixels.foreach_get(actual)
lut_error=float(np.max(np.abs(actual.reshape(pixels.shape)-pixels)))
assert lut_error<.02,lut_error

manifest=dict(revision='v14',art_revision='v13',source_sha256=INPUT_HASH,
    source_geometry_preserved=True,meshes=stats,materials=material_records,
    sockets=[dict(name=o['asset_id'],unity_local_position=list(o['unity_local_position']),
        unity_local_forward=[0,0,1],unity_local_up=[0,1,0]) for o in sockets],
    uv_contract={'0':'SurfaceUV; unique hull bake layout','1':'LightmapReserved; unused copy of UV0',
        '2':'RevealOrder: U arrival seconds/3; V branch progress','3':'TraceIdentity: U row centre in 128-row timeline; V reserved'},
    texture_contract={'HullArrival.exr':{'size':[2048,2048],'channel':'R arrival seconds/3','filter':'Bilinear'},
        'TraceTimeline.exr':{'size':[SAMPLES,ROWS],'channel':'RGBA as timeline.json','filter':'Point'},
        'common':{'sRGB':False,'mipmaps':False,'compression':'None','format':'RGBAHalf or RGBAFloat','wrap':'Clamp','npot_scale':'None'}},
    shield=dict(segments=['Shield_Segment_1','Shield_Segment_2','Shield_Segment_3'],depletion_order=['Shield_Segment_3','Shield_Segment_2','Shield_Segment_1'],
        lit_token='Light',lit_emission=2.5,lost_token='Edge',lost_emission=0,
        blink_renderers=[o['asset_id'] for o in surface],driver='Existing ShieldDisplay; no new gameplay logic'),
    timing=dict(duration=3,interior_retire=70/24,surface_settled=2.855,systems_arrive=2.88,
        shader_clock='Normalized _BuildProgress multiplied by 3; entry duration remains externally controlled'),
    counts=dict(surface_meshes=len(surface),interior_meshes=len(interior),trace_meshes=len(traces),
        flight_triangles=3484,interior_triangles=2132,trace_triangles=stats['Construction_Traces']['triangles'],
        flight_submeshes=sum(len(o.data.materials) for o in surface),
        assembly_submeshes=sum(len(o.data.materials) for o in objects)),
    verification='Blender and data checks only. Unity import, shader compilation, palette audit, moving-camera review and runtime profiling deferred.')
(OUT/'package-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')
(OUT/'validation.json').write_text(json.dumps(dict(passed=True,fbx_meshes_verified=len(checked),
    triangle_uv_material_winding_round_trip=True,sockets_verified=len(sockets),source_file_unchanged=True,
    source_meshes_unchanged=len(before),trace_rows=91,lut_max_round_trip_error=lut_error,
    hull_arrival_max_seconds=float(valid.max()*3),hull_bake_nonzero_pixels=int((arrival_pixels[:,0]>0).sum()),
    scope=manifest['verification']),indent=2)+'\n',encoding='utf8')
print('RUNTIME_PACKAGE_VERIFIED',json.dumps(manifest['counts']),flush=True)
