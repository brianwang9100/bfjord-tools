#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Refresh one family in the packed combined review after a bounded rebuild."""
import argparse
from pathlib import Path
import sys
import bpy
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--catalog',required=True);p.add_argument('--family',required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);source=Path(a.source).resolve();catalog=Path(a.catalog).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source/'TreeDetail07.blend'))
old=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(a.family+'_LOD')]
assert len(old)==3
position=old[0].location.copy();material=old[0].data.materials[0]
for o in old:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(catalog/'Models'/(a.family+'.fbx')))
for o in bpy.context.selected_objects:
 if o.type!='MESH':continue
 o.location=position;o.data.materials.clear();o.data.materials.append(material);o.hide_render='_LOD0' not in o.name;o.hide_set(o.hide_render)
scene=bpy.context.scene;scene.render.filepath=str(source/'Review'/'tree07-family.png')
bpy.ops.wm.save_as_mainfile(filepath=str(source/'TreeDetail07.blend'))
bpy.ops.render.render(write_still=True)
