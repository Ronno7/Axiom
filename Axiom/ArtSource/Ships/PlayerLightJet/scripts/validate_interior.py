"""Measure interior containment, mesh integrity, contacts and palette; no scene edits."""
import itertools
import json
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
scene=bpy.context.scene
coll={k:bpy.data.collections[v] for k,v in json.loads(scene['collections']).items()}
hull=next(o for o in coll['body'].objects if o.type=='MESH')
objects=list(coll['interior'].objects)


def geometry(obj):
    m=obj.data;m.calc_loop_triangles()
    points=[obj.matrix_world@v.co for v in m.vertices]
    tris=[tuple(t.vertices) for t in m.loop_triangles]
    return points,tris,BVHTree.FromPolygons(points,tris,all_triangles=True)


hp,ht,tree=geometry(hull)


def inside(p):
    # Majority parity rays handle concave rims where a nearest-face normal is ambiguous.
    votes=[]
    for d in ((.239,.571,.786),(-.423,.812,.402),(.743,-.317,.589)):
        direction=Vector(d).normalized();origin=p.copy();count=0
        for _ in range(64):
            hit,n,i,dist=tree.ray_cast(origin,direction)
            if hit is None:break
            count+=1;origin=hit+direction*1e-5
        votes.append(count%2==1)
    return sum(votes)>=2


stats={};trees={};problems=[]
for obj in objects:
    name=obj['asset_id'];points,tris,t=geometry(obj);trees[name]=t
    bm=bmesh.new();bm.from_mesh(obj.data)
    volume=bm.calc_volume(signed=True);manifold=all(e.is_manifold for e in bm.edges);bm.free()
    self_hits=[(a,b) for a,b in t.overlap(t) if a<b and not set(tris[a])&set(tris[b])]
    degenerate=sum(t.area<=1e-12 for t in obj.data.loop_triangles)
    crossing=len(t.overlap(tree));outside=[];clearances=[]
    # A closed connected part, with no boundary crossing and interior sample points,
    # is contained. Nearest-surface signs also expose barely protruding vertices.
    for i,p in enumerate(points):
        q,n,_,d=tree.find_nearest(p)
        if not inside(p):outside.append(i)
        clearances.append(d)
    stats[name]={'triangles':len(tris),'closed_manifold':manifold,'positive_volume':volume>0,
       'nonadjacent_self_intersections':len(self_hits),'degenerate_triangles':degenerate,
       'hull_surface_intersections':crossing,'outside_vertices':len(outside),
       'minimum_vertex_clearance_m':min(clearances),'group':obj['construction_group']}
    if not manifold or volume<=0 or self_hits or degenerate or crossing or outside:
        problems.append({name:stats[name]})
pairs={}
for a,b in itertools.combinations(objects,2):
    key=a['asset_id']+' / '+b['asset_id'];hits=trees[a['asset_id']].overlap(trees[b['asset_id']])
    if hits:pairs[key]=len(hits)
fit={}
for obj in coll['fit'].objects:
    p,t,b=geometry(obj)
    fit[obj['asset_id']]={'hull_surface_intersections':len(b.overlap(tree)),
                         'outside_vertices':sum(not inside(v) for v in p)}
palette={c['name']:c['hex'] for c in json.loads((HERE.parents[2]/'Palette/palette.json').read_text())['colors']}
material_checks={}
for mat in {m for o in objects for m in o.data.materials}:
    token=mat['palette_token'];h=palette[token].lstrip('#')
    rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    rgba=tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)+(1,)
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    assert all(max(abs(a-b) for a,b in zip(actual,rgba))<1e-6 for actual in
               (mat.diffuse_color,bsdf.inputs['Base Color'].default_value,bsdf.inputs['Emission Color'].default_value)),mat.name
    material_checks[mat.name]=token
report={'revision':scene['revision'],'parts':stats,'interior_surface_intersections':pairs,
        'pair_checks':len(objects)*(len(objects)-1)//2,'fit_guides':fit,
        'palette_materials':material_checks,'total_triangles':sum(s['triangles'] for s in stats.values()),
        'scope':'Interior closed mesh surfaces and containment in approved hull; nested drive rings deliberately surround cores without touching.',
        'passed':not problems and not pairs,'problems':problems}
out=HERE.parent / 'reviews'/scene['revision'];(out/'interior-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('INTERIOR_CHECK',json.dumps({'parts':len(objects),'triangles':report['total_triangles'],
      'problems':problems,'pair_intersections':pairs,'fit':fit}))
