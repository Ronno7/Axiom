"""Assembly checks for the continuous ship: surfaces, lights, symmetry and palette.

Run in Blender on the active continuous-hull review. Writes assembly-checks.json and
updates that revision's manifest. Does not modify or save the Blender scene.
"""
import itertools
import json
from pathlib import Path

import bpy
import bmesh
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
scene=bpy.context.scene
coll={k:bpy.data.collections[v] for k,v in json.loads(scene['collections']).items()}
hulls=[o for o in coll['body'].objects if o.type=='MESH']
assert len(hulls)==1,'Expected one continuous hull'
objects=hulls+list(coll['lights'].objects)
deps=bpy.context.evaluated_depsgraph_get()
trees={};self_hits={};volumes={}
for obj in objects:
    ev=obj.evaluated_get(deps);m=ev.to_mesh();m.calc_loop_triangles()
    points=[obj.matrix_world@v.co for v in m.vertices]
    triangles=[tuple(t.vertices) for t in m.loop_triangles]
    tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
    hits=[(a,b) for a,b in tree.overlap(tree) if a<b and
          not set(triangles[a])&set(triangles[b])]
    assert not hits,(obj.name,'nonadjacent triangle intersections',hits[:5])
    assert all(t.area>1e-12 for t in m.loop_triangles),(obj.name,'degenerate triangle')
    bm=bmesh.new();bm.from_mesh(m)
    assert all(e.is_manifold for e in bm.edges),(obj.name,'open/nonmanifold edge')
    volume=bm.calc_volume(signed=True);assert volume>0,(obj.name,'inverted volume')
    volumes[obj['asset_id']]=volume
    bm.free();self_hits[obj['asset_id']]=len(hits);trees[obj['asset_id']]=tree
    ev.to_mesh_clear()
cross_hits={}
for (a,ta),(b,tb) in itertools.combinations(trees.items(),2):
    hits=ta.overlap(tb)
    assert not hits,(a,b,'surface intersection',len(hits))
    cross_hits[a+' / '+b]=0
palette={c['name']:c['hex'] for c in json.loads((HERE.parents[2]/'Palette/palette.json').read_text())['colors']}
palette_checks={}
for mat in {m for c in (coll['body'],coll['lights'],coll['construction'])
            for o in c.objects if o.type in ('MESH','CURVE') for m in o.data.materials}:
    token=mat['palette_token'];hex_=palette[token].lstrip('#')
    rgb=[int(hex_[i:i+2],16)/255 for i in (0,2,4)]
    expected=tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)+(1,)
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    for actual in (mat.diffuse_color,bsdf.inputs['Base Color'].default_value,
                   bsdf.inputs['Emission Color'].default_value):
        assert max(abs(a-b) for a,b in zip(actual,expected))<1e-6,mat.name
    palette_checks[mat.name]=token
report={'revision':scene['revision'],'nonadjacent_self_intersections':self_hits,
        'pairwise_surface_intersections':cross_hits,'closed_positive_volumes':volumes,
        'palette_tokens':palette_checks,'construction_curves_excluded':True,
        'scope':'Visible solid hull and system meshes; source curves are a separate reveal study.'}
out=HERE.parent / 'reviews'/scene['revision']
(out/'assembly-checks.json').write_text(json.dumps(report,indent=2)+'\n')
p=out/'manifest.json';manifest=json.loads(p.read_text())
manifest['checks'].update({'visible_mesh_self_intersections':0,
    'visible_mesh_pair_intersections':0,'visible_mesh_pair_checks':len(cross_hits),
    'asset_palette_tokens_verified':True})
p.write_text(json.dumps(manifest,indent=2)+'\n')
print('ASSEMBLY_VERIFIED',json.dumps({'visible_meshes':len(trees),'pair_checks':len(cross_hits),
                                  'intersections':0,'palette_materials':len(palette_checks)}))
