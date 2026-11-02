"""Luminous refinement: v12 late fill, v13 overlapping inward/outward growth.

Blender-only study. Reuses the v11 geometry without modifying it. No Unity exports.
Run in the source blend; saves are explicit, separate from generation.
"""
import hashlib
import json
import math
import runpy
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

HERE=Path(__file__).resolve().parent
REVISION=globals().get('REVISION','v12')
OVERLAP=REVISION=='v13'
if OVERLAP:surface_arrival=runpy.run_path(str(HERE/'overlap_order.py'))['surface_arrival']
SOURCE='Swept Dart | v11'
NAME='Swept Dart luminous assembly | '+REVISION
assert NAME not in bpy.data.scenes,'Preserve existing review scenes; choose a new revision'
source=bpy.data.scenes[SOURCE]
bpy.context.window.scene=source
bpy.context.view_layer.update()
coll={k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}


def signature(obj):
    data={'vertices':[list(v.co) for v in obj.data.vertices],
          'polygons':[(list(p.vertices),p.material_index) for p in obj.data.polygons]}
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()


before={o.name:signature(o) for k in ('body','lights','interior') for o in coll[k].objects if o.type=='MESH'}
scene=bpy.data.scenes.new(NAME);scene.world=source.world.copy()
scene.unit_settings.system='METRIC';scene.render.engine='CYCLES'
scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.cycles.transparent_max_bounces=48
scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=960;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.fps=24
scene.frame_start=1;scene.frame_end=72
scene.view_settings.view_transform=source.view_settings.view_transform
scene['revision']=REVISION;scene['source_scene']=SOURCE
scene['role']='Three-second luminous construction study; not a Unity shader'
groups={}
for key in ('guides','refinement','contours','solid','studio'):
    c=bpy.data.collections.new('LUMINOUS '+key+' | '+REVISION)
    scene.collection.children.link(c);groups[key]=c
scene['collections']=json.dumps({k:v.name for k,v in groups.items()})
cyan=next(m for o in coll['interior'].objects for m in o.data.materials if m.get('palette_token')=='Cyan')
COLOR=tuple(cyan.diffuse_color)
cues=[];copied=[]


def B(x,y,z):return Vector((x,-z,y))


def key(obj,path,value,frame):
    setattr(obj,path,value);obj.keyframe_insert(data_path=path,frame=frame)
    if path=='hide_render':
        obj.hide_viewport=value;obj.keyframe_insert(data_path='hide_viewport',frame=frame)


def socket_key(socket,value,frame):
    socket.default_value=value;socket.keyframe_insert('default_value',frame=frame)


def glow_material(name,start,finish,retire,seed=0,strength=3.):
    m=bpy.data.materials.new(name+' | '+REVISION);m.use_nodes=True
    m.diffuse_color=COLOR;m['palette_token']='Cyan'
    n=m.node_tree.nodes;n.clear();l=m.node_tree.links
    out=n.new('ShaderNodeOutputMaterial');em=n.new('ShaderNodeEmission')
    em.inputs['Color'].default_value=COLOR;l.new(em.outputs[0],out.inputs['Surface'])
    # Each part gets a short, offset brightness interruption during refinement.
    f=min(finish-1,start+3+seed%4)
    for frame,value in [(1,0),(start,1.0),(start+2,strength*1.9),(f,.25),
                        (f+1,strength*1.45),(finish,strength),(retire-3,strength*.8),(retire,0)]:
        socket_key(em.inputs['Strength'],value*4,frame)
    return m


def stroke(name,paths,start,finish,retire,group='guides',radius=.004,strength=3.,seed=0):
    if OVERLAP and group=='guides':retire=min(retire,finish+7)
    d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.resolution_u=1
    d.bevel_depth=radius;d.bevel_resolution=0;d.resolution_u=1
    for points in paths:
        sp=d.splines.new('POLY');sp.points.add(len(points)-1)
        for p,co in zip(sp.points,points):p.co=(*co,1)
    o=bpy.data.objects.new(name,d);groups[group].objects.link(o)
    d.materials.append(glow_material(name,start,finish,retire,seed,strength))
    key(d,'bevel_factor_end',0,1);key(d,'bevel_factor_end',0,start)
    key(d,'bevel_factor_end',1,finish)
    # A brief local retract/retrace, distinct from a global brightness flicker.
    if finish-start>5:
        key(d,'bevel_factor_end',.57,start+3);key(d,'bevel_factor_end',.42,start+4)
    key(o,'hide_render',True,1);key(o,'hide_render',False,start)
    key(o,'hide_render',True,retire)
    o['phase']=group;o['start_frame']=start;o['complete_frame']=finish;o['retire_frame']=retire
    cues.append({'object':o.name,'phase':group,'start':start,'complete':finish,'retire':retire,
                 'segments':sum(len(p)-1 for p in paths)})
    return o


def ring(center,rx,ry,plane='XY',n=24):
    x,y,z=center;points=[]
    for i in range(n+1):
        a=i*2*math.pi/n;u=rx*math.cos(a);v=ry*math.sin(a)
        points.append(B(x+u,y+v,z) if plane=='XY' else B(x+u,y,z+v))
    return points


# Small controls first. All positions use the existing source's Unity-local axes.
for i,sign in enumerate((-1,1)):
    stroke('Seed_Control_'+str(i),[ring((sign*.22,.06,.205),.052,.052)],2+i,9+i,30,seed=i,strength=4.)
    stroke('Guide_ControlRail_'+str(i),[[B(sign*.22,.06,.205),B(sign*.30,.01,-.25),B(sign*.34,0,-.85)]],
           7+i,17+i,43,seed=3+i)
    stroke('Guide_DriveConnection_'+str(i),[[B(sign*.22,.06,.205),B(sign*.48,.02,.14),B(sign*.85,.02,.14)]],
           9+i,19+i,44,seed=5+i)
    for j,z in enumerate((.20,-.28,-.78,-1.30)):
        stroke('Guide_DriveRing_%d_%d'%(i,j),[ring((sign*.85,.02,z),.145,.145)],
               12+j*3+i,20+j*3+i,46+j,seed=i*7+j)
    stroke('Guide_Wing_'+str(i),[[B(sign*.85,.02,-.78),B(sign*1.22,0,-.92),B(sign*1.82,0,-1.23)]],
           25+i,37+i,51,seed=20+i)
    for j,x in enumerate((1.20,1.55)):
        stroke('Guide_WingSection_%d_%d'%(i,j),[ring((sign*x,0,-.98),.07,.25,'XZ')],
               28+j*3+i,36+j*3+i,53,seed=24+j+i)
stroke('Guide_CockpitVolume',[ring((0,.05,-.08),.30,.70,'XZ')],7,20,40,seed=9)
stroke('Guide_Console',[ring((0,.10,.55),.15,.075,'XZ')],5,14,35,seed=10)

# Refinement uses the model's feature edges, excluding coplanar triangulation diagonals.
# Parts form at different times as propagation reaches their location from the controls.
for idx,original in enumerate(sorted(coll['interior'].objects,key=lambda o:o.name)):
    if original.type!='MESH':continue
    bm=bmesh.new();bm.from_mesh(original.data);bm.normal_update()
    paths=[]
    for e in bm.edges:
        if len(e.link_faces)!=2 or e.calc_face_angle(0)>.20:
            paths.append([original.matrix_world@v.co for v in e.verts])
    bm.free()
    center=sum((original.matrix_world@v.co for v in original.data.vertices),Vector())/len(original.data.vertices)
    group=original['construction_group'];forward=-center.y
    if group=='Cockpit':start=15+int(min(10,abs(forward-.205)*11))
    elif group.startswith('Engine'):start=22+int(max(0,.25-forward)*9)
    elif group.startswith('Spar'):start=34+int(max(0,abs(center.x)-1)*8)
    else:start=22+int(min(12,abs(forward-.205)*8))
    stroke('Resolve_'+original['asset_id'],paths,start,min(49,start+9),65,
           'refinement',.0018,2.4,idx)

# Approved external construction contours arrive after the interior's coarse volumes.
for idx,original in enumerate(coll['construction'].objects):
    if original.type!='CURVE':continue
    paths=[]
    for sp in original.data.splines:
        if sp.type=='BEZIER':points=[original.matrix_world@p.co for p in sp.bezier_points]
        else:points=[original.matrix_world@Vector(p.co[:3]) for p in sp.points]
        # Start each drawn contour at its end nearest the cockpit controls.
        origin=B(0,.06,.205)
        if (points[-1]-origin).length<(points[0]-origin).length:points.reverse()
        paths.append(points)
    stroke('Envelope_'+str(idx),paths,31+idx%7,47+idx%4,68,'contours',.0026,2.8,idx)


def solid_material(original,interior=False,systems=False,part_arrival=None):
    m=original.copy();m.name=original.name+' | luminous solid '+REVISION
    n=m.node_tree.nodes;l=m.node_tree.links
    out=n.get('Material Output');surface=n.get('Principled BSDF')
    p=n.new('ShaderNodeValue');p.name='BuildSeconds'
    p.outputs[0].driver_add('default_value').driver.expression='(frame-1)/24'
    def op(kind,a,b):
        node=n.new('ShaderNodeMath');node.operation=kind
        for i,value in enumerate((a,b)):
            if isinstance(value,(float,int)):node.inputs[i].default_value=value
            else:l.new(value,node.inputs[i])
        return node.outputs[0]
    pos=n.new('ShaderNodeTexCoord')
    if interior or systems:
        threshold=2.22 if interior else 2.88
        if OVERLAP and interior:threshold=part_arrival
        visible=op('GREATER_THAN',p.outputs[0],threshold)
        edge=op('LESS_THAN',p.outputs[0],threshold+.05)
    else:
        # Cellular boundary field: material starts at regional seams, then fills inward.
        # Object coordinates keep this attached to the hull, independent of camera motion.
        cell=n.new('ShaderNodeTexVoronoi');cell.feature='DISTANCE_TO_EDGE'
        cell.inputs['Scale'].default_value=28;l.new(pos.outputs['Object'],cell.inputs['Vector'])
        grain=n.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=150
        grain.inputs['Detail'].default_value=1;l.new(pos.outputs['Object'],grain.inputs['Vector'])
        attr=n.new('ShaderNodeAttribute');attr.attribute_name='RegionDelay'
        boundary=op('MULTIPLY',cell.outputs['Distance'],.70 if OVERLAP else 1.25)
        speckle=op('MULTIPLY',grain.outputs['Fac'],.11 if OVERLAP else .16)
        arrival=op('ADD',op('ADD',op('ADD',boundary,speckle),attr.outputs['Fac']),0 if OVERLAP else 2.18)
        arrival=op('MINIMUM',arrival,2.80)
        delta=op('SUBTRACT',p.outputs[0],arrival)
        visible=op('GREATER_THAN',delta,0)
        edge=op('LESS_THAN',delta,.055)
    glow=n.new('ShaderNodeEmission');glow.inputs['Color'].default_value=COLOR
    glow.inputs['Strength'].default_value=12.0
    finish=n.new('ShaderNodeMixShader');l.new(edge,finish.inputs[0])
    l.new(surface.outputs[0],finish.inputs[1]);l.new(glow.outputs[0],finish.inputs[2])
    transparent=n.new('ShaderNodeBsdfTransparent');mix=n.new('ShaderNodeMixShader')
    l.new(visible,mix.inputs[0]);l.new(transparent.outputs[0],mix.inputs[1]);l.new(finish.outputs[0],mix.inputs[2])
    for link in list(out.inputs['Surface'].links):l.remove(link)
    l.new(mix.outputs[0],out.inputs['Surface'])
    return m


for category in ('body','lights','interior','studio'):
    for original in coll[category].objects:
        if original.type=='EMPTY' or original.name.startswith('VIEW_Turntable'):continue
        o=original.copy();o.data=original.data.copy();o.animation_data_clear()
        o.parent=None;o.matrix_world=original.matrix_world.copy()
        groups['studio' if category=='studio' else 'solid'].objects.link(o)
        o.hide_viewport=False;o.hide_render=original.hide_render if category=='studio' else False
        if category=='studio':continue
        assert signature(original)==signature(o)
        copied.append((original.name,o.name))
        if category=='body':
            a=o.data.attributes.new('RegionDelay','FLOAT','POINT')
            for v in o.data.vertices:
                x,back,up=v.co
                forward=-back
                inset=max(0,min(1,(2-abs(x))/1.2,(2.5-forward)/.8,(forward+1.64)/.5))
                a.data[v.index].value=surface_arrival(v.co) if OVERLAP else .15*inset+.05*max(0,min(1,(up+.10)/.5))
        part_arrival=None
        if OVERLAP and category=='interior':
            cue=next(c for c in cues if c['object'].split('.')[0]=='Resolve_'+original['asset_id'])
            part_arrival=(cue['complete']-1)/24+.04
            o['solid_arrival_seconds']=part_arrival
        for i,m in enumerate(list(o.data.materials)):
            o.data.materials[i]=solid_material(m,category=='interior',category=='lights',part_arrival)
        if category=='interior':key(o,'hide_render',False,70);key(o,'hide_render',True,71)
        o['phase']='solid_'+category

# Gentle bloom on emissive guides; regular white hull stays non-emissive.
comp=bpy.data.node_groups.new('Luminous preview glow | '+REVISION,'CompositorNodeTree')
comp.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
nodes=comp.nodes;links=comp.links
layers=nodes.new('CompositorNodeRLayers');layers.scene=scene
glare=nodes.new('CompositorNodeGlare');glare.inputs['Type'].default_value='Fog Glow'
glare.inputs['Threshold'].default_value=.3;glare.inputs['Strength'].default_value=1.0
glare.inputs['Size'].default_value=.35
output=nodes.new('NodeGroupOutput');links.new(layers.outputs['Image'],glare.inputs['Image'])
links.new(glare.outputs['Image'],output.inputs['Image']);scene.compositing_node_group=comp
scene.camera=next(o for o in groups['studio'].objects if o.name.split('.')[0]=='VIEW_InteriorRear')
markers=([(1,'Empty'),(3,'Controls seed'),(18,'Primitive volumes'),(28,'Parts resolve locally'),
          (37,'Edges inward and center outward'),(50,'Overlapping construction'),(64,'Skin consolidates'),(72,'Flight exterior')]
         if OVERLAP else [(1,'Empty'),(3,'Controls seed'),(18,'Primitive volumes'),(34,'Mechanical refinement'),
                         (50,'Luminous framework'),(54,'Matter from seams'),(64,'Skin consolidates'),(72,'Flight exterior')])
for f,label in markers:
    scene.timeline_markers.new(label,frame=f)
scene.frame_set(50)
assert all(signature(bpy.data.objects[name])==sig for name,sig in before.items())
assert all(signature(bpy.data.objects[a])==signature(bpy.data.objects[b]) for a,b in copied)
out=HERE.parent / 'reviews'/REVISION;out.mkdir(exist_ok=True,parents=True)
report={'revision':REVISION,'source_scene':SOURCE,'preview_scene':NAME,'frames':72,'fps':24,
        'duration_seconds':3,'geometry_unchanged':True,'copied_meshes':len(copied),
        'source_geometry_hashes':before,'cues':cues,
        'solid_start_seconds':1.40 if OVERLAP else 2.18,'final_systems_seconds':2.88,
        'overlapping_materialization':OVERLAP,
        'solid_boundary_method':('Competing perimeter-inward and control-center-outward fronts with fine static grain; clamped to settle by 2.855 s' if OVERLAP else
                                 'Fine object-space cellular edges plus static grain and delay from perimeter into upper hull; clamped to settle by 2.855 s'),
        'notes':'Blender study only. Generated regional seams are not final authored panel seams. No new FBX or Unity shader.'}
(out/'luminous-preview.json').write_text(json.dumps(report,indent=2)+'\n')
for file in ('build_luminous_preview.py','prepare_luminous_review.py','overlap_order.py','validate_luminous.py',
             'render_luminous.py','compose_luminous.py'):
    p=HERE/file
    if p.exists():t=bpy.data.texts.new(file+' | '+REVISION);t.write(p.read_text(encoding='utf8'))
bpy.context.window.scene=scene
print('LUMINOUS_BUILT',len(cues),'curve objects;',len(copied),'unchanged mesh copies',flush=True)
