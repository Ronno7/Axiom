"""Build a continuous, welded Swept Dart skin with intrinsic recessed details.

The old blockout supplies only studio/reference infrastructure. Its newly generated
mesh/strip objects are replaced in this new revision; historical scenes are untouched.
All dimensions are Unity-local metres until B() converts them to Blender coordinates.
"""
import json
import math
import runpy
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import tessellate_polygon

HERE=Path(__file__).resolve().parent
REVISION=globals().get('REVISION','v10')
base=runpy.run_path(str(HERE/'build_blockout.py'),init_globals={'REVISION':REVISION})
for key in ('scene','body','lights','sockets','studio','root','B','mesh','tube',
            'material','collection','camera','hull','facet','edge','inset','cyan','white'):
    globals()[key]=base[key]
for coll in (body,lights):
    for obj in list(coll.objects):
        if obj is not root:
            bpy.data.objects.remove(obj,do_unlink=True)
for mat,token,metal,rough in [(hull,'Light',.05,.36),(facet,'Text',.08,.42)]:
    color=base['rgba'](token);mat.diffuse_color=color;mat['palette_token']=token
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    for field in ('Base Color','Emission Color'):bsdf.inputs[field].default_value=color
    bsdf.inputs['Emission Strength'].default_value=0
    bsdf.inputs['Metallic'].default_value=metal;bsdf.inputs['Roughness'].default_value=rough
carrier=material('Shield carrier','Structure',.1,.43)
construction_mat=material('Construction only','Cyan',0,.5,1.1)
construction=collection('07 Construction curves')
construction.hide_render=True;construction.hide_viewport=True

# Shared span stations define the silhouette, upper mass and underside keel together.
# x, front, back, crest height, belly depth (negative). Gun/drive rows retain the sockets.
stations=[(0,2.50,-1.44,.51,-.28),(.18,1.70,-1.44,.48,-.275),
          (.34,1.14,-1.44,.34,-.23),(.50,.88,-1.48,.23,-.18),
          (.60,.96,-1.57,.24,-.18),(.70,1.10,-1.64,.34,-.22),
          (.85,1.10,-1.64,.36,-.23),(1.00,1.10,-1.64,.32,-.21),
          (1.16,.03,-1.43,.17,-.115),(1.40,-.40,-1.33,.105,-.075),
          (1.75,-.90,-1.40,.055,-.05),(1.94,-1.15,-1.49,.035,-.035),
          (2.00,-1.23,-1.43,.015,-.02)]
ts=[0,.08,.20,.34,.48,.62,.76,.88,1]


def lerp_profile(t,pairs):
    for (a,v),(b,w) in zip(pairs,pairs[1:]):
        if t<=b:return v+(w-v)*(t-a)/(b-a)
    return pairs[-1][1]


def station_point(i,j,top=True):
    x,front,back,crest,belly=stations[i];t=ts[j]
    if top:
        if x<.60:
            f=lerp_profile(t,[(0,.045),(.2,.28),(.48,1),(.62,1),(.88,.72),(1,.48)])
        else:
            f=lerp_profile(t,[(0,.12),(.20,.65),(.34,1),(.62,1),(.88,.8),(1,.63)])
        y=crest*f
    else:
        f=lerp_profile(t,[(0,.18),(.20,.7),(.48,1),(.76,1),(1,.74)])
        y=belly*f
    if x<=1.0:
        longitudinal=[front,.60*front+.40*.55,min(.9,.35*front+.65*.55),
                      .45,-.05,-.55,-1.0,-1.28,back]
        z=longitudinal[j]
    else:z=front+(back-front)*t
    # Rear-only vertex refinement: keep the approved face layout and flat shading.
    # Taper the existing drive-cap facets and sweep the centre transom inward.
    aft=max(0,min(1,(-z-1.28)/max(.001,-back-1.28)))
    cap_scale=lerp_profile(x,[(0,.60),(.18,.62),(.34,.68),(.60,.78),
                             (.70,.55),(.85,.80),(1.0,.55),(1.16,1),(2,1)])
    y*=1-aft*(1-cap_scale)
    z+=aft*.12*max(0,1-x/.60)
    return Vector((x,y,z))


verts=[];faces=[];mats=[];lookup={};regions=[]
mirror_faces=True


def vertex(p):
    key=tuple(round(v,8) for v in p)
    if key not in lookup:lookup[key]=len(verts);verts.append(tuple(p))
    return lookup[key]


def face(points,mat=0,mirror=None):
    # Explicit triangles prevent unpredictable n-gon tessellation on curved panels.
    if mirror is None:mirror=mirror_faces
    triangles=[tuple(points[i] for i in tri) for tri in tessellate_polygon([points])] if len(points)>4 else [
        (points[0],points[k],points[k+1]) for k in range(1,len(points)-1)]
    for tri in triangles:faces.append(tuple(vertex(p) for p in tri));mats.append(mat)
    if mirror:
        for tri in triangles:
            faces.append(tuple(vertex((-p[0],p[1],p[2])) for p in reversed(tri)));mats.append(mat)


def bilerp(q,u,v):
    return q[0]*(1-u)*(1-v)+q[1]*u*(1-v)+q[2]*u*v+q[3]*(1-u)*v


# Use a separate pocket helper for the vent floor, with all side edges explicitly shared.
def pocket(q,outward,depth,mat=2,vent=False):
    normal=Vector((0,outward,0))
    uv=[(.12,.12),(.88,.12),(.88,.88),(.12,.88)]
    rim=[bilerp(q,u,v)-normal*.003 for u,v in uv]
    floor=[p-normal*depth for p in rim]
    for i in range(4):
        j=(i+1)%4;face([q[i],q[j],rim[j],rim[i]],0)
    if not vent:
        for i in range(4):
            j=(i+1)%4;face([rim[i],rim[j],floor[j],floor[i]],1)
        face(floor,mat);return
    cuts=[0,.36,.43,.63,.70,1]
    rows=[]
    for k,t in enumerate(cuts):
        row=[bilerp(floor,u,t) for u in (0,1)]
        if k in (2,3):row=[p+normal*.007 for p in row]
        rows.append(row)
    face([rim[0],rim[1],rows[0][1],rows[0][0]],1)
    face([rim[2],rim[3],rows[-1][0],rows[-1][1]],1)
    for k in range(len(rows)-1):
        face([rows[k][0],rows[k][1],rows[k+1][1],rows[k+1][0]],3 if k in (1,2,3) else mat)
    # The wall is concave around the raised louvre: triangulate its polygon, not a fan.
    face([rim[0],rim[3]]+[row[0] for row in reversed(rows)],1)
    face([rim[1]]+[row[1] for row in rows]+[rim[2]],1)


for top in (True,False):
    for i in range(len(stations)-1):
        for j in range(len(ts)-1):
            q=[station_point(i,j,top),station_point(i+1,j,top),
               station_point(i+1,j+1,top),station_point(i,j+1,top)]
            # Upper/lower drive vents, wing underside access panels and central belly recess.
            drive=i in (5,6) and j in (5,6)
            wing=(not top) and i in (8,9) and j in (3,4)
            belly=(not top) and i==0 and j in (4,5)
            canopy=top and i==0 and j in (3,4)
            if belly or canopy:
                continue # One full-width panel is stitched into this region below.
            if drive or wing:
                pocket(q,1 if top else -1,.020 if drive or belly else .010,
                       mat=1 if wing else 2,vent=drive)
                regions.append({'span':i,'length':j,'surface':'top' if top else 'bottom',
                                'kind':'drive vent' if drive else 'wing panel' if wing else 'belly' if belly else 'canopy'})
            else:
                mat=4 if top and i==0 and j>=6 else (1 if i in (1,3,8) or not top else 0)
                face(q,mat)
# Full-width central recesses include all perimeter vertices to preserve a welded boundary.
mirror_faces=False
for top,start,end,depth in [(True,3,5,.016),(False,4,6,.022)]:
    outline=[station_point(0,start,top)]
    outline += [station_point(1,j,top) for j in range(start,end+1)]
    outline += [station_point(0,end,top)]
    outline += [Vector((-p.x,p.y,p.z)) for p in
                [station_point(1,j,top) for j in range(end,start-1,-1)]]
    center=sum(outline,Vector())/len(outline);normal=Vector((0,1 if top else -1,0))
    inner=[center+(p-center)*.80-normal*depth for p in outline]
    for i in range(len(outline)):
        j=(i+1)%len(outline)
        face([outline[i],outline[j],inner[j],inner[i]],1)
    face(inner,2)
    regions.append({'surface':'top' if top else 'bottom','kind':'single centre recess'})
mirror_faces=True
# Perimeter joins the upper and lower skin. Centreline is welded by face() mirroring.
for i in range(len(stations)-1):
    for j in (0,len(ts)-1):
        q=[station_point(i,j,True),station_point(i+1,j,True),
           station_point(i+1,j,False),station_point(i,j,False)]
        # Continue the shield carrier onto the centre stern; secondary facets
        # separate the transom from white upper armour without extra panels.
        mat=(4 if i==0 else 1) if j==len(ts)-1 else 0
        face(q,2 if i in (5,6) else mat)
i=len(stations)-1
for j in range(len(ts)-1):
    face([station_point(i,j,True),station_point(i,j+1,True),
          station_point(i,j+1,False),station_point(i,j,False)],0)

shell=mesh('Hull_Continuous',verts,faces,hull,bevel=0)
for m in (facet,inset,edge,carrier):shell.data.materials.append(m)
for p,m in zip(shell.data.polygons,mats):p.material_index=m
shell['asset_id']='Hull_Continuous';shell['construction_group']='Unified'
shell['topology']='One connected closed skin; recesses and louvres are part of its surface'
approved=bpy.data.scenes.get('Swept Dart | v09')
topology_matches=None
if approved:
    approved_hull=next(o for o in approved.objects if o.get('asset_id')=='Hull_Continuous')
    topology_matches=(len(shell.data.vertices)==len(approved_hull.data.vertices) and
        [tuple(p.vertices) for p in shell.data.polygons]==
        [tuple(p.vertices) for p in approved_hull.data.polygons])
    assert topology_matches,'Preserve the approved v09 hull topology'

# Build a spatial surface query after triangulation for fitted system details.
bpy.context.view_layer.update()
bm=bmesh.new();bm.from_mesh(shell.data)
tree=BVHTree.FromBMesh(bm)


def surface_point(x,z,top=True):
    start=B((x,3 if top else -3,z));direction=B((0,-1 if top else 1,0))
    hit,normal,index,dist=tree.ray_cast(start,direction)
    assert hit is not None,(x,z,top)
    return hit,normal


def fitted_strip(name,coords,width,mat,group,top=True):
    # Closed low-profile ribbon sampled against the actual triangulated host skin.
    points=[]
    for x,z in coords:
        pair=[]
        for offset in (-width/2,0,width/2):
            hit,n=surface_point(x+offset,z,top)
            n=B((0,1 if top else -1,0))
            pair.append((hit+n*.0015,hit+n*.0045))
        points.append(pair)
    v=[]
    for row in points:
        for pair in row:v.extend(pair)
    f=[]
    for c in range(2):
        a=c*2;f.append((a,a+2,a+3,a+1))
    for k in range(len(points)-1):
        a=6*k;b=a+6
        for c in range(2):
            d=c*2
            f.append((a+d,b+d,b+d+2,a+d+2))
            f.append((a+d+1,a+d+3,b+d+3,b+d+1))
        f.append((a,a+1,b+1,b))
        f.append((a+4,b+4,b+5,a+5))
    a=6*(len(points)-1)
    for c in range(2):
        d=a+c*2;f.append((d,d+1,d+3,d+2))
    # mesh() receives Unity coordinates, so invert B for the fitted Blender vertices.
    o=mesh(name,[(p.x,p.z,-p.y) for p in v],f,mat,lights,0)
    o['asset_id']=name;o['construction_group']=group;o['surface_clearance_m']=.0015
    return o


for sign,side in [(-1,'L'),(1,'R')]:
    # Sampled at small intervals to track the host facets instead of crossing through them.
    fitted_strip('Nose_Channel_'+side,[(sign*(.065+.08*t),2.15-1.05*t) for t in [k/20 for k in range(21)]],.014,cyan,'Core')
    fitted_strip('Wing_Channel_'+side,[(sign*(1.20+.65*t),-.75-.55*t) for t in [k/20 for k in range(21)]],.018,cyan,'Wing_'+side)
    fitted_strip('Belly_Channel_'+side,[(sign*.38,-.25-.80*t) for t in [k/20 for k in range(21)]],.014,cyan,'Core',False)
    # Caps are part of the skin. These apertures sit entirely outside the cap, centred on old origins.
    for name,cx,z,w,h,mat in [('Drive_Light_',sign*.85,-1.643,.26,.036,white),
                            ('Weapon_Aperture_',sign*.75,1.103,.085,.016,cyan)]:
        points=[(x,y,zz) for zz in (z-.001,z+.001) for y in (-h/2,h/2) for x in (cx-w/2,cx+w/2)]
        o=mesh(name+side,points,[(0,1,3,2),(4,6,7,5),(0,4,5,1),
               (2,3,7,6),(0,2,6,4),(1,5,7,3)],mat,lights,0)
        o['asset_id']=name+side
        o['construction_group']=('Drive_' if name.startswith('Drive') else 'Weapon_')+side
for i,x in enumerate((-.10,0,.10),1):
    fitted_strip('Shield_Segment_'+str(i),[(x,-1.02-.20*t) for t in [k/10 for k in range(11)]],.035,white,'Core')
for obj in sockets.objects:
    obj['asset_id']=obj.name.split('.')[0];obj['construction_group']='Core'


def build_path(name,points,group):
    o=tube('Build_'+name,points,.005,construction_mat,construction)
    o['asset_id']='Build_'+name;o['construction_group']='Unified';o['branch_group']=group
    o['role']='construction_only'


for top,name in [(True,'Spine'),(False,'Keel')]:
    points=[]
    for j in range(len(ts)):
        p=station_point(0,j,top);p.y+=.006 if top else -.006;points.append(tuple(p))
    build_path(name,points,'Core')
for sign,side in [(-1,'L'),(1,'R')]:
    for row,name,group in [(6,'Drive','Drive_'),(9,'Wing','Wing_')]:
        points=[]
        for j in range(len(ts)):
            p=station_point(row,j,True);p.x*=sign;p.y+=.006;points.append(tuple(p))
        build_path(name+'_'+side,points,group+side)
    points=[]
    for i in range(len(stations)):
        p=station_point(i,0,True);p.x*=sign;p.y+=.006;points.append(tuple(p))
    build_path('Leading_'+side,points,'Wing_'+side)
    for j in (3,6):
        points=[]
        for i in range(len(stations)):
            p=station_point(i,j,True);p.x*=sign;p.y+=.006;points.append(tuple(p))
        build_path('Section_%d_'%j+side,points,'Wing_'+side)

for name,pos,target,scale in [('VIEW_Bottom',(0,-10,.35),(0,0,.35),7.2),
                             ('VIEW_Belly_Rear',(-4,-3.4,-5.8),(0,-.08,.05),5.7),
                             ('VIEW_Belly_Close',(3,-4.5,-3),(0,-.12,-.3),4.5),
                             ('VIEW_Construction',(5,4.4,7),(0,.05,.25),6),
                             ('VIEW_Junction',(3,2,3),(.62,.08,-.05),3.8)]:
    camera(name,pos,target,scale)
next(o for o in studio.objects if o.name.startswith('STUDIO_Underside')).data.energy=550
coll_map=json.loads(scene['collections']);coll_map['construction']=construction.name
scene['collections']=json.dumps(coll_map);scene['stage']='continuous hull study'
root['purpose']='Continuous skin with integrated recesses; no overlapping hull pieces'

# Validate connectedness and every edge, not just each former part in isolation.
assert all(e.is_manifold for e in bm.edges),[(tuple(v.co) for v in e.verts) for e in bm.edges if not e.is_manifold]
assert bm.calc_volume(signed=True)>0
remaining=set(bm.verts);components=0
while remaining:
    components+=1;stack=[remaining.pop()]
    while stack:
        for edge_ in stack.pop().link_edges:
            for v in edge_.verts:
                if v in remaining:remaining.remove(v);stack.append(v)
assert components==1,components
bm.free()
shell.data.calc_loop_triangles()
coords={tuple(round(c,6) for c in v.co) for v in shell.data.vertices}
assert {(-x,y,z) for x,y,z in coords}==coords
manifest=json.loads((HERE.parent / 'reviews'/REVISION/'manifest.json').read_text())
manifest['stage']='continuous hull study'
manifest['mesh_objects']=[{'name':'Hull_Continuous','base_triangles':len(shell.data.loop_triangles),
                          'closed_manifold':True,'connected_components':components}]
lo=[min(v.co[i] for v in shell.data.vertices) for i in range(3)]
hi=[max(v.co[i] for v in shell.data.vertices) for i in range(3)]
manifest['hull_bounds_unity']={'min':[lo[0],lo[2],-hi[1]],'max':[hi[0],hi[2],-lo[1]]}
manifest['checks']={'single_connected_hull':True,'closed_hull_sections':True,
                    'topology_identical_to_v09':topology_matches,
                    'exact_symmetry':True,'evaluated_hull_triangles':len(shell.data.loop_triangles),
                    'unity_import_or_play_mode':False}
manifest['integrated_recesses']=regions
manifest['construction_curves']=[o['asset_id'] for o in construction.objects]
manifest['materials']=[]
used={m for c in (body,lights,construction) for o in c.objects if o.type in ('MESH','CURVE') for m in o.data.materials}
for m in used:
    token=m['palette_token'];bsdf=m.node_tree.nodes.get('Principled BSDF');expected=base['rgba'](token)
    assert max(abs(a-b) for a,b in zip(bsdf.inputs['Base Color'].default_value,expected))<1e-6
    manifest['materials'].append({'name':m.name,'token':token,'srgb':base['palette'][token]})
(HERE.parent / 'reviews'/REVISION/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
runpy.run_path(str(HERE/'validate_model.py'))
for selected in bpy.context.selected_objects:selected.select_set(False)
bpy.context.view_layer.objects.active=shell
for name in ('build_blockout.py','build_cohesive.py','validate_model.py','export_study.py','render_review.py','compose_review.py'):
    text=bpy.data.texts.new(name+' | '+REVISION);text.write((HERE/name).read_text(encoding='utf8'))
print(json.dumps({'scene':scene.name,'hull_triangles':len(shell.data.loop_triangles),'connected_components':components}))
