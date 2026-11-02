"""Reproduce a luminous study from a preserved earlier snapshot in background Blender."""
from pathlib import Path
import runpy
import sys
import bpy

HERE=Path(__file__).resolve().parent
revision=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'v12'
runpy.run_path(str(HERE/'build_luminous_preview.py'),init_globals={'REVISION':revision})
runpy.run_path(str(HERE/'validate_luminous.py'),init_globals={'REVISION':revision})
bpy.ops.wm.save_as_mainfile(filepath=str(HERE.parent / 'reviews'/revision/('SweptDart_'+revision+'.blend')))
