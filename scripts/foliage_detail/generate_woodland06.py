#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Original CC0 woodland 06. Blender source meshes and textures; CC0 bark scan; no private reference inputs."""
import argparse, hashlib, json, math, random, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector, Quaternion

p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--review',required=True);p.add_argument('--skip-review-renders',action='store_true')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.output).resolve();review=Path(a.review).resolve()
for d in (out/'Models',out/'Textures',review):d.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Each 512x1024 island has its own bounded height field and normal derivatives.
N=2048;H=N//2;W=N//4;y,x=np.mgrid[0:H,0:W];u=x/(W-1);v=y/(H-1)
colors=np.ones((N,N,4),dtype=np.float32);normal=colors.copy();packed=np.zeros_like(colors)
rng=np.random.default_rng(60915);fine=rng.normal(0,1,(H,W));low=np.sin(u*39+np.sin(v*17))*np.sin(v*43+u*7)
for tile in range(8):
 if tile==0:
  # Irregular longitudinal plates, branching fissures and shorter cross checks.
  warp=u+(.013*np.sin(v*15)+.006*np.sin(v*61+u*14))
  ridges=np.abs(np.sin(warp*math.pi*26+np.sin(v*34+u*7)*.6))
  splits=np.exp(-(ridges/.13)**2)
  checks=np.exp(-(np.sin(v*math.pi*31+np.sin(u*39)*1.7)/.085)**2)*(.3+.7*(np.sin(u*112)>0))
  field=.69+.20*ridges-.27*splits-.13*checks+.055*low+.032*fine
  rgb=np.stack((field*.46,field*.405,field*.325),axis=-1)
  lichen=np.clip((np.sin(u*51+v*19)*np.sin(v*41-u*23)-.56)*2,0,1)*.28
  rgb=rgb*(1-lichen[:,:,None])+np.array([.45,.47,.36])*lichen[:,:,None]
  height=field*.45;rough=.90
 elif tile==1:
  field=.88+.035*low+.012*fine
  rgb=np.stack((field*.86,field*.84,field*.77),axis=-1)
  scars=np.zeros_like(u)
  rr=random.Random(801)
  for i in range(200):
   cx=rr.random();cy=rr.random();sx=rr.uniform(.004,.075);sy=rr.uniform(.0006,.003)
   scars=np.maximum(scars,np.exp(-(((u-cx)/sx)**4+((v-cy+.003*np.sin(u*38))/sy)**2)))
  tears=np.clip((np.sin(u*44+np.sin(v*14))*np.sin(v*23+u*16)-.45)*3,0,1)*(1-v)**3
  scars=np.maximum(scars,tears);rgb*=1-.88*scars[:,:,None];height=field*.10-scars*.11;rough=.83
 else:
  palettes=[(.21,.36,.075),(.25,.39,.085),(.17,.30,.058),(.31,.40,.12),(.22,.345,.08),(.29,.37,.10)]
  main=np.exp(-((u-.5)/.009)**2)
  veins=np.exp(-(np.sin((v*7-np.abs(u-.5)*3.1)*math.pi)/.10)**2)
  field=.76+.15*np.sin(v*math.pi)+.045*low+.017*fine
  rgb=field[:,:,None]*np.array(palettes[tile-2]);rgb+=main[:,:,None]*np.array([.08,.075,.024])+veins[:,:,None]*.012
  height=main*.02+veins*.009+fine*.0004;rough=.69
 row,col=tile//4,tile%4;ys=slice(row*H,(row+1)*H);xs=slice(col*W,(col+1)*W)
 colors[ys,xs,:3]=np.clip(rgb,0,1)
 dy,dx=np.gradient(height);norm=np.stack((-dx*35,-dy*35,np.ones_like(dx)),axis=-1);norm/=np.linalg.norm(norm,axis=-1)[:,:,None]
 normal[ys,xs,:3]=norm*.5+.5;packed[ys,xs,3]=1-rough
# Verified CC0 scan supplies the oak island; birch and leaves remain original authored textures.
source_root=review.parent/'Sources'
source_receipt=json.loads((source_root/'sources.json').read_text())
for source in source_receipt['files']:
 path=source_root/source['file'];assert hashlib.sha256(path.read_bytes()).hexdigest()==source['sha256']
 im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';im.scale(W,H);arr=np.array(im.pixels[:],dtype=np.float32).reshape(H,W,4)
 if '_diff_' in path.name:colors[:H,:W,:3]=arr[:,:,:3]
 elif '_nor_gl_' in path.name:normal[:H,:W,:3]=arr[:,:,:3]
 else:packed[:H,:W,3]=1-arr[:,:,0]
 bpy.data.images.remove(im)
images=[]
for name,array,linear in [('Woodland06Atlas',colors,False),('Woodland06Normal',normal,True),('Woodland06Mask',packed,True)]:
 im=bpy.data.images.new(name,width=N,height=N,alpha=True)
 if linear:im.colorspace_settings.name='Non-Color'
 im.pixels.foreach_set(array.ravel());im.filepath_raw=str(out/'Textures'/f'{name}.png');im.file_format='PNG';im.save();images.append(im)
mat=bpy.data.materials.new('OriginalWoodland06');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.79
for i,im in enumerate(images[:2]):
 node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
 if i==0:mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
 else:
  nm=mat.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.65;mat.node_tree.links.new(node.outputs['Color'],nm.inputs['Color']);mat.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])

class Mesh:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def face(self,pts,uv,t):
  start=len(self.v);self.v.extend(pts);self.f.append(tuple(range(start,start+len(pts))));self.uv.extend([((t%4+.025+.95*s)/4,(t//4+.015+.97*q)/2) for s,q in uv])
 def tube(self,pts,radii,sides,t,cap=False,flare=False):
  rings=[];dist=[0]
  for j in range(1,len(pts)):dist.append(dist[-1]+(pts[j]-pts[j-1]).length)
  for j,(pos,r) in enumerate(zip(pts,radii)):
   axis=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();side=axis.cross(Vector((0,1,0)))
   if side.length<.1:side=axis.cross(Vector((1,0,0)))
   side.normalize();up=axis.cross(side)
   rings.append([pos+(side*math.cos(k*math.tau/sides)+up*math.sin(k*math.tau/sides))*r*(1+.035*math.sin(k*13+j*.7)+(.16*math.cos(k*math.tau/sides*5+j*.06)*math.exp(-max(pos.z,0)*2) if flare else 0)) for k in range(sides)])
  for j in range(len(pts)-1):
   for k in range(sides):
    n=(k+1)%sides;self.face([rings[j][k],rings[j][n],rings[j+1][n],rings[j+1][k]],[(k/sides,dist[j]/dist[-1]),((k+1)/sides,dist[j]/dist[-1]),((k+1)/sides,dist[j+1]/dist[-1]),(k/sides,dist[j+1]/dist[-1])],t)
  if cap:
   for idx,flip in ((0,True),(-1,False)):
    for k in range(sides):
     pts2=[pts[idx],rings[idx][k],rings[idx][(k+1)%sides]];uv=[(.5,.5),(.5+.4*math.cos(k*math.tau/sides),.5+.4*math.sin(k*math.tau/sides)),(.5+.4*math.cos((k+1)*math.tau/sides),.5+.4*math.sin((k+1)*math.tau/sides))]
     if flip:pts2.reverse();uv.reverse()
     self.face(pts2,uv,t)
 def leaf(self,p,d,length,width,roll,t,lod,birch):
  d.normalize();side=d.cross(Vector((0,0,1)))
  if side.length<.01:side=d.cross(Vector((0,1,0)))
  side.normalize();side=Quaternion(d,roll)@side;normal=side.cross(d).normalized()
  steps=4 if lod==0 and not birch else 3
  rows=[]
  for k in range(steps+1):
   f=k/steps;w=width*math.sin(math.pi*f)**(.82 if birch else .7)*(1 if birch else (1 if k%2 else .72));mid=p+d*length*f+normal*length*(.10*math.sin(math.pi*f)-.13*f*f)
   rows.append([mid-side*w,mid+normal*w*.2,mid+side*w])
  for j in range(steps):
   for k in range(2):
    pts=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]];uv=[(k*.5,j/steps),((k+1)*.5,j/steps),((k+1)*.5,(j+1)/steps),(k*.5,(j+1)/steps)]
    if j==0:pts.pop(0);uv.pop(0)
    if j==steps-1:pts.pop(2);uv.pop(2)
    self.face(pts,uv,t)
 def object(self,name):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();mesh.materials.append(mat);uv=mesh.uv_layers.new(name='UVMap')
  for poly in mesh.polygons:
   for li in poly.loop_indices:uv.data[li].uv=self.uv[mesh.loops[li].vertex_index]
  ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001);bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
  for poly in mesh.polygons:poly.use_smooth=True
  mod=ob.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name);mesh.calc_tangents();ob.select_set(False);return ob

def curved(start,end,bend,n=7):return [start.lerp(end,j/(n-1))+bend*math.sin(math.pi*j/(n-1)) for j in range(n)]
def tree(birch,lod):
 m=Mesh();sprigs=Mesh();seed=815 if birch else 224;bark=1 if birch else 0
 def branch(pts,rad,sides=None,cap=True,flare=False):(m if rad>=.1 else sprigs).tube(pts,[max(.002,rad*(1-j/(len(pts)-1))**1.1) for j in range(len(pts))],sides or [8,6,4][lod],bark,cap,flare)
 # A closed underground collar is deliberately retained for slope-fitting by the placement tool.
 zs=[-.45,-.12,0,.15,.4,.8,1.4,2.0,2.7,3.3,3.8,4.2] if not birch else [-.45,-.12,0,.15,.4,.8,1.4,2.2,3.4,4.8,6.4,8.1,9.8,10.6]
 pts=[Vector((.07*math.sin(z*1.1)+z*.022,.045*math.sin(z*.8),z)) for z in zs]
 radii=([.40,.52,.60,.54,.43,.38,.34,.30,.255,.21,.13,.025] if not birch else [.20,.29,.33,.29,.235,.20,.18,.165,.148,.125,.093,.065,.035,.005])
 m.tube(pts,radii,[24,16,10][lod],bark,True,True)
 for k in range(7 if not birch else 5):
  rr=random.Random(seed+k);ang=k*2.4;reach=rr.uniform(.80,1.22) if not birch else rr.uniform(.43,.68);d=Vector((math.cos(ang),math.sin(ang),0))
  m.tube([d*.12+Vector((0,0,.65)),d*.40+Vector((0,0,.20)),d*reach*.78+Vector((0,0,-.05)),d*reach+Vector((0,0,-.22))],[.24 if not birch else .12,.17 if not birch else .09,.075,.012],[10,7,5][lod],bark,True)
 leaders=[]
 if birch:
  for i in range(24):
   r=random.Random(seed+i);f=i/23;z=2.7+f*7.5;ang=i*2.399+r.uniform(-.7,.7);start=Vector((.07*math.sin(z*1.1)+z*.022,.045*math.sin(z*.8),z));reach=(2.5*(1-f)**.65+.12)*r.uniform(.80,1.13);end=start+Vector((math.cos(ang)*reach,math.sin(ang)*reach,r.uniform(.45,1.2)))
   path=curved(start,end,Vector((0,0,.45)),7);branch(path,.065*(1-.75*f));leaders.append((path,ang,seed+i))
 else:
  for i in range(9):
   r=random.Random(seed+i);ang=i*2.399+r.uniform(-.5,.5);start=pts[7+i%3];reach=r.uniform(2.25,3.4) if i<7 else r.uniform(.75,1.3);rise=r.uniform(3.4,4.9) if i<7 else 4.8;end=start+Vector((math.cos(ang)*reach,math.sin(ang)*reach,rise));path=curved(start,end,Vector((math.cos(ang)*.4,math.sin(ang)*.4,-.22)),9);branch(path,.215*(1-i*.055));
   for j in range(6):
    f=.24+j*.135;idx=min(len(path)-2,int(f*(len(path)-1)));base=path[idx].lerp(path[idx+1],f*(len(path)-1)-idx);a2=ang+(-1 if j%2 else 1)*r.uniform(.55,1.35);reach2=r.uniform(1.0,1.8)*(1-.25*f);end2=base+Vector((math.cos(a2)*reach2,math.sin(a2)*reach2,r.uniform(.6,1.55)));p2=curved(base,end2,Vector((0,0,.18)),5);branch(p2,.058*(1-.4*f));leaders.append((p2,a2,seed*20+i*10+j))
 for path,ang,spray_seed in leaders:
  r=random.Random(spray_seed)
  for j in range(7 if not birch else 11):
   f=.30+j*(.095 if not birch else .062);idx=min(len(path)-2,int(f*(len(path)-1)));base=path[idx].lerp(path[idx+1],f*(len(path)-1)-idx);a2=ang+(-1 if j%2 else 1)*r.uniform(.6,1.6)
   length=r.uniform(.43,.95) if not birch else r.uniform(.65,1.35);delta=Vector((math.cos(a2)*length,math.sin(a2)*length,r.uniform(-.1,.55) if not birch else r.uniform(-.85,-.25)));tip=base+delta
   twig=curved(base,tip,Vector((0,0,.08 if not birch else .24)),4)
   if lod<2:branch(twig,.012 if not birch else .008,[5,4,3][lod])
   leafcount=19 if not birch else 27
   for k in range(leafcount):
    # Every LOD samples the same deterministic leaf anchors before compensating its area.
    rr=random.Random(spray_seed*10000+j*100+k)
    if lod==1 and k%2:continue
    if lod==2 and k%5:continue
    t=.12+.88*k/(leafcount-1);idx=min(2,int(t*3));lp=twig[idx].lerp(twig[idx+1],t*3-idx);a3=a2+k*2.399+rr.uniform(-.5,.5)
    d=Vector((math.cos(a3),math.sin(a3),rr.uniform(-.65,.65)));scale=[1,1.32,1.8][lod];length=rr.uniform(.13,.21) if not birch else rr.uniform(.085,.14)
    lp+=d*.022;sprigs.leaf(lp,d,length*scale,length*(.29 if not birch else .37)*scale,rr.uniform(-1.15,1.15),2+rr.randrange(6),lod,birch)
 return m,sprigs

def tree_object(birch,lod,name):
 wood,sprigs=tree(birch,lod);source=wood.object('WoodUvSource');ob=source.copy();ob.data=source.data.copy();bpy.context.collection.objects.link(ob);ob.name=name
 bpy.context.view_layer.objects.active=ob;ob.select_set(True)
 remesh=ob.modifiers.new('Fused branch collars','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.028 if not birch else .017;remesh.use_smooth_shade=True
 bpy.ops.object.modifier_apply(modifier=remesh.name)
 smooth=ob.modifiers.new('Collar relaxation','SMOOTH');smooth.factor=.75;smooth.iterations=4;bpy.ops.object.modifier_apply(modifier=smooth.name)
 dec=ob.modifiers.new('Bounded woody topology','DECIMATE');dec.ratio=[.32,.14,.05][lod];bpy.ops.object.modifier_apply(modifier=dec.name)
 if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
 transfer=ob.modifiers.new('Preserve longitudinal bark UV','DATA_TRANSFER');transfer.object=source;transfer.use_loop_data=True;transfer.data_types_loops={'UV'};transfer.loop_mapping='POLYINTERP_NEAREST'
 bpy.ops.object.modifier_apply(modifier=transfer.name);bpy.data.objects.remove(source,do_unlink=True);ob.select_set(False)
 foliage=sprigs.object('Fine branches and leaves');ob.select_set(True);foliage.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.join()
 for poly in ob.data.polygons:poly.material_index=0;poly.use_smooth=True
 while len(ob.data.materials)>1:ob.data.materials.pop(index=1)
 tri=ob.modifiers.new('Final triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name);ob.data.calc_tangents();ob.select_set(False);return ob

manifest={'revision':'woodland-06','license':'CC0-1.0','generator':'Scripts/world_assets/foliage_detail/generate_woodland06.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'blender':bpy.app.version_string,'textureSize':N,'sourceTextures':source_receipt,'assets':[]}
ids=['MatureOak_B','SilverBirch_B']
for kind in ids:
 obs=[tree_object('Birch' in kind,lod,kind+f'_LOD{lod}') for lod in range(3)]
 for ob in obs:ob.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];path=out/'Models'/f'{kind}.fbx'
 # Unity assigns the atlas; excluding FBX image bindings prevents absolute texture URLs.
 export_mat=bpy.data.materials.new('Woodland06ExportSlot')
 for ob in obs:ob.data.materials[0]=export_mat
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,use_tspace=True,add_leaf_bones=False,path_mode='STRIP',embed_textures=False)
 for ob in obs:ob.data.materials[0]=mat
 bpy.data.materials.remove(export_mat)
 for ob in obs:ob.select_set(False)
 verts=obs[0].data.vertices;roots=[v.co for v in verts if v.co.z<.7]
 manifest['assets'].append({'id':kind,'model':'Models/'+path.name,'triangles':[len(ob.data.polygons) for ob in obs],'boundsBlenderZUp':[[min(v.co[k] for v in verts) for k in range(3)],[max(v.co[k] for v in verts) for k in range(3)]],'rootFootprintRadius':max(math.hypot(v.x,v.y) for v in roots),'sealedCollarBottom':-.45,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
checks=[]
for idx,kind in enumerate(ids):
 bpy.ops.import_scene.fbx(filepath=str(out/'Models'/f'{kind}.fbx'))
 for ob in list(bpy.context.selected_objects):
  if ob.type!='MESH':continue
  lod=int(ob.name.split('_LOD')[1][0]);ob.data.materials.clear();ob.data.materials.append(mat)
  assert ob.data.uv_layers and all(len(p.vertices)==3 and p.area>1e-12 for p in ob.data.polygons),(kind,lod,'degenerate')
  assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
  ob.data.calc_tangents();assert all(math.isfinite(c) for loop in ob.data.loops for c in loop.tangent)
  checks.append({'id':kind,'lod':lod,'triangles':len(ob.data.polygons),'nondegenerate':True,'finiteTangents':True,'uv':True});ob.hide_render=lod!=0;ob.hide_set(lod!=0);ob.location.x=-5.8 if idx==0 else 5.8
manifest['exportReimportChecks']=checks;manifest['textures']=[{'file':'Textures/'+im.name+'.png','sha256':hashlib.sha256((out/'Textures'/f'{im.name}.png').read_bytes()).hexdigest()} for im in images]
(out/'woodland06-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.location.z=-.03;gm=bpy.data.materials.new('Review ground');gm.diffuse_color=(.11,.125,.09,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(18,-29,13));camera=bpy.context.object;camera.data.type='ORTHO';bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-10,-10,18));bpy.context.object.data.energy=5500;bpy.context.object.data.size=8
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.65,.74,.88,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.45
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24;scene.render.threads_mode='FIXED';scene.render.threads=5;scene.view_settings.view_transform='AgX';scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
for im in images:
 im.pack();im.filepath='//../../asset-catalog/Assets/BFjord/OriginalFoliage/Textures/'+im.name+'.png'
for name,target,loc,scale in [('woodland-assets',(0,0,5),(17,-29,13),24),('MatureOak_B-detail',(-5.8,0,4.3),(-15,-18,10),13.7),('SilverBirch_B-detail',(5.8,0,5.4),(13,-17,10),15.4),('oak-bark',(-5.8,0,1),(-8.3,-4.5,2.4),2.8),('birch-bark',(5.8,0,1.3),(7,-3.5,2.3),2.5),('oak-leaves',(-8,-1,6),(-10,-7,8),2.5)]:
 camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale;scene.render.filepath=str(review/f'{name}.png')
 if name=='woodland-assets':bpy.ops.wm.save_as_mainfile(filepath=str(review.parent/'Woodland06.blend'))
 if not a.skip_review_renders:bpy.ops.render.render(write_still=True)
print('WOODLAND06_VERIFIED',json.dumps(manifest))
