"""Render the exported meshes and baked data in Blender; no Unity operations."""
import json
import math
from pathlib import Path
import bpy

HERE=Path(__file__).resolve().parent
OUT=HERE.parent/'reviews/v14/qa';OUT.mkdir(parents=True,exist_ok=True)
PACKAGE=OUT.parent/'export'
manifest=json.loads((PACKAGE/'package-manifest.json').read_text(encoding='utf8'))
scene=bpy.data.scenes['Swept Dart runtime package | v14']
preview=bpy.data.scenes['Swept Dart luminous assembly | v13']
bpy.context.window.scene=preview;preview.frame_set(50);bpy.context.view_layer.update()
studio_matrices={o.name:o.matrix_world.copy() for o in preview.objects if o.type in ('LIGHT','CAMERA')}
bpy.context.window.scene=scene
scene.world=preview.world.copy();scene.view_settings.view_transform=preview.view_settings.view_transform
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.cycles.transparent_max_bounces=48
scene.render.threads_mode='FIXED';scene.render.threads=4
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='OPTIX';prefs.get_devices()
for device in prefs.devices:device.use=device.type=='OPTIX'
scene.cycles.device='GPU';scene.render.resolution_x=960;scene.render.resolution_y=640
scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8'
cameras={}
for o in preview.objects:
    if o.type in ('LIGHT','CAMERA'):
        copy=o.copy();copy.data=o.data.copy();copy.animation_data_clear()
        copy.parent=None;copy.constraints.clear();copy.matrix_world=studio_matrices[o.name];scene.collection.objects.link(copy)
        if o.type=='CAMERA':cameras[o.name.split('.')[0]]=copy
scene.camera=cameras['VIEW_InteriorRear']
scene.compositing_node_group=preview.compositing_node_group.copy()
for node in scene.compositing_node_group.nodes:
    if node.type=='R_LAYERS':node.scene=scene
arrival=bpy.data.images.load(str(PACKAGE/'HullArrival.exr'),check_existing=False)
timeline=bpy.data.images.load(str(PACKAGE/'TraceTimeline.exr'),check_existing=False)
for img in (arrival,timeline):img.colorspace_settings.name='Non-Color';img.pack()
cyan=next(v['base_linear'] for v in manifest['materials'].values() if v['palette_token']=='Cyan')

def wire_math(nodes,links,kind,a,b):
    node=nodes.new('ShaderNodeMath');node.operation=kind
    for i,v in enumerate((a,b)):
        if isinstance(v,(float,int)):node.inputs[i].default_value=v
        else:links.new(v,node.inputs[i])
    return node.outputs[0]

for name,record in manifest['materials'].items():
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links
    out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    seconds=nodes.new('ShaderNodeValue');seconds.name='RuntimeSeconds'
    seconds.outputs[0].driver_add('default_value').driver.expression='(frame-1)/24'
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='RevealOrder'
    sep=nodes.new('ShaderNodeSeparateXYZ');links.new(uv.outputs[0],sep.inputs[0])
    op=lambda kind,a,b:wire_math(nodes,links,kind,a,b)
    if record['mode']=='Trace':
        row=nodes.new('ShaderNodeUVMap');row.uv_map='TraceIdentity'
        row_sep=nodes.new('ShaderNodeSeparateXYZ');links.new(row.outputs[0],row_sep.inputs[0])
        xy=nodes.new('ShaderNodeCombineXYZ');links.new(row_sep.outputs[0],xy.inputs[1])
        links.new(op('ADD',op('MULTIPLY',seconds.outputs[0],96/289),.5/289),xy.inputs[0])
        tex=nodes.new('ShaderNodeTexImage');tex.image=timeline;tex.interpolation='Linear';tex.extension='EXTEND'
        links.new(xy.outputs[0],tex.inputs['Vector'])
        split=nodes.new('ShaderNodeSeparateColor');links.new(tex.outputs['Color'],split.inputs[0])
        visible=op('MULTIPLY',op('GREATER_THAN',split.outputs[0],sep.outputs[1]),op('GREATER_THAN',split.outputs[2],.5))
        emit=nodes.new('ShaderNodeEmission');emit.inputs['Color'].default_value=cyan
        links.new(split.outputs[1],emit.inputs['Strength']);finish=emit.outputs[0]
    else:
        if record['mode']=='Hull':
            uv0=nodes.new('ShaderNodeUVMap');uv0.uv_map='SurfaceUV'
            tex=nodes.new('ShaderNodeTexImage');tex.image=arrival;tex.interpolation='Linear';tex.extension='EXTEND'
            links.new(uv0.outputs[0],tex.inputs['Vector']);field=tex.outputs['Color']
        else:field=sep.outputs[0]
        delta=op('SUBTRACT',seconds.outputs[0],op('MULTIPLY',field,3))
        visible=op('MULTIPLY',op('GREATER_THAN',delta,0),op('LESS_THAN',seconds.outputs[0],record['retire_seconds']))
        physical=nodes.get('Principled BSDF')
        edge=nodes.new('ShaderNodeEmission');edge.inputs['Color'].default_value=cyan;edge.inputs['Strength'].default_value=12
        mix=nodes.new('ShaderNodeMixShader');links.new(op('LESS_THAN',delta,record['front_seconds']),mix.inputs[0])
        links.new(physical.outputs[0],mix.inputs[1]);links.new(edge.outputs[0],mix.inputs[2]);finish=mix.outputs[0]
    transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader')
    links.new(visible,mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(finish,mix.inputs[2])
    for link in list(out.inputs['Surface'].links):links.remove(link)
    links.new(mix.outputs[0],out.inputs['Surface'])

for frame in (6,28,40,46,50,54,62,72):
    scene.frame_set(frame);scene.render.filepath=str(OUT/f'stage-{frame:03}.png')
    bpy.ops.render.render(write_still=True)
    print('RUNTIME_REVIEW_FRAME',frame,flush=True)

shields=[next(o for o in scene.objects if o.get('asset_id')==name) for name in manifest['shield']['segments']]
lit=shields[0].data.materials[0]
lost=bpy.data.materials.new('Shield_Lost_Edge');lost.use_nodes=True
edge_color=next(v['base_linear'] for v in manifest['materials'].values() if v['palette_token']=='Edge')
lost.diffuse_color=edge_color
lost.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=edge_color
lost.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.45
scene.camera=cameras['VIEW_Rear_ThreeQuarter']
scene.frame_set(72)
for remaining in (3,2,1,0):
    for i,o in enumerate(shields):o.data.materials[0]=lit if i<remaining else lost
    scene.render.filepath=str(OUT/f'shield-{remaining}.png');bpy.ops.render.render(write_still=True)
    print('SHIELD_REVIEW',remaining,flush=True)
for o in shields:o.data.materials[0]=lit
scene.camera=cameras['VIEW_InteriorRear'];scene.frame_set(50)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'runtime-review.blend'))
print('RUNTIME_REVIEW_DONE',flush=True)
