"""Render the baked-data review scene, not the original curve animation."""
from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent
OUT=HERE.parent/'reviews/v14/qa/frames';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['Swept Dart runtime package | v14'];bpy.context.window.scene=scene
scene.render.resolution_x=640;scene.render.resolution_y=426;scene.cycles.samples=16
scene.render.use_persistent_data=True
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='OPTIX';prefs.get_devices()
for device in prefs.devices:device.use=device.type=='OPTIX'
scene.cycles.device='GPU'
for frame in range(1,73):
    scene.frame_set(frame);scene.render.filepath=str(OUT/f'frame-{frame:03}.png')
    bpy.ops.render.render(write_still=True)
print('RUNTIME_ANIMATION_DONE',flush=True)
