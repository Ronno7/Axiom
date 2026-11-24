"""Create an isolated, editable Blender shader assembly study. Not a Unity shader."""
import json
import runpy
from pathlib import Path
import bpy

HERE=Path(__file__).resolve().parent
order=runpy.run_path(str(HERE/'assembly_order.py'))['assembly_order']
source=bpy.context.scene
revision=source['revision']
name='Swept Dart assembly | '+revision
assert name not in bpy.data.scenes,'Preserve earlier assembly previews'
coll={k:bpy.data.collections[v] for k,v in json.loads(source['collections']).items()}
scene=bpy.data.scenes.new(name);scene.world=source.world.copy()
scene.unit_settings.system='METRIC';scene.render.engine='CYCLES'
scene.cycles.device='CPU';scene.cycles.samples=16;scene.cycles.use_denoising=True
scene.cycles.transparent_max_bounces=32
scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=960;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.fps=24
scene.frame_start=1;scene.frame_end=40;scene.view_settings.view_transform=source.view_settings.view_transform
scene['revision']=revision;scene['role']='Blender-only assembly look and timing study'
scene['source_scene']=source.name;source['assembly_preview_scene']=name
scene['timing_note']='40 frames at 24 fps (1.67 s); slow review uses same frames at 4x duration. Not runtime wiring.'


def progress_socket(node):
    driver=node.outputs[0].driver_add('default_value').driver
    driver.expression='max(0,min(1.04,(frame-1)/38))'


materials={}
cyan=next(m for o in coll['interior'].objects for m in o.data.materials if m.get('palette_token')=='Cyan')
cyan_color=tuple(cyan.diffuse_color)


def reveal_material(original,wire=False):
    key=(original.name,wire)
    if key in materials:return materials[key]
    m=original.copy();m.name=original.name+' | assembly preview'
    n=m.node_tree.nodes;l=m.node_tree.links;output=n.get('Material Output');surface=n.get('Principled BSDF')
    p=n.new('ShaderNodeValue');p.name='AssemblyProgress';progress_socket(p)
    transparent=n.new('ShaderNodeBsdfTransparent')
    mix=n.new('ShaderNodeMixShader');l.new(transparent.outputs[0],mix.inputs[1])
    if wire:
        fade=n.new('ShaderNodeMath');fade.operation='LESS_THAN';fade.inputs[1].default_value=.58
        l.new(p.outputs[0],fade.inputs[0]);l.new(fade.outputs[0],mix.inputs[0]);l.new(surface.outputs[0],mix.inputs[2])
    else:
        attr=n.new('ShaderNodeAttribute');attr.attribute_name='AssemblyOrder'
        delta=n.new('ShaderNodeMath');delta.operation='SUBTRACT';l.new(p.outputs[0],delta.inputs[0]);l.new(attr.outputs['Fac'],delta.inputs[1])
        visible=n.new('ShaderNodeMath');visible.operation='GREATER_THAN';l.new(delta.outputs[0],visible.inputs[0]);visible.inputs[1].default_value=0
        edge=n.new('ShaderNodeMath');edge.operation='LESS_THAN';l.new(delta.outputs[0],edge.inputs[0]);edge.inputs[1].default_value=.018
        glow=n.new('ShaderNodeEmission');glow.inputs['Color'].default_value=cyan_color;glow.inputs['Strength'].default_value=1.4
        finish=n.new('ShaderNodeMixShader');l.new(edge.outputs[0],finish.inputs[0]);l.new(surface.outputs[0],finish.inputs[1]);l.new(glow.outputs[0],finish.inputs[2])
        l.new(visible.outputs[0],mix.inputs[0]);l.new(finish.outputs[0],mix.inputs[2])
    for link in list(output.inputs['Surface'].links):l.remove(link)
    l.new(mix.outputs[0],output.inputs['Surface']);materials[key]=m
    return m


mapping={};group_counts={}
for key in ('body','lights','interior','construction','studio'):
    c=bpy.data.collections.new('PREVIEW '+key+' | '+revision);scene.collection.children.link(c);mapping[key]=c.name
    for original in coll[key].objects:
        if original.type=='EMPTY':continue
        if original.name.startswith('VIEW_Turntable'):continue
        obj=original.copy();obj.data=original.data.copy();obj.animation_data_clear()
        c.objects.link(obj);obj.parent=None;obj.matrix_world=original.matrix_world.copy()
        obj.hide_viewport=False;obj.hide_render=original.hide_render if key=='studio' else False
        if key=='studio':continue
        if obj.type=='MESH':
            group='Exterior' if key=='body' else ('Systems' if key=='lights' else obj['construction_group'])
            attr=obj.data.attributes.new('AssemblyOrder','FLOAT','POINT')
            for v in obj.data.vertices:attr.data[v.index].value=order(v.co,group)[0]
            obj['preview_order_group']=group;group_counts[group]=group_counts.get(group,0)+1
            for i,m in enumerate(list(obj.data.materials)):obj.data.materials[i]=reveal_material(m)
            if key=='interior':
                obj.hide_render=False;obj.keyframe_insert('hide_render',frame=39)
                obj.hide_render=True;obj.keyframe_insert('hide_render',frame=40)
        elif obj.type=='CURVE':
            for i,m in enumerate(list(obj.data.materials)):obj.data.materials[i]=reveal_material(m,True)
            driver=obj.data.driver_add('bevel_factor_end').driver
            driver.expression='max(0,min(1,(frame-1)/13))'
scene['collections']=json.dumps(mapping)
scene.camera=next(o for o in scene.objects if o.name.split('.')[0]=='VIEW_InteriorRear')
for frame,label in [(1,'Trace'),(10,'Drives'),(18,'Cockpit and spars'),(22,'Exposed interior'),(29,'Shell consolidates'),(37,'Upper surfaces'),(40,'Flight exterior')]:
    scene.timeline_markers.new(label,frame=frame)
scene.frame_set(40)
bpy.context.window.scene=source
out=HERE.parent / 'reviews'/revision
report={'source_scene':source.name,'preview_scene':scene.name,'frames':40,'fps':24,
        'groups':group_counts,'exterior_topology_modified':False,'runtime_shader':False,
        'notes':scene['timing_note']}
(out/'assembly-preview.json').write_text(json.dumps(report,indent=2)+'\n')
print('PREVIEW_CREATED',json.dumps(report))
