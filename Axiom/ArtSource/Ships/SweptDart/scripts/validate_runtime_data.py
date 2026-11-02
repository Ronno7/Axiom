"""Independent timing, palette, UV and texture validation on the staged blend."""
import hashlib
import json
import math
import re
from pathlib import Path
import bpy
import numpy as np

HERE=Path(__file__).resolve().parent
OUT=HERE.parent/'reviews/v14/export'
manifest=json.loads((OUT/'package-manifest.json').read_text(encoding='utf8'))
timeline=json.loads((OUT/'timeline.json').read_text(encoding='utf8'))
source=HERE.parent/'reviews/v13/SweptDart_v13.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest()==manifest['source_sha256']
image=bpy.data.images.load(str(OUT/'TraceTimeline.exr'),check_existing=False)
data=np.empty(289*128*4,np.float32);image.pixels.foreach_get(data);data=data.reshape(128,289,4)
preview=bpy.data.scenes['Swept Dart luminous assembly | v13'];bpy.context.window.scene=preview
curves={o.name.split('.')[0]:o for o in preview.objects if o.type=='CURVE'}
max_draw=0;max_light=0;max_visibility=0;retracts=0;dips=0
# Midpoints exercise interpolation between the sampled values, not just stored keys.
for i in range(288):
    seconds=(i+.5)/96;frame=seconds*24+1
    preview.frame_set(int(frame),subframe=frame-int(frame))
    for cue in timeline['curves']:
        row=cue['row'];o=curves[cue['id']]
        actual=(data[row,i]+data[row,i+1])*.5
        strength=next(n for n in o.data.materials[0].node_tree.nodes if n.type=='EMISSION').inputs['Strength'].default_value
        max_draw=max(max_draw,abs(float(actual[0])-o.data.bevel_factor_end))
        max_light=max(max_light,abs(float(actual[1])-max(0,strength)))
        max_visibility=max(max_visibility,abs(float(data[row,i,2])-float(not o.hide_render)))
for cue in timeline['curves']:
    row=cue['row'];a=int(round(cue['start_seconds']*96));b=int(round(cue['complete_seconds']*96))
    retracts+=int(np.any(np.diff(data[row,a:b+1,0])<-.001))
    dips+=int(np.any(np.diff(data[row,a:b+1,1])<-.01))
assert max_draw<.01 and max_light<1.5 and max_visibility==0,(max_draw,max_light,max_visibility)
assert retracts>0 and dips>0
assert not data[:91,0,2].any() and not data[:91,-1,2].any()

stage=bpy.data.scenes['Swept Dart runtime package | v14'];bpy.context.window.scene=stage
by_id={o['asset_id']:o for o in stage.objects if o.get('asset_id')}
for name,record in manifest['meshes'].items():
    obj=by_id[name];mesh=obj.data
    assert len(mesh.uv_layers)==4 and all(not p.use_smooth for p in mesh.polygons)
    assert all(math.isfinite(v) for l in mesh.uv_layers for item in l.data for v in item.uv)
    assert all(abs(obj.scale[i]-1)<1e-6 and abs(obj.location[i])<1e-6 for i in range(3))
    assert [m.name for m in mesh.materials]==record['materials']
    if record['role']=='construction_only':
        ids={round(item.uv.x*128-.5) for item in mesh.uv_layers[3].data}
        assert ids==set(range(91))
        for polygon in mesh.polygons:
            assert len({round(mesh.uv_layers[3].data[i].uv.x*128-.5) for i in polygon.loop_indices})==1
    if record['role']=='interior_reveal':
        # Each source part keeps a constant arrival through consolidation.
        for polygon in mesh.polygons:
            assert len({round(mesh.uv_layers[2].data[i].uv.x,6) for i in polygon.loop_indices})==1
for name in manifest['shield']['segments']+manifest['shield']['blink_renderers']:assert name in by_id
assert len(manifest['shield']['blink_renderers'])==11

palette=json.loads((HERE.parents[2]/'Palette/palette.json').read_text(encoding='utf8'))
tokens={c['name']:c['hex'] for c in palette['colors']}
def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
recipes={}
for name,r in manifest['materials'].items():
    h=tokens[r['palette_token']].lstrip('#');srgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]+[1]
    assert max(abs(a-b) for a,b in zip(r['base_linear'][:3],map(linear,srgb[:3])))<1e-6,name
    properties={'_BaseColor':{'palette':r['palette_token'],'srgb':srgb},'_BuildProgress':1}
    if r['mode']=='Trace':properties.update(_Timeline='TraceTimeline.exr',_EmissionScale=1)
    else:
        properties.update(_EdgeColor={'palette':'Cyan'},_EmissionStrength=r['emission_strength'],
            _ShadeStrength=.65,_UseArrivalMap=r['reveal_mode'],_RetireAt=r['retire_seconds'],
            _FrontDuration=r['front_seconds'],_FrontStrength=12)
        if r['arrival_texture']:properties['_ArrivalMap']=r['arrival_texture']
    recipes[name]={'shader':r['shader'],'properties':properties}
recipes['Shield_Lost_Edge']={'shader':'GeometricFlight/Swept Dart Surface',
    'properties':{'_BaseColor':{'palette':'Edge'},'_EdgeColor':{'palette':'Cyan'},'_BuildProgress':1,
        '_EmissionStrength':0,'_ShadeStrength':.65,'_UseArrivalMap':0,'_RetireAt':4,'_FrontDuration':.05,'_FrontStrength':12}}
(OUT/'material-bindings.json').write_text(json.dumps(recipes,indent=2)+'\n',encoding='utf8')
for name,recipe in recipes.items():
    shader=OUT/'Shaders'/('SweptDartTrace.shader' if recipe['shader'].endswith('Trace') else 'SweptDartSurface.shader')
    shader_text=shader.read_text(encoding='utf8')
    declared=set(re.findall(r'(_\w+)\s*\(',shader_text.split('SubShader')[0]))
    assert set(recipe['properties']).issubset(declared),(name,set(recipe['properties'])-declared)
    for value in recipe['properties'].values():
        if isinstance(value,str) and value.endswith('.exr'):assert (OUT/value).is_file()
hull_image=bpy.data.images.load(str(OUT/'HullArrival.exr'),check_existing=False)
hull_pixels=np.empty(2048*2048*4,np.float32);hull_image.pixels.foreach_get(hull_pixels)
assert np.isfinite(hull_pixels).all()
arrival=hull_pixels.reshape((-1,4))[:,0]
assert arrival.min()>=0 and arrival.max()<=2.8/3+1e-5
half_arrival_error=float(np.max(np.abs(arrival.astype(np.float16).astype(np.float32)-arrival))*3)
half_emission_error=float(np.max(np.abs(data[:,:,1].astype(np.float16).astype(np.float32)-data[:,:,1])))
assert half_arrival_error<.001 and half_emission_error<.02
report=dict(passed=True,midpoint_draw_fraction_error=max_draw,midpoint_emission_error=max_light,
    visibility_error=max_visibility,retracing_curves=retracts,brightness_dip_curves=dips,
    trace_rows_bound=91,interior_arrivals_constant_per_part=True,material_palette_checks=len(recipes),
    shield_bindings_exist=True,source_file_unchanged=True,shader_recipe_properties_exist=True,
    half_texture_arrival_error_seconds=half_arrival_error,half_texture_emission_error=half_emission_error,
    scope='Source data and shader recipes only; not Unity shader compilation or runtime playback.')
(OUT/'data-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8')
print('RUNTIME_DATA_VALIDATED',json.dumps(report),flush=True)
