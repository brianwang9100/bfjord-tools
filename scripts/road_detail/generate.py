#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Build original CC0 dry-stone verge and delineator assets, export and reimport all LODs.
Blender --background --python generate.py -- --output art/BFjordTools/RoadDetail
No image, mesh or vendor content is read by this generator.
"""
import argparse, hashlib, json, math, random, sys
from pathlib import Path
import bpy
from mathutils import Vector, noise
p=argparse.ArgumentParser();p.add_argument('--output',required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output).resolve()
for d in ['Models','Textures','Sources','Review']:(out/d).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
N=512; colors=[]; heights=[]
# A single repeatable metre-scale mineral field, separate from geometry variation.
for y in range(N):
 for x in range(N):
  q=Vector((x/N*5,y/N*5,.731))
  broad=noise.noise_vector(q)[0]; grain=noise.noise_vector(q*31)[1]
  n=noise.noise_vector(q*4)[2]; lichen=max(0,noise.noise_vector(q*.8)[1]-.42)
  h=broad*.055+n*.018+grain*.003; heights.append(h)
  c=.36+broad*.07+n*.065+grain*.036
  colors.extend([c+.018+lichen*.10,c+.010+lichen*.08,c-.015-lichen*.08,1])
def image(name,pixels):
 im=bpy.data.images.new(name,width=N,height=N,alpha=False);im.pixels.foreach_set(pixels);im.filepath_raw=str(out/'Textures'/f'{name}.png');im.file_format='PNG';im.save();return im
base=image('VergeStone_BaseColor',colors);normal=[]
for y in range(N):
 for x in range(N):
  dx=heights[y*N+(x+1)%N]-heights[y*N+(x-1)%N];dy=heights[((y+1)%N)*N+x]-heights[((y-1)%N)*N+x]
  v=Vector((-dx*7,-dy*7,1)).normalized();normal.extend([v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1])
norm=image('VergeStone_Normal',normal);norm.colorspace_settings.name='Non-Color';norm.save()
def material(name,color,roughness=.85):
 m=bpy.data.materials.new(name);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=roughness;return m
stone=material('VergeStone',(.4,.39,.36));n=stone.node_tree.nodes;l=stone.node_tree.links
tex=n.new('ShaderNodeTexImage');tex.image=base;l.new(tex.outputs['Color'],n.get('Principled BSDF').inputs['Base Color'])
texn=n.new('ShaderNodeTexImage');texn.image=norm;nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.55;l.new(texn.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],n.get('Principled BSDF').inputs['Normal'])
cream=material('DelineatorIvory',(.68,.665,.60),.69);black=material('DelineatorBlack',(.025,.030,.028),.74);amber=material('DelineatorAmber',(.85,.19,.018),.36)
def box(name,center,size,mat,bevel,rng,weather=False):
 bpy.ops.mesh.primitive_cube_add(size=1,location=center);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if weather:
  for v in o.data.vertices:
   v.co.x+=rng.uniform(-.031,.031);v.co.y+=rng.uniform(-.025,.025)
   if v.co.z>0:v.co.z+=rng.uniform(-.040,.040)
 o.data.materials.append(mat)
 if bevel:
  mod=o.modifiers.new('Worn stone arris' if weather else 'Moulded arris','BEVEL');mod.width=min(bevel,min(size)*.22);mod.segments=2 if lod==0 else 1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 # Project UVs per face from dominant normal; scale remains local to the stone, not its bounds.
 uv=o.data.uv_layers.active or o.data.uv_layers.new();phase=rng.uniform(0,3)
 for face in o.data.polygons:
  axis=max(range(3),key=lambda i:abs(face.normal[i]));axes=[i for i in range(3) if i!=axis]
  for li in face.loop_indices:
   v=o.data.vertices[o.data.loops[li].vertex_index].co
   uv.data[li].uv=(v[axes[0]]*.65+phase,v[axes[1]]*.65+phase*.73)
 return o
records=[];imported=[]
for asset in ['VergeWall_A','Delineator_A']:
 for lod in range(3):
  rng=random.Random(8817);objects=[]
  if asset=='VergeWall_A':
   # Batter, staggered joints and upright coping: no flat rectangular wall silhouette.
   for row in range(3):
    count=(7 if row%2==0 else 8) if lod<2 else 5
    weights=[rng.uniform(.64,1.4) for _ in range(count)]; lengths=[w/sum(weights)*2.4 for w in weights];cursor=-1.2
    for j,length in enumerate(lengths):
     z=.115+row*.235;depth=.68-row*.065
     objects.append(box('stone', (rng.uniform(-.013,.013),cursor+length*.5,z), (depth,length-.020,.226),stone,.015 if lod<2 else 0,rng,True));cursor+=length
   for j in range(11 if lod<2 else 6):
    count=11 if lod<2 else 6
    objects.append(box('coping',(rng.uniform(-.025,.025),-1.2+(j+.5)*2.4/count,.784+rng.uniform(-.035,.035)),(.55,2.4/count-.023,.20),stone,.012 if lod<2 else 0,rng,True))
  else:
   objects.append(box('ivory post',(0,0,.53),(.13,.15,1.06),cream,.016 if lod<2 else 0,rng))
   # Raised inset on both travel-facing sides; no emission or retroreflection claim.
   for sign in [-1,1]:
    objects.append(box('black inset',(0,sign*.079,.80),(.105,.015,.29),black,.008 if lod<2 else 0,rng))
    objects.append(box('reflector',(0,sign*.09,.83),(.061,.014,.112),amber,.007 if lod==0 else 0,rng))
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=f'{asset}_LOD{lod}'
  bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
  mod=o.modifiers.new('Triangulate','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
  path=out/'Models'/f'{o.name}.fbx'
  bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=True)
  expected=len(o.data.polygons);bpy.data.objects.remove(o,do_unlink=True)
  bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False);o=bpy.context.selected_objects[0]
  verts=[o.matrix_world@v.co for v in o.data.vertices];bounds=[[min(v[k] for v in verts),max(v[k] for v in verts)] for k in range(3)]
  assert len(o.data.polygons)==expected and all(len(f.vertices)==3 for f in o.data.polygons)
  assert all(math.isfinite(c) for v in verts for c in v) and len(o.data.uv_layers)==1
  assert all((o.data.vertices[f.vertices[1]].co-o.data.vertices[f.vertices[0]].co).cross(o.data.vertices[f.vertices[2]].co-o.data.vertices[f.vertices[0]].co).length>1e-9 for f in o.data.polygons)
  records.append({'id':asset,'lod':lod,'file':f'Models/{path.name}','triangles':expected,'vertices':len(verts),'boundsBlender':bounds,'uvChannels':len(o.data.uv_layers),'reimported':True,'materialSlots':[slot.material.name.split('.')[0] for slot in o.material_slots],'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
  # Review uses reimported FBX with original material definitions reassigned by slot name.
  for slot in o.material_slots:
   original=next((m for m in [stone,cream,black,amber] if slot.material.name.startswith(m.name)),None)
   if original:slot.material=original
  o.hide_render=lod!=0;imported.append(o)
  if asset=='Delineator_A':o.location=(1.1,.4,0)
manifest={'schemaVersion':1,'license':'CC0-1.0','generatorLicense':'MIT','units':'metres','pivot':'base centre','unityUp':'Y','collision':'None; decoration never owns ride collision.','assets':records}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
for asset in ['VergeWall_A','Delineator_A']:
 tri=[r['triangles'] for r in records if r['id']==asset];assert tri[0]>tri[1]>tri[2],tri
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.name='Review ground';ground.data.materials.append(material('ReviewGround',(.18,.20,.16)))
bpy.ops.object.camera_add(location=(3.7,-4.4,2.35));cam=bpy.context.object;cam.rotation_euler=(Vector((.28,0,.43))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=4.4;bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(0,-3,5));bpy.context.object.data.energy=900;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=4
scene=bpy.context.scene;scene.world.color=(.2,.2,.2);scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1100;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.filepath=str(out/'Review/road-detail-reimport.png');bpy.ops.wm.save_as_mainfile(filepath=str(out/'Sources/road-detail.blend'));bpy.ops.render.render(write_still=True)
(out/'Review/verification.json').write_text(json.dumps({'reimportedModels':len(records),'finiteGeometry':True,'nondegenerateTriangles':True,'decreasingLODs':True,'reviewUsesReimportedFBX':True,'blenderVersion':bpy.app.version_string},indent=2)+'\n')
print(json.dumps(manifest,indent=2))
