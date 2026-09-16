#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Deterministic original CC0 tree artwork. No reference image bytes are inputs."""
import argparse,hashlib,json,math,random,sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector,Quaternion
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as g
p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--review',required=True);p.add_argument('--skip-review-renders',action='store_true');p.add_argument('--family',choices=['GiantRedwood_C','CoastalPine_C','MatureBeech_C']);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output).resolve();review=Path(a.review).resolve()
for d in [out/'Models',out/'Textures',review]:d.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
IDS=['GiantRedwood_C','CoastalPine_C','MatureBeech_C'];mat=None
source_root=review.parent/'Sources';source_receipt=json.loads((source_root/'sources.json').read_text())
for item in source_receipt['files']:
 assert hashlib.sha256((source_root/item['file']).read_bytes()).hexdigest()==item['sha256']

def texture(kind):
 n=1024;y,x=np.mgrid[0:n,0:n];u=x/n;v=y/n;rr=np.random.default_rng(711+IDS.index(kind));noise=rr.normal(0,1,(n,n));warp=.018*np.sin(v*26)+.007*np.sin(v*79+u*12)
 if 'Redwood' in kind:
  # Unequal fibre widths and branching interruptions avoid equally spaced ruled stripes.
  field=np.zeros_like(u);fiss=np.zeros_like(u)
  for i in range(45):
   center=rr.random()+warp*rr.uniform(.35,1.2)+.008*np.sin(v*rr.uniform(6,25)+rr.uniform(0,6));width=rr.uniform(.0015,.007)
   fiss=np.maximum(fiss,np.exp(-((u-center)/width)**2)*(.5+.5*np.sin(v*rr.uniform(2,8)+i)**2))
  grain=np.sin((u+warp)*950+np.sin(v*87)*2)*.025
  field=.76-.48*fiss+grain+.025*noise+.075*np.sin(u*63+v*7)*np.sin(v*23)
  rgb=field[:,:,None]*np.array([.46,.245,.135]);height=field*.5;rough=.89
 elif 'Pine' in kind:
  # Voronoi-like irregular flakes with deep seams and short stacked checks.
  d1=np.full_like(u,1e5);d2=d1.copy()
  for row in range(-1,9):
   for col in range(-1,13):
    cx=(col+rr.uniform(-.3,.3))/11;cy=(row+rr.uniform(-.35,.35))/7
    d=((u+.009*np.sin(v*87)-cx)*11)**2+((v+.005*np.sin(u*90)-cy)*7)**2
    d2=np.minimum(d2,np.maximum(d1,d));d1=np.minimum(d1,d)
  seam=np.exp(-((d2-d1)/.055)**2);field=.78-.44*seam+.12*np.sin(np.sqrt(d1)*7)+.025*noise
  rgb=field[:,:,None]*np.array([.39,.285,.19]);height=field*.32;rough=.9
 else:
  mott=np.sin(u*27+np.sin(v*14)*2)*np.sin(v*36-u*7);field=.76+.09*mott+.019*noise
  scars=np.zeros_like(u)
  for i in range(145):
   cx,cy=rr.random(2);scars=np.maximum(scars,np.exp(-(((u-cx)/rr.uniform(.004,.035))**4+((v-cy+.003*np.sin(u*35))/rr.uniform(.0008,.0025))**2)))
  field-=scars*.2;rgb=field[:,:,None]*np.array([.48,.475,.415]);height=field*.09;rough=.82
 colors=np.ones((n,n*2,4),dtype=np.float32);normals=colors.copy();mask=np.zeros_like(colors)
 def put(xs,ys,c,h,r):
  colors[ys,xs,:3]=np.clip(c,0,1);dy,dx=np.gradient(h);nn=np.stack([-dx*38,-dy*38,np.ones_like(h)],-1);nn/=np.linalg.norm(nn,axis=-1)[:,:,None];normals[ys,xs,:3]=nn*.5+.5;mask[ys,xs,3]=1-r
 put(slice(0,n),slice(0,n),rgb,height,rough)
 for j in range(4):
  yy,xx=np.mgrid[0:512,0:512];s=xx/511;t=yy/511;vein=np.exp(-((s-.5)/.013)**2);sidevein=np.exp(-(np.sin((t*8-np.abs(s-.5)*4)*math.pi)/.13)**2)
  palette=([.17,.29,.058] if 'Beech' in kind else [.20,.34,.145] if 'Pine' in kind else [.20,.35,.115]);palette=np.array(palette)*(1+j*.10)
  f=.72+.20*np.sin(t*math.pi)+.07*np.cos(s*math.pi)+rr.normal(0,.017,s.shape)
  c=f[:,:,None]*palette+vein[:,:,None]*np.array([.033,.04,.008])+sidevein[:,:,None]*.01
  put(slice(n+(j%2)*512,n+(j%2+1)*512),slice((j//2)*512,(j//2+1)*512),c,vein*.012+sidevein*.006,.72)
 # Use the verified CC0 bark scan for photographic microstructure; species tint stays authored.
 if 'Beech' not in kind:
  for item in source_receipt['files']:
   im=bpy.data.images.load(str(source_root/item['file']));im.colorspace_settings.name='Non-Color';im.scale(n,n);arr=np.array(im.pixels[:],dtype=np.float32).reshape(n,n,4)
   if '_diff_' in item['file']:
    tint=np.array([1.12,.82,.64] if 'Redwood' in kind else [1.02,.94,.83]);colors[:n,:n,:3]=np.clip(arr[:,:,:3]*tint,0,1)
   elif '_nor_gl_' in item['file']:normals[:n,:n,:3]=arr[:,:,:3]
   else:mask[:n,:n,3]=1-arr[:,:,0]
   bpy.data.images.remove(im)
 images=[]
 for suffix,array,linear in [('Atlas',colors,False),('Normal',normals,True),('Mask',mask,True)]:
  im=bpy.data.images.new(kind+suffix,width=n*2,height=n,alpha=True)
  if linear:im.colorspace_settings.name='Non-Color'
  im.pixels.foreach_set(array.ravel());im.filepath_raw=str(out/'Textures'/(im.name+'.png'));im.file_format='PNG';im.save();images.append(im)
 m=bpy.data.materials.new(kind+'Material');m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.8
 for i in range(2):
  node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=images[i]
  if i==0:m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
  else:
   nm=m.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.6;m.node_tree.links.new(node.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 return m,images

def at(path,t):
 x=min(len(path)-1.000001,max(0,t)*(len(path)-1));i=int(x);return path[i].lerp(path[i+1],x-i)
def direction(angle,z=0):return Vector((math.cos(angle),math.sin(angle),z))

def build(kind,lod,wood_only=False):
 wood=g.Mesh();fine=g.Mesh();r=random.Random(711+IDS.index(kind)*101);red='Redwood' in kind;pine='Pine' in kind;beech=not red and not pine
 def branch(path,rad):
  if wood_only and rad<.075:return
  if not wood_only and rad>=.075:return
  if (lod==1 or red) and rad<.009:return
  if lod==2 and rad<.05:return
  m=wood if rad>=.075 else fine;m.tube(path,[max(.003,rad*(1-i/(len(path)-1))**1.25) for i in range(len(path))],[5,4,3][lod] if rad<.075 else 14,0,True)
 height=20 if red else 11.6 if pine else 5.2
 def stem(z):return Vector((.04*z+.11*math.sin(z*.55) if red else .022*z*z+.09*math.sin(z*1.2) if pine else .06*z+.09*math.sin(z),.06*math.sin(z*.7),z))
 if wood_only:
  zs=[-.5,-.12,0,.22,.55,1,1.6]+[2+(height-2)*i/20 for i in range(21)];base=.82 if red else .34 if pine else .44
  radii=[base*(1.8 if z<.1 else (1.05+1.0*math.exp(-z*2)))*(max(.018,1-max(z,0)/height)**.65) for z in zs]
  wood.tube([stem(z) for z in zs],radii,40,0,True,True)
  for i in range(9 if red else 7):
   ang=i*2.399+r.uniform(-.3,.3);reach=r.uniform(1.7,2.5) if red else r.uniform(.9,1.5);d=direction(ang);rad=base*r.uniform(.4,.62)
   wood.tube([stem(.95)+d*.12,d*base*.75+Vector((0,0,.37)),d*reach*.57+Vector((0,0,.04)),d*reach*.83+Vector((0,0,-.08)),d*reach+Vector((0,0,-.3))],[rad,rad*.88,rad*.49,rad*.24,.012],14,0,True)
 # Consume collar randomness identically for foliage passes.
 else:
  for i in range(9 if red else 7):r.uniform(-.3,.3);r.uniform(1.7,2.5) if red else r.uniform(.9,1.5);r.uniform(.4,.62)
 terminals=[]
 if red or pine:
  count=80 if red else 22
  for i in range(count):
   f=i/(count-1);z=(3.7+f*15.8) if red else (3.0+f*8.3);ang=i*2.399+r.uniform(-.5,.5)
   # Mature redwood carries variable hanging boughs; coastal pine leaves windward gaps.
   if pine and i%7==2:continue
   reach=((4.4*(1-f)**.66+.25) if red else (3.5*(1-.50*f)+.2))*r.uniform(.68,1.23)
   delta=direction(ang)*reach+Vector((.2 if red else 1.0,0,r.uniform(-.25,.6) if red else r.uniform(.15,1.15)))
   path=g.curved(stem(z),stem(z)+delta,Vector((0,0,-.32 if red else -.17)),8);branch(path,(.16 if red else .14)*(1-.7*f))
   for j in range(r.randrange(6,10) if red else r.randrange(8,13)):
    t=.21+j*.073 if red else .15+j*.066;t=min(.97,t);base=at(path,t);a2=ang+(-1 if j%2 else 1)*r.uniform(.65,1.28);le=r.uniform(.48,1.23)*(1.1-.5*t) if red else r.uniform(.65,1.5)
    end=base+direction(a2)*le+Vector((.08 if red else .3,0,r.uniform(-.35,.12) if red else r.uniform(.13,.45)));secondary=g.curved(base,end,Vector((0,0,.12)),5);branch(secondary,.026 if red else .045)
    for q in range(r.randrange(2,5) if red else r.randrange(3,7)):
     s=.35+q*.14;base2=at(secondary,min(.97,s));a3=a2+(-1 if q%2 else 1)*r.uniform(.55,1.1);tip=base2+direction(a3)*r.uniform(.25,.55)+Vector((0,0,r.uniform(-.13,.14) if red else r.uniform(.1,.3)));shoot=g.curved(base2,tip,Vector((0,0,.06)),4);branch(shoot,.007);terminals.append((shoot,a3,r.randrange(10000000)))
 else:
  for i in range(10):
   ang=i*2.399+r.uniform(-.6,.6);z=r.uniform(2.3,4.4);reach=r.uniform(2.4,4.3);end=stem(z)+direction(ang)*reach+Vector((0,0,r.uniform(3.0,5.5)));path=g.curved(stem(z),end,direction(ang)*.38,9);branch(path,r.uniform(.16,.26))
   for j in range(r.randrange(7,10)):
    f=.23+j*.08;base=at(path,f);a2=ang+(-1 if j%2 else 1)*r.uniform(.5,1.3);le=r.uniform(.95,1.8);end2=base+direction(a2)*le+Vector((0,0,r.uniform(.3,1.5)));secondary=g.curved(base,end2,Vector((0,0,.23)),6);branch(secondary,.056)
    for q in range(r.randrange(7,12)):
     t=.18+q*.075;base2=at(secondary,min(t,.98));a3=a2+(-1 if q%2 else 1)*r.uniform(.5,1.4);le=r.uniform(.42,.92);shoot=g.curved(base2,base2+direction(a3)*le+Vector((0,0,r.uniform(-.2,.55))),Vector((0,0,.08)),4);branch(shoot,.009);terminals.append((shoot,a3,r.randrange(10000000)))
 if wood_only:return wood
 for shoot,ang,seed in terminals:
  rr=random.Random(seed);count=24 if red else 20 if pine else 19
  for j in range(count):
   t=.09+.91*j/(count-1);anchor=at(shoot,t);a2=ang+(-1 if j%2 else 1)*rr.uniform(.75,1.5);le=rr.uniform(.18,.27) if red else rr.uniform(.22,.34) if pine else rr.uniform(.14,.22);roll=rr.uniform(-.7,.7);tone=rr.randrange(1,5);z=rr.uniform(-.45,.45)
   if lod==1 and j%2:continue
   if lod==2 and j%8:continue
   scale=[1,1.28,2.05][lod]
   if beech:fine.leaf(anchor,direction(a2,z),le*scale,le*.31*scale,roll,tone,1,True)
   else:
    # Geometric needle fascicles: each folded taper has two triangles and its own UV.
    for k in range(1 if red else 3):
     theta=a2+(k-1)*.23;d=direction(theta,z+k*.1).normalized();side=d.cross(Vector((0,0,1))).normalized();normal=side.cross(d);w=le*(.12 if red else .060)*scale;mid=anchor+d*le*.55*scale+normal*w*.7;tip=anchor+d*le*scale+normal*le*.11
     fine.face([anchor,mid-side*w,tip,mid+side*w],[(.5,0),(0,.55),(.5,1),(1,.55)],tone)
 return fine

def fuse(kind):
 source=build(kind,0,True).object(kind+'WoodUvSource');ob=source.copy();ob.data=source.data.copy();bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
 mod=ob.modifiers.new('Fused buttresses and branch collars','REMESH');mod.mode='VOXEL';mod.voxel_size=.035 if 'Redwood' in kind else .024;mod.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=ob.modifiers.new('Collar relaxation','SMOOTH');mod.factor=.65;mod.iterations=3;bpy.ops.object.modifier_apply(modifier=mod.name)
 if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
 mod=ob.modifiers.new('Longitudinal UV projection','DATA_TRANSFER');mod.object=source;mod.use_loop_data=True;mod.data_types_loops={'UV'};mod.loop_mapping='POLYINTERP_NEAREST';bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(source,do_unlink=True);ob.select_set(False);return ob

def asset(kind):
 global mat
 mat,images=texture(kind);g.mat=mat;master=fuse(kind);obs=[]
 for lod in range(3):
  ob=master.copy();ob.data=master.data.copy();bpy.context.collection.objects.link(ob);ob.name=kind+'_LOD'+str(lod);ob.select_set(True);bpy.context.view_layer.objects.active=ob
  mod=ob.modifiers.new('Woody LOD budget','DECIMATE');mod.ratio=min(1,[32000,14000,4200][lod]/(len(ob.data.polygons)*2));bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)
  foliage=build(kind,lod).object('Foliage');foliage.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.join();ob.data.materials.clear();ob.data.materials.append(mat)
  for poly in ob.data.polygons:poly.material_index=0;poly.use_smooth=True
  tri=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name);ob.data.calc_tangents();ob.select_set(False);obs.append(ob)
 bpy.data.objects.remove(master,do_unlink=True);path=out/'Models'/(kind+'.fbx');exportmat=bpy.data.materials.new(kind+'ExportSlot')
 for ob in obs:ob.select_set(True);ob.data.materials[0]=exportmat
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,use_tspace=True,path_mode='STRIP',embed_textures=False)
 for ob in obs:bpy.data.objects.remove(ob,do_unlink=True)
 bpy.data.materials.remove(exportmat);bpy.ops.import_scene.fbx(filepath=str(path));checks=[];bounds=[];radius=0;crown=0;all_lod_crown=0
 for ob in list(bpy.context.selected_objects):
  if ob.type!='MESH':continue
  lod=int(ob.name.split('_LOD')[1][0]);ob.data.materials.clear();ob.data.materials.append(mat);me=ob.data;me.calc_tangents()
  assert me.uv_layers and all(len(poly.vertices)==3 and poly.area>1e-12 for poly in me.polygons),(kind,lod,'degenerate')
  assert all(math.isfinite(c) for v in me.vertices for c in v.co)
  assert all(math.isfinite(c) for loop in me.loops for c in (*loop.normal,*loop.tangent))
  assert all(math.isfinite(c) for uv in me.uv_layers.active.data for c in uv.uv)
  all_lod_crown=max(all_lod_crown,max(math.hypot(v.co.x,v.co.y) for v in me.vertices))
  checks.append({'lod':lod,'triangles':len(me.polygons),'finiteVerticesNormalsTangentsUV':True,'nondegenerate':True})
  if lod==0:
   bounds=[[min(v.co[k] for v in me.vertices) for k in range(3)],[max(v.co[k] for v in me.vertices) for k in range(3)]];radius=max(math.hypot(v.co.x,v.co.y) for v in me.vertices if v.co.z<.7);crown=max(math.hypot(v.co.x,v.co.y) for v in me.vertices)
  ob.hide_render=lod!=0;ob.hide_set(lod!=0);ob.select_set(False)
 for im in images:im.pack();im.filepath='//../asset-catalog/Assets/BFjord/TreeDetail07/Textures/'+im.name+'.png'
 rec={'id':kind,'model':'Models/'+path.name,'materialSlotCount':1,'textures':{s:'Textures/'+kind+s+'.png' for s in ['Atlas','Normal','Mask']},'boundsBlenderZUp':bounds,'rootFootprintRadius':radius,'crownRadius':crown,'allLodCrownRadius':all_lod_crown,'recommendedSpacingRadius':all_lod_crown+.5,'buriedCollar':True,'checks':sorted(checks,key=lambda x:x['lod'])}
 print('TREE07_FAMILY',json.dumps(rec),flush=True);return rec,images

families=[];images=[]
for kind in ([a.family] if a.family else IDS):
 rec,ims=asset(kind);families.append(rec);images+=ims
manifest={'revision':'tree-detail-07','license':'CC0-1.0','generator':'Scripts/world_assets/tree_detail_07/generate_trees07.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'geometrySha256':hashlib.sha256(Path(g.__file__).read_bytes()).hexdigest(),'blender':bpy.app.version_string,'textureSize':[2048,1024],'units':'metres; FBX Y up; authoring Blender Z up','families':families,'sourceProvenance':{'geometry':'Original deterministic authored branching, closed wood voxel unions, explicit geometric leaves and needle fascicles.','textures':'Conifer bark uses the verified Poly Haven Bark Brown 02 CC0 scan with authored species tint; beech bark and all foliage islands are original.','sourceTextures':source_receipt,'reuseResearch':[{'url':'https://extensions.blender.org/add-ons/sapling-tree-gen/','decision':'Free offline tree authoring alternative considered; existing original procedural workflow retained for species architecture and stable LOD anchors.'},{'url':'https://docs.blender.org/manual/en/4.5/modeling/modifiers/generate/remesh.html','decision':'Built-in voxel remesh fuses buttresses and scaffold junctions. Unity receives ordinary FBX meshes; no runtime package.'}],'privateReference':'Visually reviewed privately for branch variation and root transitions. No image bytes, names, textures or derived meshes are included.'}}
manifest['files']=[{'path':str(f.relative_to(out)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()} for f in sorted(list((out/'Models').glob('*.fbx'))+list((out/'Textures').glob('*.png')))]
(out/('tree07-manifest.json' if not a.family else a.family+'-manifest.json')).write_text(json.dumps(manifest,indent=2)+'\n')
# Review scene preserves the exact imported deliverables and packed source textures.
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.location.z=-.025;gm=bpy.data.materials.new('Review ground');gm.diffuse_color=(.09,.105,.075,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';scene=bpy.context.scene;scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-10,-14,24));bpy.context.object.data.energy=5500;bpy.context.object.data.size=9
world=scene.world;world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.7,.78,.9,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.55
bpy.ops.object.light_add(type='SUN',location=(-12,-18,22));bpy.context.object.rotation_euler=(.45,-.4,-.35);bpy.context.object.data.energy=1.6;bpy.context.object.data.angle=.2
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16;scene.render.threads_mode='FIXED';scene.render.threads=5;scene.view_settings.view_transform='AgX';scene.render.resolution_x=1200;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
for rec in families:
 kind=rec['id'];height=rec['boundsBlenderZUp'][1][2]
 for ob in scene.objects:
  if ob.type=='MESH' and '_LOD' in ob.name:ob.hide_render=not(ob.name.startswith(kind) and '_LOD0' in ob.name)
 for label,target,loc,scale in [('full',(0,0,height*.47),(18,-30,height*.63),height*1.12),('roots',(0,0,1),(4,-7,3),4.8 if 'Redwood' in kind else 3.4),('crown',(1,0,height*.70),(10,-17,height*.80),7 if 'Redwood' in kind else 6)]:
  cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(review/(kind+'-'+label+'.png'))
  if not a.skip_review_renders:bpy.ops.render.render(write_still=True)
for i,rec in enumerate(families):
 for ob in scene.objects:
  if ob.type=='MESH' and ob.name.startswith(rec['id']):ob.location.x=(i-1)*14;ob.hide_render='_LOD0' not in ob.name
cam.location=(32,-55,25);cam.rotation_euler=(Vector((0,0,9))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=42;scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.filepath=str(review/'tree07-family.png')
bpy.ops.wm.save_as_mainfile(filepath=str(review.parent/('TreeDetail07.blend' if not a.family else a.family+'.blend')))
if not a.skip_review_renders:bpy.ops.render.render(write_still=True)
print('TREE07_VERIFIED',flush=True)
