#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Offline Blender illustration of the same bounded shader equation, not Unity playback evidence."""
import argparse,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--blend',required=True);p.add_argument('--output',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(Path(a.blend).resolve()));scene=bpy.context.scene
plant=next(o for o in scene.objects if o.name=='TallMeadowGrass_A_LOD0')
for ob in scene.objects:
 if ob.type=='MESH':ob.hide_render=ob!=plant and ob.name!='Review stage'
plant.hide_render=False;plant.hide_set(False);camera=scene.camera;camera.location=plant.location+Vector((1.9,-2.8,1.4));camera.rotation_euler=(plant.location+Vector((0,0,.72))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.9
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.cycles.samples=8;scene.render.threads=4
original=[v.co.copy() for v in plant.data.vertices];root=min(v.z for v in original);height=max(v.z for v in original)-root;phase=plant.location.x*.137+plant.location.y*.193;maximum=0
for frame in range(32):
 seconds=frame/8;time=seconds*.16*1.5;main=.72*math.sin(time*6+phase)+.28*math.sin(time*11+phase*1.7);across=.18*math.sin(time*9+phase*.73);bend=Vector(((.8*main-.6*across)*.14,(.6*main+.8*across)*.14,0))
 for vertex,base in zip(plant.data.vertices,original):
  h=min(1,max(0,(base.z-root)/height));weight=h*h*(3-2*h);vertex.co=base+bend*weight;maximum=max(maximum,(bend*weight).length)
 plant.data.update();scene.render.filepath=str(out/f'frame-{frame:03d}.png');bpy.ops.render.render(write_still=True)
(out/'evidence.json').write_text(json.dumps({'kind':'offline Blender shader-equation illustration','frames':32,'framesPerSecond':8,'amplitudeMeters':.14,'speed':1.5,'maximumObservedDisplacementMeters':maximum,'maximumAnalyticDisplacementMeters':.14*math.sqrt(1+.18**2),'rootWeight':0,'UnityPlaybackAcceptance':False},indent=2)+'\n')
