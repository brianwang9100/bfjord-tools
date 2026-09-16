#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Original v0.5 woodland assets; no external images/models are inputs. Outputs CC0-1.0.
Blender --background --python generate_woodland.py -- --output PATH --review PATH
"""
import argparse, hashlib, json, math, random, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--review',required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output).resolve();review=Path(a.review).resolve()
for d in [out/'Models',out/'Textures',review]:d.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Eight guarded UV islands: oak, birch, leaf, grass, deadwood, heartwood, seed, moss.
N=1024; yy,xx=np.mgrid[0:N,0:N];u=(xx%256)/255;v=(yy%512)/511;tile=xx//256+(yy//512)*4
rng=np.random.default_rng(20260915);noise=rng.random((N,N))-.5
ridge=np.sin(u*math.tau*17+np.sin(v*math.tau*2)*.65+np.sin(v*17+u*6)*1.4)+.4*np.sin(u*math.tau*39+v*11)
crack=np.clip((ridge+.25)*.7,0,1);bark=.52+.27*crack+.12*np.sin(u*24+v*31)*np.sin(v*53-u*19)+.06*noise
scar=(np.sin(v*math.tau*32+np.sin(u*15)*.2)>.89)&(np.sin(u*51+v*91)>.1)
vein=np.exp(-((u-.5)/.013)**2)*.2+np.exp(-(np.sin((v*9-np.abs(u-.5)*4)*math.pi)/.10)**2)*.055
leaf=.68+.18*np.sin(v*math.pi)+vein+.05*noise
heart=np.sin(np.sqrt((u-.5)**2+(v-.5)**2)*160)
colors=np.zeros((N,N,4),dtype=np.float32);colors[:,:,3]=1;heights=np.zeros((N,N),dtype=np.float32);rough=np.ones((N,N),dtype=np.float32)*.8
for t,col,field in [(0,(.28,.24,.18),bark),(1,(.76,.73,.65),.88-.75*scar+.04*noise),(2,(.15,.31,.055),leaf),(3,(.26,.35,.10),.7+.22*v+vein+.05*noise),(4,(.38,.31,.22),bark),(5,(.46,.35,.22),.8+.09*heart+.06*noise),(6,(.42,.32,.14),.7+.25*v),(7,(.14,.23,.035),.8+.16*noise)]:
 mask=tile==t
 for ch in range(3):colors[:,:,ch][mask]=(field*col[ch])[mask]
 heights[mask]=(field*.12)[mask];rough[mask]=(.65 if t in (2,3) else .91)
# Tangent-space normal uses island-clamped derivatives so atlas gutters cannot bleed height discontinuities.
normal=np.ones((N,N,4),dtype=np.float32);normal[:,:,3]=1
dy,dx=np.gradient(heights);normal[:,:,:3]=np.stack((-dx*70,-dy*70,np.ones((N,N))),axis=-1);normal[:,:,:3]/=np.linalg.norm(normal[:,:,:3],axis=-1)[:,:,None];normal[:,:,:3]=normal[:,:,:3]*.5+.5
packed=np.zeros((N,N,4),dtype=np.float32);packed[:,:,3]=1-rough
images=[]
for name,arr,linear in [('WoodlandAtlas',colors,False),('WoodlandNormal',normal,True),('WoodlandMask',packed,True)]:
 im=bpy.data.images.new(name,width=N,height=N,alpha=True)
 if linear:im.colorspace_settings.name='Non-Color'
 im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(out/'Textures'/f'{name}.png');im.file_format='PNG';im.save();images.append(im)
mat=bpy.data.materials.new('OriginalWoodland');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.8
for im in images:
 node=nodes.new('ShaderNodeTexImage');node.image=im
 if im==images[0]:links.new(node.outputs['Color'],bs.inputs['Base Color'])
 elif im==images[1]:
  nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.6;links.new(node.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
class Mesh:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def face(self,pts,uv,t):
  start=len(self.v);self.v.extend(pts);self.f.append(tuple(range(start,start+len(pts))));self.uv.extend([((t%4+.035+.93*x)/4,(t//4+.02+.96*y)/2) for x,y in uv])
 def tube(self,pts,radii,sides,t):
  # Stable frames along the limb prevent ring twists and pinching at bends.
  rings=[]
  for j,(p,r) in enumerate(zip(pts,radii)):
   axis=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();side=axis.cross(Vector((0,1,0)))
   if side.length<.1:side=axis.cross(Vector((1,0,0)))
   side.normalize();up=axis.cross(side)
   rings.append([p+(side*math.cos(k*math.tau/sides)+up*math.sin(k*math.tau/sides))*r*(1+.06*math.sin(k*7+j*.6)) for k in range(sides)])
  for j in range(len(pts)-1):
   for k in range(sides):
    n=(k+1)%sides;self.face([rings[j][k],rings[j][n],rings[j+1][n],rings[j+1][k]],[(k/sides,j/(len(pts)-1)),((k+1)/sides,j/(len(pts)-1)),((k+1)/sides,(j+1)/(len(pts)-1)),(k/sides,(j+1)/(len(pts)-1))],t)
 def leaf(self,p,d,length,width,t=2,lobes=False):
  d=d.normalized();side=d.cross(Vector((0,0,1)))
  if side.length<.01:side=d.cross(Vector((0,1,0)))
  side.normalize();normal=side.cross(d).normalized();steps=4 if lobes else 3
  # Folded leaf strips produce a midrib and lobed oak margin without alpha overdraw.
  for j in range(steps):
   def row(k):
    f=k/steps;w=width*math.sin(math.pi*f)**.7*(.77+.23*(k%2) if lobes else 1);mid=p+d*length*f+normal*length*(.10*math.sin(math.pi*f)-.09*f*f)
    return [mid-side*w,mid+normal*w*.25,mid+side*w]
   a,b=row(j),row(j+1)
   for k in range(2):
    pts=[a[k],a[k+1],b[k+1],b[k]];uv=[(k*.5,j/steps),((k+1)*.5,j/steps),((k+1)*.5,(j+1)/steps),(k*.5,(j+1)/steps)]
    if j==0:pts.pop(0);uv.pop(0)
    if j==steps-1:pts.pop(2);uv.pop(2)
    self.face(pts,uv,t)
 def object(self,name):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();mesh.materials.append(mat);uv=mesh.uv_layers.new(name='UVMap')
  for poly in mesh.polygons:
   for li in poly.loop_indices:uv.data[li].uv=self.uv[mesh.loops[li].vertex_index]
  ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001);bpy.ops.object.mode_set(mode='OBJECT')
  for poly in mesh.polygons:poly.use_smooth=True
  mod=ob.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False);return ob

def tree(kind,lod):
 birch='Birch' in kind;seed=841 if birch else 219;m=Mesh();barktile=1 if birch else 0
 height=10.8 if birch else 8.8;trunk=height*.82 if birch else height*.52
 pts=[Vector((.10*math.sin(i*.8)*i/8,.06*math.cos(i)*i/8,trunk*i/8)) for i in range(9)]
 m.tube(pts,[((.19 if birch else .43)*(1-i/9)**1.1+.016) for i in range(9)],[12,9,6][lod],barktile)
 # Flared roots sit on the metric root pivot, with natural unequal buttresses.
 for i in range(6):
  a=i*2.4;tip=Vector((math.cos(a)*(.55 if birch else 1),math.sin(a)*(.55 if birch else 1),.02));m.tube([tip,tip*.4+Vector((0,0,.16)),Vector((0,0,.56))],[.025,.09,.19 if not birch else .08],[7,5,4][lod],barktile)
 for i in range(18 if birch else 15):
  r=random.Random(seed+i);ang=i*2.399+r.uniform(-.25,.25);t=i/(17 if birch else 14);z=trunk*(.22+.72*t);base=Vector((.05,0,z))
  reach=(2.45*(1-.40*t) if birch else 3.3*math.sqrt(max(.15,1-((t-.42)/.82)**2)))*r.uniform(.85,1.2);d=Vector((math.cos(ang),math.sin(ang),0));rise=(1.7 if birch else 2.65)+t*.8
  limb=[base+d*reach*f+Vector((0,0,rise*(f*.7+f*f*.3))) for f in (0,.22,.5,.76,1)]
  radius=(.075 if birch else .15)*(1-.55*t);m.tube(limb,[radius*(1-f*.94) for f in (0,.22,.5,.76,1)],[7,5,4][lod],barktile)
  for j in range(7):
   rr=random.Random(seed*100+i*20+j);f=.28+j*.105;start=base+d*reach*f+Vector((0,0,rise*(f*.7+f*f*.3)));a2=ang+(-1 if j%2 else 1)*rr.uniform(.6,1.3)
   dd=Vector((math.cos(a2),math.sin(a2),rr.uniform(.25,.65)));length=rr.uniform(.72,1.40)*(1-.25*t);end=start+dd*length
   m.tube([start,start.lerp(end,.55)+Vector((0,0,.1)),end],[radius*.25,radius*.14,.005],[5,4,3][lod],barktile)
   for k in range(5):
    rt=random.Random(seed*10000+i*100+j*4+k);start2=start.lerp(end,.20+k*.19);a3=a2+(-1 if k%2 else 1)*rt.uniform(.7,1.7);d3=Vector((math.cos(a3),math.sin(a3),rt.uniform(-.3,.6)));tip=start2+d3*rt.uniform(.40,.73)
    if lod<2:m.tube([start2,tip],[.009,.002],[4,3,3][lod],barktile)
    for q in range([11,5,2][lod]):
     # Deterministic thinning keeps leaf/twig locations coherent through the LODs.
     tq=(q+.3)/[11,5,2][lod];lp=start2.lerp(tip,tq);theta=a3+q*2.4;direction=Vector((math.cos(theta),math.sin(theta),rt.uniform(-.55,.8)))
     scale=[1,1.25,1.70][lod];m.leaf(lp,direction,rt.uniform(.19,.29)*scale,rt.uniform(.060,.095)*scale,lobes=not birch and lod==0)
 return m

def log(lod):
 m=Mesh();rings=[];sides=[22,14,9][lod];length=4.7
 # Open inner wall and irregular broken rims are actual geometry, not black end caps.
 for j in range(13):
  f=j/12;center=Vector(((f-.5)*length,.15*math.sin(f*5)+.04*math.sin(f*17),.46+.15*math.sin(f*4)));r=.46*(1-.18*f)*(.94+.07*math.sin(f*12))
  rings.append([(center+Vector((.22*math.sin(k*2.6) if j in (0,12) else 0,math.cos(k*math.tau/sides)*r,math.sin(k*math.tau/sides)*r)),center+Vector((.22*math.sin(k*2.6) if j in (0,12) else 0,math.cos(k*math.tau/sides)*r*.65,math.sin(k*math.tau/sides)*r*.65))) for k in range(sides)])
 for j in range(12):
  for k in range(sides):
   n=(k+1)%sides
   for wall in (0,1):
    pts=[rings[j][k][wall],rings[j][n][wall],rings[j+1][n][wall],rings[j+1][k][wall]]
    if wall:pts.reverse()
    m.face(pts,[(k/sides,j/12),((k+1)/sides,j/12),((k+1)/sides,(j+1)/12),(k/sides,(j+1)/12)],4)
 for j in (0,12):
  for k in range(sides):
   n=(k+1)%sides;m.face([rings[j][k][0],rings[j][n][0],rings[j][n][1],rings[j][k][1]],[(k/sides,0),((k+1)/sides,0),((k+1)/sides,1),(k/sides,1)],5)
 for i in range(7):
  r=random.Random(920+i);base=Vector((r.uniform(-1.7,1.7),0,.56));tip=base+Vector((r.uniform(-.7,.7),(-1 if i%2 else 1)*r.uniform(.65,1.45),r.uniform(.0,.42)));m.tube([base,base.lerp(tip,.6)+Vector((0,0,.1)),tip],[.10,.07,.014],[7,5,4][lod],4)
 # Set lowest point at the ground contact pivot.
 low=min(v.z for v in m.v);m.v=[v-Vector((0,0,low)) for v in m.v];return m

def grass(lod):
 m=Mesh()
 for i in range([132,76,35][lod]):
  r=random.Random(1700+i);a=i*2.4;rad=r.uniform(.015,.29);p=Vector((math.cos(a)*rad,math.sin(a)*rad,0));height=r.uniform(.42,1.13);lean=r.uniform(.12,.55);side=Vector((-math.sin(a),math.cos(a),0));segments=[7,5,3][lod]
  rows=[]
  for j in range(segments+1):
   t=j/segments;pos=p+Vector((math.cos(a)*lean*t*t,math.sin(a)*lean*t*t,height*(t-.22*t*t*t)));w=.0065*(1-t)*r.uniform(.8,1.2);rows.append([pos-side*w,pos+Vector((0,0,w*.8)),pos+side*w])
  for j in range(segments):
   for k in range(2):
    pts=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]];uv=[(k*.5,j/segments),((k+1)*.5,j/segments),((k+1)*.5,(j+1)/segments),(k*.5,(j+1)/segments)]
    if j==segments-1:pts.pop(2);uv.pop(2)
    m.face(pts,uv,3 if i%5 else 6)
 for i in range([18,11,6][lod]):
  r=random.Random(1820+i);ang=i*2.4;p=Vector((math.cos(ang)*.16,math.sin(ang)*.16,0));tip=p+Vector((math.cos(ang)*.25,math.sin(ang)*.25,r.uniform(1.0,1.42)));m.tube([p,p.lerp(tip,.55),tip],[.0028,.002,.0008],3,6)
  for k in range([9,6,4][lod]):
   start=tip-Vector((0,0,k*.018));d=Vector((math.cos(k*2.4)*.6,math.sin(k*2.4)*.6,.8));m.leaf(start,d,.065,.009,6)
 return m
ids=['MatureOak_A','SilverBirch_A','FallenHollowLog_A','TallMeadowGrass_A'];manifest={'schemaVersion':1,'revision':'woodland-05','license':'CC0-1.0','generatorLicense':'MIT','units':'metres','pivot':'ground contact, Z-up authoring / Unity Y-up export','assets':[]};originals=[]
for kind in ids:
 obs=[]
 for lod in range(3):
  m=tree(kind,lod) if kind in ids[:2] else log(lod) if kind==ids[2] else grass(lod);obs.append(m.object(kind+'_LOD'+str(lod)))
 bpy.ops.object.select_all(action='DESELECT')
 for ob in obs:ob.select_set(True)
 path=out/'Models'/f'{kind}.fbx';bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',use_tspace=True,mesh_smooth_type='FACE',path_mode='STRIP',apply_scale_options='FBX_SCALE_UNITS')
 manifest['assets'].append({'id':kind,'model':'Models/'+path.name,'triangles':[len(o.data.polygons) for o in obs],'boundsBlenderZUp':[[min(v.co[k] for v in obs[0].data.vertices) for k in range(3)],[max(v.co[k] for v in obs[0].data.vertices) for k in range(3)]],'sha256':hashlib.sha256(path.read_bytes()).hexdigest()});originals.extend(obs)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
checks=[]
for index,kind in enumerate(ids):
 bpy.ops.import_scene.fbx(filepath=str(out/'Models'/f'{kind}.fbx'))
 for ob in list(bpy.context.selected_objects):
  if ob.type!='MESH':continue
  lod=int(ob.name.split('_LOD')[1][0]);ob.data.materials.clear();ob.data.materials.append(mat)
  assert ob.data.uv_layers and all(len(p.vertices)==3 and p.area>1e-12 for p in ob.data.polygons),(kind,lod,'degenerate')
  assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
  ob.data.calc_tangents();assert all(math.isfinite(c) for loop in ob.data.loops for c in loop.tangent)
  checks.append({'id':kind,'lod':lod,'triangles':len(ob.data.polygons),'nondegenerate':True,'finiteTangents':True,'uv':True})
  ob.hide_render=lod!=0;ob.hide_set(lod!=0);ob.location.x=[-5,5,-3,4][index];ob.location.y=0 if index<2 else -5
manifest['exportReimportChecks']=checks;(out/'woodland-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Neutral inspection stage, daylight with diffuse sky fill; export geometry only.
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.name='Review stage';ground.location.z=-.02;gm=bpy.data.materials.new('Neutral earth');gm.diffuse_color=(.11,.125,.09,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(17,-28,15));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,4.8))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=22;bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-8,-10,18));bpy.context.object.data.energy=4500;bpy.context.object.data.size=9
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.55,.65,.8,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.35
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.view_settings.view_transform='AgX';scene.render.resolution_x=1500;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for im in images:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(review.parent/'Woodland05.blend'));scene.render.filepath=str(review/'woodland-assets.png');bpy.ops.render.render(write_still=True)
for kind,target,loc,scale in [('MatureOak_A',(-5,0,4),(-14,-16,10),13),('SilverBirch_A',(5,0,5),(14,-16,11),13),('FallenHollowLog_A',(-3,-5,.4),(-9,-12,4),6),('TallMeadowGrass_A',(4,-5,.6),(6,-8,1.9),1.9)]:
 camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale;scene.render.resolution_x=1100;scene.render.resolution_y=1000;scene.render.filepath=str(review/f'{kind}-detail.png');bpy.ops.render.render(write_still=True)
print('WOODLAND_VERIFIED',json.dumps(manifest))
