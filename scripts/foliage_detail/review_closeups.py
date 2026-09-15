# SPDX-License-Identifier: MIT
"""Render each exported LOD0 at a useful botanical inspection scale from the saved review blend."""
import argparse, sys
from pathlib import Path
import bpy
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;scene.cycles.samples=24;scene.render.resolution_x=720;scene.render.resolution_y=720
plants=[o for o in scene.objects if o.type=='MESH' and '_LOD' in o.name]
for o in scene.objects:
 if o.type=='FONT':o.hide_render=True
for ob in [o for o in plants if '_LOD0' in o.name]:
 for other in plants:other.hide_render=other!=ob
 corners=[ob.matrix_world@Vector(c) for c in ob.bound_box];center=sum(corners,Vector())/8
 height=max(c.z for c in corners)-min(c.z for c in corners);width=max(max(c.x for c in corners)-min(c.x for c in corners),max(c.y for c in corners)-min(c.y for c in corners))
 scene.camera.location=center+Vector((2,-3,2.1))*max(height,width)
 scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=max(height,width)*1.5
 scene.render.filepath=str(out/(ob.name.split('_LOD')[0]+'-detail.png'));bpy.ops.render.render(write_still=True)
