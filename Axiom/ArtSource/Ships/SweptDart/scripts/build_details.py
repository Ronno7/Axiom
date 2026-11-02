"""Build the structural study, preserving all earlier scenes.

Run in Blender with __file__ set to this source path. Uses the reproducible blockout
as a base, then adds closed recessed panels, underside structure and authored curves.
"""
import json
import math
import runpy
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
REVISION = 'v06'
base = runpy.run_path(str(HERE / 'build_blockout.py'), init_globals={'REVISION': REVISION})
for key in ('scene', 'body', 'lights', 'sockets', 'studio', 'root', 'mesh', 'loft',
            'prism', 'tube', 'collection', 'camera', 'B', 'hull', 'facet', 'edge',
            'inset', 'cyan', 'white', 'material'):
    globals()[key] = base[key]

root['purpose'] = 'Structural study with underside detailing and authored construction paths'
scene['stage'] = 'white hull study'

# Player-white uses the shared Light token, not technical White or an independent tint.
# Dark cavities and the rear shield carrier preserve contrast on the pale shell.
for mat, token, metal, roughness in [(hull, 'Light', .05, .36),
                                    (facet, 'Text', .08, .42)]:
    color = base['rgba'](token)
    mat.diffuse_color = color
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Emission Color'].default_value = color
    bsdf.inputs['Emission Strength'].default_value = 0
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = roughness
    mat['palette_token'] = token
shield_carrier = material('Shield carrier', 'Structure', .1, .43)
spine = next(o for o in body.objects if o.name.startswith('Dorsal_Spine'))
spine.data.materials.append(shield_carrier)
for polygon in spine.data.polygons:
    if all(-spine.data.vertices[i].co.y <= -.899 for i in polygon.vertices):
        polygon.material_index = len(spine.data.materials)-1
construction = collection('07 Construction curves')
construction.hide_render = True
construction.hide_viewport = True
construction_mat = material('Construction only', 'Cyan', 0, .5, 1.1)


def panel(name, outline, outward, rim=.14, depth=.018, mat=facet, surface=None):
    """Closed recessed panel; outline is Unity xyz on a supporting surface.

    A raised rim surrounds a lower inset floor, backed by a closed base.
    outward is +1 for upper skin, -1 for belly. No coplanar decal overlay.
    """
    n = len(outline)
    center = tuple(sum(p[i] for p in outline) / n for i in range(3))
    inner = [(x + (center[0] - x) * rim, y, z + (center[2] - z) * rim)
             for x, y, z in outline]
    if surface is not None:
        outline = [(x, surface(x,z), z) for x,y,z in outline]
        inner = [(x, surface(x,z), z) for x,y,z in inner]
    pts = list(outline)
    pts += [(x, y + outward * depth, z) for x, y, z in outline]
    pts += [(x, y + outward * depth, z) for x, y, z in inner]
    pts += [(x, y + outward * depth * .28, z) for x, y, z in inner]
    faces = [tuple(range(n - 1, -1, -1))]
    for ring in range(3):
        for i in range(n):
            j = (i + 1) % n
            faces.append((ring*n+i, ring*n+j, (ring+1)*n+j, (ring+1)*n+i))
    faces.append(tuple(range(3*n, 4*n)))
    o = mesh(name, pts, faces, mat, bevel=.0025)
    o.data.materials.append(inset)
    o.data.polygons[-1].material_index = 1
    return o


# Continuous ventral keel: joins the nose to the belly without becoming a hanging fin.
loft('Belly_Keel', [(2.08, .055, .0, -.060), (1.28, .13, -.07, -.125),
     (.55, .22, -.12, -.215), (-.12, .27, -.17, -.285),
     (-.88, .25, -.18, -.29), (-1.39, .17, -.15, -.225)], facet, bevel=.009)
panel('Belly_Service_Recess', [(-.14,-.291,-.25),(.14,-.291,-.25),
      (.18,-.291,-.44),(.16,-.291,-.76),(.09,-.291,-.90),
      (-.09,-.291,-.90),(-.16,-.291,-.76),(-.18,-.291,-.44)], -1, depth=.017)

for sign, side in [(-1, 'L'), (1, 'R')]:
    def M(points):
        return [(sign*x,y,z) for x,y,z in points]

    # A narrow rib follows the swept wing root and supports the underside panel.
    prism('Wing_Spar_'+side, M([(.42,-.135,.12),(.55,-.12,.17),
          (1.86,-.051,-1.15),(1.81,-.054,-1.28),(1.18,-.09,-.93),
          (.48,-.135,-.24)]), .028, hull, .006)
    panel('Wing_Belly_Recess_'+side, M([(1.20,-.087,-.30),(1.72,-.057,-1.03),
          (1.78,-.053,-1.20),(1.52,-.069,-1.24),(1.18,-.09,-1.16)]), -1,
          rim=.13, depth=.018, surface=lambda x,z: -.079+(abs(x)-1.4)*(.03/.54))
    # Inner wing-root fairing connects the central hull to each propulsion shoulder.
    prism('Root_Belly_Plate_'+side, M([(.30,-.16,.15),(.53,-.115,.28),
          (.61,-.18,-.78),(.49,-.205,-1.27),(.31,-.185,-1.30)]), .032, facet, .005)
    panel('Drive_Belly_Recess_'+side, M([(.69,-.207,-.22),(1.01,-.207,-.22),
          (1.055,-.222,-.64),(1.02,-.208,-1.32),(.68,-.208,-1.32),
          (.645,-.222,-.64)]), -1, rim=.16, depth=.025, surface=lambda x,z: -.223)
    # Four wide fins sit inside the drive recess, above its rim when seen from below.
    for i in range(4):
        z = -.62 - i*.145
        y = -.234
        prism('Drive_Vent_%d_'%i+side, M([(.72,y,z),(.98,y,z),
              (.98,y+.002,z-.055),(.72,y+.002,z-.055)]), .009, edge, .002)
    # Two small aft rails give a finished bottom edge around the nozzle.
    for i, x in enumerate([.64,1.06]):
        prism('Drive_Rail_%d_'%i+side, M([(x-.025,-.20,-1.05),(x+.025,-.20,-1.05),
              (x+.025,-.19,-1.54),(x-.025,-.19,-1.54)]), .032, hull, .004)
    # Broad shoulder panel; the forward/rear forms remain clean from the chase view.
    panel('Drive_Upper_Recess_'+side, M([(.72,0,-.67),(.98,0,-.67),
          (1.01,0,-.78),(1.0,0,-1.22),(.70,0,-1.22),(.69,0,-.78)]),
          1, depth=.014, rim=.12, mat=hull,
          surface=lambda x,z: .354+(z+.65)*(.11/.91))
    for i in range(3):
        z = -.70 - i*.12
        y = .35 + (z+.65)*(.11/.91) + .014
        prism('Drive_Upper_Louvre_%d_'%i+side, M([(.735,y,z),(.965,y,z),
              (.965,y-.004,z-.035),(.735,y-.004,z-.035)]), .009, facet, .002)
    # Selective underside lights follow the inner root, away from shield indicators.
    tube('Belly_Root_Light_'+side, M([(.39,-.201,-.25),(.44,-.235,-.73),
         (.42,-.238,-1.06)]), .008, cyan)
    # Nose lower cheek is a broad facet; it carries the pointed front around the underside.
    prism('Nose_Lower_Cheek_'+side, M([(.075,-.088,1.67),(.16,-.14,1.05),
          (.24,-.203,.52),(.19,-.219,.43),(.095,-.14,1.03)]), .012, hull, .003)


def group_for(name):
    side = name.rsplit('_',1)[-1]
    if side in ('L','R'):
        if name.startswith(('Drive','Exhaust')): return 'Drive_'+side
        if name.startswith(('Weapon','Muzzle')): return 'Weapon_'+side
        if name.startswith(('Wing','Tip','Root','Belly_Root')): return 'Wing_'+side
    return 'Core'


# Stable IDs are independent of Blender's automatically suffixed display names.
for coll in (body, lights, sockets):
    for obj in coll.objects:
        obj['asset_id'] = obj.name.split('.')[0]
        obj['construction_group'] = group_for(obj['asset_id'])
        if obj.type == 'MESH': obj['stage'] = 'structural study'


def path(name, points, group, radius=.007, closed=False):
    obj = tube('Build_'+name, points, radius, construction_mat, construction, closed=closed)
    obj['asset_id'] = 'Build_'+name
    obj['construction_group'] = group
    obj['role'] = 'construction_only'
    return obj


path('Spine', [(0,.026,2.5),(0,.14,1.85),(0,.28,1.1),
     (0,.50,.48),(0,.514,-.18),(0,.412,-.9),(0,.258,-1.38)], 'Core')
path('Keel', [(0,-.065,2.08),(0,-.132,1.28),(0,-.222,.55),
     (0,-.292,-.12),(0,-.297,-.88),(0,-.232,-1.39)], 'Core')
for sign, side in [(-1,'L'),(1,'R')]:
    def M(points): return [(sign*x,y,z) for x,y,z in points]
    path('Nose_'+side, M([(.045,.025,2.5),(.13,.13,1.85),(.24,.23,1.05),
         (.36,.34,.3),(.41,.35,-.55),(.33,.23,-1.44)]), 'Core')
    path('Wing_Outline_'+side, M([(.30,.12,.98),(.80,.11,.42),(1.4,.05,-.40),
         (1.94,.045,-1.12),(1.95,.04,-1.49),(1.4,.055,-1.30),
         (.8,.115,-1.36),(.30,.125,-1.32)]), 'Wing_'+side, closed=True)
    path('Wing_Rib_'+side, M([(.36,.34,-.55),(.8,.17,-.56),
         (1.4,.11,-.895),(1.94,.042,-1.285)]), 'Wing_'+side)
    path('Wing_Lower_'+side, M([(.32,-.17,.10),(.8,-.13,-.35),
         (1.4,-.11,-.90),(1.86,-.08,-1.22)]), 'Wing_'+side)
    path('Drive_Ridge_'+side, M([(.85,.24,.53),(.85,.37,.13),
         (.85,.36,-.65),(.85,.25,-1.56)]), 'Drive_'+side)
    for j,z in enumerate([.08,-.67,-1.53]):
        width=.26 if j==0 else .29
        top=.36 if j<2 else .25
        path('Drive_Section_%d_'%j+side, M([(.85-width*.62,top,z),
             (.85+width*.62,top,z),(.85+width,.08,z),(.85+width,-.08,z),
             (.85+width*.62,-.21,z),(.85-width*.62,-.21,z),
             (.85-width,-.08,z),(.85-width,.08,z)]), 'Drive_'+side, closed=True)

camera('VIEW_Bottom', (0,-10,.35), (0,0,.35), 7.2)
camera('VIEW_Belly_Rear', (-4,-3.4,-5.8), (0,-.08,.05), 5.7)
camera('VIEW_Belly_Close', (3,-4.5,-3), (0,-.12,-.3), 4.5)
camera('VIEW_Construction', (5,4.4,7),(0,.05,.25),6)
next(o for o in studio.objects if o.name.startswith('STUDIO_Underside')).data.energy=850
coll_map=json.loads(scene['collections'])
coll_map['construction']=construction.name
scene['collections']=json.dumps(coll_map)

# Bake-free QA of closed source sections and symmetry, including the underside.
bpy.context.view_layer.update()
manifest=json.loads((HERE.parent / 'reviews'/REVISION/'manifest.json').read_text())
manifest['stage']='white hull study'
manifest['mesh_objects']=[]
evaluated_tris=0
pts=[]
for obj in body.objects:
    if obj.type!='MESH': continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    assert all(e.is_manifold for e in bm.edges),obj.name
    assert bm.calc_volume(signed=True)>0,obj.name
    bm.free()
    assert obj.matrix_local==Matrix.Identity(4),obj.name
    obj.data.calc_loop_triangles()
    ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
    m.calc_loop_triangles();evaluated_tris+=len(m.loop_triangles);ev.to_mesh_clear()
    pts.extend([v.co.copy() for v in obj.data.vertices])
    manifest['mesh_objects'].append({'name':obj['asset_id'],'group':obj['construction_group'],
        'base_triangles':len(obj.data.loop_triangles),'closed_manifold':True})
objects={o['asset_id']:o for o in body.objects if o.type=='MESH'}
mirrored=[]
for name,obj in objects.items():
    if name.endswith('_L'):
        other=objects[name[:-2]+'_R']
        a={tuple(round(c,5) for c in (-v.co.x,v.co.y,v.co.z)) for v in obj.data.vertices}
        b={tuple(round(c,5) for c in v.co) for v in other.data.vertices}
        assert a==b,name
        mirrored.append(name[:-2])
lo=[min(p[i] for p in pts) for i in range(3)]
hi=[max(p[i] for p in pts) for i in range(3)]
manifest['hull_bounds_unity']={'min':[lo[0],lo[2],-hi[1]],'max':[hi[0],hi[2],-lo[1]]}
manifest['construction_curves']=[o['asset_id'] for o in construction.objects]
manifest['checks']={'closed_hull_sections':True,'identity_mesh_transforms':True,
    'mirrored_pairs':mirrored,'evaluated_hull_triangles':evaluated_tris,
    'unity_import_or_play_mode':False}
# Audit the actual active asset materials, not just their descriptive token labels.
used={m for c in (body,lights,construction) for o in c.objects
      if o.type in ('MESH','CURVE') for m in o.data.materials}
manifest['materials']=[]
for mat in sorted(used,key=lambda m:m.name):
    token=mat['palette_token']
    assert token in base['palette'],mat.name
    expected=base['rgba'](token)
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    for actual in (mat.diffuse_color,bsdf.inputs['Base Color'].default_value,
                   bsdf.inputs['Emission Color'].default_value):
        assert max(abs(a-b) for a,b in zip(actual,expected))<1e-6,mat.name
    strength=bsdf.inputs['Emission Strength'].default_value
    if mat in (hull,facet,inset,edge,shield_carrier):assert strength==0,mat.name
    manifest['materials'].append({'name':mat.name,'token':token,
        'srgb':base['palette'][token],'emission_strength':strength})
manifest['checks']['asset_palette_tokens_verified']=True
manifest['checks']['solid_hull_non_emissive']=True
(HERE.parent / 'reviews'/REVISION/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
for obj in bpy.context.selected_objects: obj.select_set(False)
for name in ('build_blockout.py','build_details.py'):
    text=bpy.data.texts.new(name+' | '+REVISION)
    text.write((HERE/name).read_text(encoding='utf8'))
scene.camera=next(o for o in studio.objects if o.name.startswith('VIEW_Front_ThreeQuarter'))
print(json.dumps({'scene':scene.name,'hull_meshes':len(manifest['mesh_objects']),
      'evaluated_triangles':evaluated_tris,'construction_paths':len(construction.objects)}))
