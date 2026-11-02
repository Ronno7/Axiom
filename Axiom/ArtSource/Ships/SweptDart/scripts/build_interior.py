"""Build a separate interior study while retaining the approved v10 exterior exactly.

Run in the working Blender file. Existing review scenes are preserved. Dimensions
are in Unity-local metres; construction preview shaders are created separately.
"""
import json
import math
import runpy
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
REVISION=globals().get('REVISION','v11')
base=runpy.run_path(str(HERE/'build_cohesive.py'),init_globals={'REVISION':REVISION})
for key in ('scene','root','shell','B','mesh','material','collection','camera','studio'):
    globals()[key]=base[key]
interior=collection('08 Interior_Reveal')
fit=collection('09 Cockpit fit study');fit.hide_render=True;fit.hide_viewport=True
structure=material('Interior frame','Structure',.12,.48)
edge=material('Interior supports','Edge',.1,.45)
text=material('Interior machinery','Text',.12,.4)
dark=material('Interior seat and cavities','Void',.02,.58)
cyan=material('Interior circuits','Cyan',.05,.4,.55)
light=material('Interior controls','Light',.04,.45)
parts=[]


def part(name,points,faces,mat,group,coll=interior):
    obj=mesh(name,points,faces,mat,coll,0)
    obj['asset_id']=name;obj['construction_group']=group;obj['role']='interior_reveal'
    obj['stage']='Interior blockout; not a functional cockpit or propulsion simulation'
    if coll is interior:parts.append(obj)
    return obj


def box(name,center,size,mat,group):
    c=Vector(center);h=Vector(size)*.5
    pts=[tuple(c+Vector((sx*h.x,sy*h.y,sz*h.z))) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    return part(name,pts,[(0,1,3,2),(4,6,7,5),(0,4,5,1),
        (2,3,7,6),(0,2,6,4),(1,5,7,3)],mat,group)


def beam(name,start,end,width,height,mat,group):
    # A rectangular spar with a horizontal transverse axis and vertical depth.
    a=Vector(start);b=Vector(end);d=(b-a).normalized()
    side=d.cross(Vector((0,1,0))).normalized()*width*.5
    up=side.normalized().cross(d).normalized()*height*.5
    pts=[tuple(p+side*s+up*u) for p in (a,b) for u in (-1,1) for s in (-1,1)]
    return part(name,pts,[(0,2,3,1),(4,5,7,6),(0,1,5,4),
        (2,6,7,3),(0,4,6,2),(1,3,7,5)],mat,group)


def drum(name,cx,cy,sections,mat,group,n=12):
    pts=[]
    for z,r in sections:
        pts.extend([(cx+math.cos(2*math.pi*k/n)*r,cy+math.sin(2*math.pi*k/n)*r,z) for k in range(n)])
    faces=[tuple(range(n-1,-1,-1))]
    for row in range(len(sections)-1):
        for k in range(n):
            a=row*n+k;b=row*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple(range((len(sections)-1)*n,len(sections)*n)))
    o=part(name,pts,faces,mat,group)
    o.data.materials.append(structure)
    for p in o.data.polygons:
        if p.index not in (0,len(faces)-1) and p.index%3==0:p.material_index=1
    return o


def ring(name,cx,cy,z,outer,inner,depth,mat,group,n=12):
    pts=[(cx+math.cos(2*math.pi*k/n)*r,cy+math.sin(2*math.pi*k/n)*r,zz)
         for zz in (z-depth/2,z+depth/2) for r in (outer,inner) for k in range(n)]
    faces=[]
    for k in range(n):
        j=(k+1)%n
        faces.extend([(k,j,n+j,n+k),(2*n+k,3*n+k,3*n+j,2*n+j),
                      (k,2*n+k,2*n+j,j),(n+k,n+j,3*n+j,3*n+k)])
    return part(name,pts,faces,mat,group)


def slab(name,outline,depth,mat,group):
    n=len(outline);pts=outline+[(x,y-depth,z) for x,y,z in outline]
    return part(name,pts,[tuple(range(n)),tuple(range(2*n-1,n-1,-1))]+
                [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat,group)


# Compact structural chassis. Deliberate small assembly gaps avoid coplanar skins.
box('INT_Keel',(0,-.205,-.33),(.13,.052,1.05),edge,'Chassis')
slab('INT_CockpitFloor',[(-.245,-.141,.67),(.245,-.141,.67),
     (.245,-.141,-.62),(-.245,-.141,-.62)],.018,structure,'Chassis')
for sign,side in [(-1,'L'),(1,'R')]:
    beam('INT_CockpitRail_'+side,(sign*.28,-.105,.64),(sign*.28,-.105,-.71),.028,.033,text,'Chassis')
    for j,z in enumerate((.35,-.60)):
        beam('INT_Crossmember_%s_%d'%(side,j),(sign*.30,-.12,z),(sign*.68,-.12,z),.035,.035,edge,'Chassis')
    # One segmented, faceted drive body, with separate encircling collars and aft throat.
    cx=sign*.85;cy=.025;group='Engine_'+side
    drum('INT_DriveCore_'+side,cx,cy,[(.54,.055),(.43,.102),(-.83,.102),(-.93,.075)],text,group)
    drum('INT_ExhaustThroat_'+side,cx,cy,[(-.947,.073),(-1.10,.095),(-1.37,.063)],edge,group)
    for j,z in enumerate((.32,-.12,-.56)):
        ring('INT_DriveCollar_%s_%d'%(side,j),cx,cy,z,.14,.113,.045,edge,group)
        # Visible circuit on a separate narrow annular band, with radial clearance.
        ring('INT_DriveCircuit_%s_%d'%(side,j),cx,cy,z,.145,.143,.014,cyan,group)
    ring('INT_NozzleRing_'+side,cx,cy,-1.16,.113,.101,.065,text,group)
    for j,z in enumerate((.32,-.56)):
        box('INT_DriveSaddle_%s_%d'%(side,j),(cx,-.137,z),(.29,.025,.065),structure,'Chassis')
    # Exposed wing spar follows the sweep and stays within the very thin outer wing.
    beam('INT_MainSpar_'+side,(sign*1.02,-.004,-.82),(sign*1.79,-.004,-1.22),.043,.025,text,'Spar_'+side)
    for j,(x,z0,z1) in enumerate(((1.22,-.56,-1.24),(1.48,-.78,-1.26))):
        # Rear and front halves stop at the main spar with clearance.
        hitz=-.82-(x-1.02)/(.77)*.40
        beam('INT_WingRibFront_%s_%d'%(side,j),(sign*x,-.005,z0),(sign*x,-.005,hitz+.033),.022,.018,edge,'Spar_'+side)
        beam('INT_WingRibRear_%s_%d'%(side,j),(sign*x,-.005,hitz-.033),(sign*x,-.005,z1),.022,.018,edge,'Spar_'+side)
    # Two broad feed rails explain the core-to-drive route.
    beam('INT_PowerFeed_'+side,(sign*.32,.005,-.90),(sign*.67,.005,-1.10),.025,.025,cyan,'Services')

# A low reclined cockpit. No human mesh is added or scaled to force a fit.
slab('INT_SeatPan',[(-.18,-.064,.28),(.18,-.064,.28),(.18,-.03,-.17),(-.18,-.03,-.17)],.035,dark,'Cockpit')
slab('INT_SeatBack',[(-.18,-.014,-.19),(.18,-.014,-.19),(.155,.245,-.61),(-.155,.245,-.61)],.026,dark,'Cockpit')
slab('INT_Headrest',[(-.105,.272,-.63),(.105,.272,-.63),(.105,.32,-.75),(-.105,.32,-.75)],.027,text,'Cockpit')
for sign,side in [(-1,'L'),(1,'R')]:
    beam('INT_SeatRim_'+side,(sign*.197,.005,-.18),(sign*.176,.266,-.62),.017,.019,text,'Cockpit')
    box('INT_ControlPod_'+side,(sign*.22,-.018,.19),(.046,.043,.27),edge,'Cockpit')
    beam('INT_ControlGrip_'+side,(sign*.22,.009,.23),(sign*.22,.077,.205),.022,.022,light,'Cockpit')
    beam('INT_FootGuide_'+side,(sign*.115,-.075,.40),(sign*.085,-.025,.91),.025,.022,edge,'Cockpit')
slab('INT_Console',[(-.155,.074,.47),(.155,.074,.47),(.14,.116,.62),(-.14,.116,.62)],.026,structure,'Cockpit')
slab('INT_ConsoleScreen',[(-.118,.087,.485),(.118,.087,.485),(.107,.117,.592),(-.107,.117,.592)],.003,cyan,'Cockpit')
box('INT_CoreDistribution',(0,.012,-1.03),(.235,.12,.23),edge,'Services')
box('INT_CoreStatus',(0,.076,-1.03),(.13,.004,.12),cyan,'Services')

# A simple seated envelope for spatial review, not an anthropometric certification.
for name,center,size in [('Head',(0,.36,-.69),(.18,.18,.22)),
                         ('Shoulders',(0,.19,-.43),(.40,.19,.27)),
                         ('Hips',(0,.04,-.04),(.30,.16,.28)),
                         ('Legs',(0,.014,.55),(.26,.14,.70))]:
    obj=box('FIT_'+name,center,size,edge,'Fit')
    interior.objects.unlink(obj);fit.objects.link(obj);parts.remove(obj)
    obj['role']='fit_guide_only';obj.display_type='WIRE'

camera('VIEW_Interior',(3.7,4.6,4.2),(0,.03,-.15),4.9)
camera('VIEW_Cockpit',(2.4,3.8,2.6),(0,.06,-.10),2.05)
camera('VIEW_InteriorRear',(-3.6,3.0,-5.0),(0,.02,-.1),5.35)
next(o for o in studio.objects if o.name.startswith('STUDIO_Underside')).data.energy=450
mapping=json.loads(scene['collections']);mapping.update(interior=interior.name,fit=fit.name)
scene['collections']=json.dumps(mapping);scene['stage']='inside-out interior blockout'
root['purpose']='Approved exterior with separate temporary interior assemblies'
scene['interior_note']='Reclined cockpit fit study; no character or human ergonomic certification'

out=HERE.parent / 'reviews'/REVISION
manifest=json.loads((out/'manifest.json').read_text())
manifest['stage']=scene['stage']
manifest['interior']={'objects':[o['asset_id'] for o in parts],
                     'groups':sorted({o['construction_group'] for o in parts}),
                     'fit_note':scene['interior_note'],'unity_verified':False}
approved=bpy.data.scenes.get('Swept Dart | v10')
if approved:
    old=next(o for o in approved.objects if o.get('asset_id')=='Hull_Continuous')
    assert [tuple(p.vertices) for p in old.data.polygons]==[tuple(p.vertices) for p in shell.data.polygons]
    assert all((a.co-b.co).length<1e-7 for a,b in zip(old.data.vertices,shell.data.vertices))
    assert [p.material_index for p in old.data.polygons]==[p.material_index for p in shell.data.polygons]
    manifest['checks']['exterior_identical_to_v10']=True
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
checks=runpy.run_path(str(HERE/'validate_interior.py'))['report']
assert checks['passed'],'Resolve the interior-checks report before accepting this revision'
manifest['interior'].update(triangles=checks['total_triangles'],validation_passed=True)
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
runpy.run_path(str(HERE/'build_assembly_preview.py'))
for name in ('build_interior.py','validate_interior.py','assembly_order.py','build_assembly_preview.py','render_assembly.py','compose_assembly.py'):
    p=HERE/name
    if p.exists():t=bpy.data.texts.new(name+' | '+REVISION);t.write(p.read_text())
print('INTERIOR_BUILT',json.dumps({'scene':scene.name,'parts':len(parts)}))
