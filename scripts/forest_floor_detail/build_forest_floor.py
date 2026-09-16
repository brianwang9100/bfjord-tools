#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Reproducible original ForestFloorDetail 07; Blender 5.x, no add-ons.
Only verified CC0 bark pixels are reused. Private visual references are never inputs.
"""
import argparse, hashlib, json, math, random, shutil, sys
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Vector

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools'):
            if candidate.is_dir():return candidate
    raise FileNotFoundError('Could not find art/BFjordTools or assets/BFjordTools above '+str(script_file))

ART=find_art_root(__file__)
OUT=ART/'ForestFloorDetail'
CAT=ART/'asset-catalog/Assets/BFjord/ForestFloorDetail'
for d in [OUT/'Sources',OUT/'Review',CAT/'Models',CAT/'Textures']:d.mkdir(parents=True,exist_ok=True)
p=argparse.ArgumentParser();p.add_argument('--skip-renders',action='store_true');p.add_argument('--only');a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
N=2048;T=512;y,x=np.mgrid[0:T,0:T];u=x/(T-1);v=y/(T-1)
col=np.ones((N,N,4),np.float32);nor=col.copy();mask=np.zeros_like(col)
rng=np.random.default_rng(7128);grain=rng.normal(0,1,(T,T));broad=sum(np.sin(u*ax+v*ay+np.sin(v*by+phase)*1.8+phase) for ax,ay,by,phase in [(13,9,11,.7),(21,-17,7,2.4),(-9,29,17,4.1),(37,11,9,1.2)])/3;fine=sum(np.sin(u*ax+v*ay+np.sin(v*by+phase)+phase) for ax,ay,by,phase in [(83,53,31,.4),(113,-71,43,3),(-47,137,19,2)])/3
pal=[(.25,.18,.105),(.35,.17,.047),(.51,.30,.082),(.26,.155,.062),(.64,.40,.12),(.14,.12,.066),(.29,.115,.037),(.75,.65,.42),(.80,.45,.07),(.76,.55,.19),(.17,.095,.041),(.23,.29,.065),(.34,.245,.15),(.23,.095,.033),(.43,.37,.24),(.47,.27,.062)]
for t in range(16):
 field=.85+.13*broad+.075*fine+.038*grain;h=field*.055;rough=.86
 rgb=field[:,:,None]*np.array(pal[t])
 if t in (1,2,3,4):
  mid=np.exp(-((u-.5)/.008)**2);vein=np.exp(-(np.sin((v*7-np.abs(u-.5)*3.4)*math.pi)/.075)**2)
  necrosis=np.clip((broad+.5*fine-.55)*1.4,0,1);rgb*=1-.45*necrosis[:,:,None]
  rgb+=mid[:,:,None]*np.array([.10,.065,.022])+vein[:,:,None]*.029;h+=mid*.02+vein*.009
 elif t in (6,8):
  r=np.sqrt((u-.5)**2+(v-.5)**2)*2;edge=np.clip((r-.83)*10,0,1)
  rgb=rgb*(1-edge[:,:,None]*.50)+np.array([.71,.60,.36])*edge[:,:,None]*.50
  if t==6:
   cracks=np.exp(-(np.sin(u*45+np.sin(v*24)*2)/.08)**2)*np.clip(broad+.2,0,1);rgb*=1-.25*cracks[:,:,None];h-=cracks*.012
  rough=.72
 elif t in (7,9):
  striation=np.sin(u*math.pi*83+v*4)*.5+.5;rgb*=.88+.12*striation[:,:,None];h+=striation*.013
 elif t in (10,15):
  r=np.sqrt((u-.5)**2+(v-.5)**2);bands=.5+.5*np.sin(r*95+np.sin(u*19)*1.4);rgb*=.65+.35*bands[:,:,None];h+=bands*.05
 elif t==11:
  tufts=np.clip(broad*.6+fine*.4,0,1);rgb+=tufts[:,:,None]*np.array([.08,.12,.009]);h+=tufts*.08
 elif t in (12,14):
  fibres=np.sin(u*253+np.sin(v*17)*4)*.5+.5;rgb*=.78+.22*fibres[:,:,None];h+=fibres*.045
 ys=slice((t//4)*T,(t//4+1)*T);xs=slice((t%4)*T,(t%4+1)*T)
 col[ys,xs,:3]=np.clip(rgb,0,1);dy,dx=np.gradient(h);normal=np.stack((-dx*25,-dy*25,np.ones_like(dx)),axis=-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None];nor[ys,xs,:3]=normal*.5+.5;mask[ys,xs,3]=1-rough
source=OUT/'Sources' if (OUT/'Sources/sources.json').exists() else ART/'FoliageDetail/Woodland06/Sources';receipt=json.loads((source/'sources.json').read_text())
for f in receipt['files']:
 path=source/f['file'];assert hashlib.sha256(path.read_bytes()).hexdigest()==f['sha256']
 if path.resolve()!=(OUT/'Sources'/path.name).resolve():shutil.copy2(path,OUT/'Sources'/path.name)
 im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';im.scale(T,T);ar=np.array(im.pixels[:],np.float32).reshape(T,T,4)
 if '_diff_' in path.name:col[:T,:T,:3]=ar[:,:,:3]
 elif '_nor_' in path.name:nor[:T,:T,:3]=ar[:,:,:3]
 else:mask[:T,:T,3]=1-ar[:,:,0]
 bpy.data.images.remove(im)
(OUT/'Sources/sources.json').write_text(json.dumps(receipt,indent=2)+'\n')
images=[]
for name,array,linear in [('ForestFloor07Atlas',col,False),('ForestFloor07Normal',nor,True),('ForestFloor07Mask',mask,True)]:
 im=bpy.data.images.new(name,width=N,height=N,alpha=True)
 if linear:im.colorspace_settings.name='Non-Color'
 im.pixels.foreach_set(array.ravel());im.filepath_raw=str(CAT/'Textures'/f'{name}.png');im.file_format='PNG';im.save();images.append(im)
mat=bpy.data.materials.new('ForestFloor07');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.85
for k,im in enumerate(images[:2]):
 n=nodes.new('ShaderNodeTexImage');n.image=im
 if k==0:links.new(n.outputs['Color'],bs.inputs['Base Color'])
 else:
  nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.7;links.new(n.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
class Mesh:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def face(self,pts,uv,t):
  # Filter coincident ends before triangulation (leaf tips, cap centres).
  pp=[];uu=[]
  for q,tex in zip(pts,uv):
   if not pp or (Vector(q)-Vector(pp[-1])).length>1e-7:pp.append(q);uu.append(tex)
  if len(pp)>2 and (Vector(pp[0])-Vector(pp[-1])).length<1e-7:pp.pop();uu.pop()
  if len(pp)<3:return
  start=len(self.v);self.v.extend(pp);self.f.append(tuple(range(start,start+len(pp))));self.uv.extend([((t%4+.018+.964*s)/4,(t//4+.018+.964*q)/4) for s,q in uu])
 def tube(self,pts,radii,sides,t,cap=True,ridge=.07):
  pts=[Vector(q) for q in pts];rings=[]
  for j,(pos,r) in enumerate(zip(pts,radii)):
   axis=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();side=axis.cross(Vector((0,1,0)))
   if side.length<.05:side=axis.cross(Vector((1,0,0)))
   side.normalize();up=axis.cross(side)
   rings.append([pos+(side*math.cos(k*math.tau/sides)+up*math.sin(k*math.tau/sides))*r*(1+ridge*math.sin(k*math.tau/sides*7+j*.25)) for k in range(sides)])
  for j in range(len(pts)-1):
   for k in range(sides):
    n=(k+1)%sides;self.face([rings[j][k],rings[j][n],rings[j+1][n],rings[j+1][k]],[(k/sides,j/(len(pts)-1)),((k+1)/sides,j/(len(pts)-1)),((k+1)/sides,(j+1)/(len(pts)-1)),(k/sides,(j+1)/(len(pts)-1))],t)
  if cap:
   for j,flip in [(0,True),(-1,False)]:
    for k in range(sides):
     pts2=[pts[j],rings[j][k],rings[j][(k+1)%sides]];uv=[(.5,.5),(k/sides,.1),((k+1)/sides,.1)]
     if flip:pts2.reverse();uv.reverse()
     self.face(pts2,uv,t)
 def leaf(self,base,angle,length,width,curl,t,lod,oak=True):
  d=Vector((math.cos(angle),math.sin(angle),0));s=Vector((-d.y,d.x,0));base=Vector(base);steps=([24,12,6] if oak else [12,8,4])[lod];rows=[]
  for j in range(steps+1):
   f=j/steps;w=width*math.sin(math.pi*f)**.72
   w*=((.75+.25*math.cos(f*math.pi*10)) if oak else (1+.055*math.sin(f*math.pi*24)))
   mid=base+d*length*f+Vector((0,0,length*(curl*f*f+.06*math.sin(f*math.pi))))
   rows.append([mid+s*w*side+Vector((0,0,width*(abs(side)**1.4*.25+side*.09*math.sin(f*11)))) for side in [-1,0,1]])
  for j in range(steps):
   for k in range(2):
    q=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]];uv=[(k/2,j/steps),((k+1)/2,j/steps),((k+1)/2,(j+1)/steps),(k/2,(j+1)/steps)]
    self.face(q,uv,t);self.face([v-Vector((0,0,.0007)) for v in reversed(q)],list(reversed(uv)),t)
  if lod<2:self.tube([base-d*.018,base+d*length*.25+Vector((0,0,.002)),rows[steps//2][1]+Vector((0,0,.001))],[.0017,.0012,.0004],4,12)
 def object(self,name):
  me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update();me.materials.append(mat);uv=me.uv_layers.new(name='UVMap')
  for poly in me.polygons:
   for li in poly.loop_indices:uv.data[li].uv=self.uv[me.loops[li].vertex_index]
  ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.0000001);bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
  tri=ob.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
  for poly in me.polygons:poly.use_smooth=True
  ob.select_set(False);return ob

def root_mesh(lod):
 m=Mesh();r=random.Random(71);sides=[22,14,8][lod]
 # The collar is hollow/open above ground so the fan can be added around a tree trunk.
 for i in range(9):
  an=i*math.tau/9+r.uniform(-.21,.21);d=Vector((math.cos(an),math.sin(an),0));s=Vector((-d.y,d.x,0));length=r.uniform(1.0,1.78);bend=r.uniform(-.25,.25)
  pts=[]
  for j in range(15):
   f=j/14;pts.append(d*(.20+length*f)+s*(math.sin(f*math.pi)*bend+.06*math.sin(f*9+i))+Vector((0,0,.43*(1-f)**3+.055*math.sin(f*math.pi)-.12*f*f)))
  radii=[(.19 if i%3 else .24)*(1-j/14)**1.5+.004 for j in range(15)];m.tube(pts,radii,sides,0,ridge=.14)
  for k in (5,8,10):
   an2=an+(1 if (k+i)%2 else -1)*r.uniform(.35,.85);d2=Vector((math.cos(an2),math.sin(an2),0));start=pts[k];reach=r.uniform(.38,.74);end=start+d2*reach;end.z=-.14
   branch=[start.lerp(end,j/7)+Vector((0,0,.035*math.sin(j/7*math.pi))) for j in range(8)];m.tube(branch,[radii[k]*.68*(1-j/7)**1.2+.002 for j in range(8)],[10,7,5][lod],0)
 return m

def litter(lod,pine=False):
 m=Mesh();r=random.Random(717 if pine else 312)
 if not pine:
  for i in range(86):
   x,y=r.uniform(-.62,.62),r.uniform(-.48,.48);ang=r.random()*math.tau;length=r.uniform(.09,.20);wid=length*r.uniform(.25,.39);curl=r.uniform(-.015,.24);t=1+r.randrange(4)
   if lod==2 and i%2:continue
   m.leaf((x,y,.004+r.random()*.016),ang,length,wid,curl,t,lod,i%3!=0)
 else:
  for i in range(140):
   x,y=r.uniform(-.47,.47),r.uniform(-.37,.37);an=r.random()*math.tau;length=r.uniform(.055,.12);d=Vector((math.cos(an),math.sin(an),0));base=Vector((x,y,.004+r.random()*.009))
   if lod==1 and i%2:continue
   if lod==2 and i%3:continue
   for j in range(2):
    side=Vector((-d.y,d.x,0))*(j*2-1)*.004;m.tube([base,base+d*length*.5+side+Vector((0,0,.003)),base+d*length+side*1.5],[.0011,.001,.00035],[3,3,3][lod],5)
  for i in range(5):cone(m,Vector((r.uniform(-.31,.31),r.uniform(-.25,.25),.025)),r.uniform(.09,.13),r.random()*math.tau,lod)
 for i in range(10 if not pine else 6):
  base=Vector((r.uniform(-.5,.4),r.uniform(-.32,.32),.018));an=r.random()*math.tau;d=Vector((math.cos(an),math.sin(an),.04));length=r.uniform(.16,.36)
  m.tube([base,base+d*length*.45+Vector((0,0,.014)),base+d*length],[.005,.004,.0015],[6,5,4][lod],0)
 return m

def cone(m,base,length,angle,lod):
 axis=Vector((math.cos(angle)*.91,math.sin(angle)*.91,.41));side=Vector((-math.sin(angle),math.cos(angle),0));up=axis.cross(side);r=length*.23
 m.tube([base,base+axis*length],[r*.32,.002],[10,7,5][lod],10)
 tiers=[11,8,5][lod];n=[9,7,5][lod]
 for j in range(tiers):
  f=(j+.4)/tiers;rad=r*math.sin(math.pi*f)**.65
  for k in range(n):
   an=k*math.tau/n+j*2.399;d=side*math.cos(an)+up*math.sin(an);s=side*-math.sin(an)+up*math.cos(an);c=base+axis*length*f+d*rad*.65;w=r*.4;tip=c+d*rad*.60-axis*length*.07
   q=[c-s*w,c+axis*length*.095, c+s*w,tip];uv=[(0,.25),(.5,1),(1,.25),(.5,0)];m.face(q,uv,10);m.face([q[3],q[2],q[0]],[(.5,0),(1,.25),(0,.25)],10)

def mushroom(m,base,scale,kind,lod,seed):
 r=random.Random(seed);base=Vector(base);radius=scale*(.45 if kind=='bolete' else .5);height=scale*(.78 if kind=='bolete' else .94);lean=Vector((r.uniform(-.10,.10),r.uniform(-.08,.08),0))*scale
 m.tube([base,base+Vector((0,0,height*.22))+lean*.2,base+Vector((0,0,height*.7))+lean*.8,base+Vector((0,0,height))+lean],[scale*.13,scale*.155 if kind=='bolete' else scale*.08,scale*.10,scale*.06],[14,10,7][lod],7 if kind=='bolete' else 9)
 center=base+Vector((0,0,height))+lean;radial=[48,30,16][lod];rings=[12,8,5][lod];rows=[]
 for j in range(rings+1):
  f=j/rings;row=[]
  for k in range(radial):
   an=k*math.tau/radial;wave=.035*math.sin(an*5+seed)+.045*math.sin(an*3+seed*.3)+.015*math.sin(an*11)
   rr=radius*f*(1+wave);z=radius*(.40*math.sqrt(max(0,1-f*f)) if kind=='bolete' else (.11+.38*f*f-.08*math.exp(-f*f*15)))
   z+=radius*(.035 if kind=='bolete' else .12)*math.sin(an*6+seed)*f**3
   row.append(center+Vector((rr*math.cos(an),rr*math.sin(an),z)))
  rows.append(row)
 tile=6 if kind=='bolete' else 8
 for j in range(rings):
  for k in range(radial):
   n=(k+1)%radial;q=[rows[j][k],rows[j+1][k],rows[j+1][n],rows[j][n]];uv=[(.5+(j/rings)*.49*math.cos(k*math.tau/radial),.5+(j/rings)*.49*math.sin(k*math.tau/radial)),(.5+((j+1)/rings)*.49*math.cos(k*math.tau/radial),.5+((j+1)/rings)*.49*math.sin(k*math.tau/radial)),(.5+((j+1)/rings)*.49*math.cos(n*math.tau/radial),.5+((j+1)/rings)*.49*math.sin(n*math.tau/radial)),(.5+(j/rings)*.49*math.cos(n*math.tau/radial),.5+(j/rings)*.49*math.sin(n*math.tau/radial))];m.face(q,uv,tile)
 # Curved underside and decurrent chanterelle ridges, with a pale rolled lip.
 for k in range(radial):
  n=(k+1)%radial;inside=center+Vector((0,0,-radius*.045));edge=rows[-1][k];edgen=rows[-1][n]
  for j in range(rings):
   q=[rows[j][k],rows[j][n],rows[j+1][n],rows[j+1][k]]
   m.face([p-Vector((0,0,radius*.052)) for p in q],[(k/radial,j/rings),(n/radial,j/rings),(n/radial,(j+1)/rings),(k/radial,(j+1)/rings)],7 if kind=='bolete' else 9)
  m.face([edge,edge-Vector((0,0,radius*.045)),edgen-Vector((0,0,radius*.045)),edgen],[(k/radial,1),(k/radial,.8),(n/radial,.8),(n/radial,1)],7 if kind=='bolete' else 9)
  if kind!='bolete' and lod<2:
   an=k*math.tau/radial;d=Vector((math.cos(an),math.sin(an),0));start=center+d*radius*.13-Vector((0,0,radius*.14));path=[start]+[rows[j][k]-Vector((0,0,radius*.065)) for j in range(2,rings+1)];m.tube(path,[.0016+(.0007*math.sin(j/(len(path)-1)*math.pi)) for j in range(len(path))],3,9)

def fungal(lod,kind):
 m=Mesh();r=random.Random(581 if kind=='bolete' else 902)
 specs=[(-.11,.015,.26),(.12,.03,.19),(.015,-.11,.14),(-.18,-.09,.10)] if kind=='bolete' else [(-.12,.03,.22),(.055,.08,.24),(.15,-.04,.18),(-.04,-.13,.16),(-.19,-.08,.13),(.04,-.03,.12)]
 for i,(x,y,s) in enumerate(specs):mushroom(m,(x,y,-.018),s,kind,lod,710+i*73)
 for i in range(8):m.leaf((r.uniform(-.22,.18),r.uniform(-.16,.13),.001),r.random()*math.tau,r.uniform(.06,.10),.025,.10,1+i%3,lod,True)
 return m

def deadwood(lod,shelf):
 m=Mesh();r=random.Random(544);pts=[Vector((-.61+j*.145,.03*math.sin(j*1.3),.085+.02*math.sin(j*.8))) for j in range(9)]
 m.tube(pts,[.09,.11,.105,.099,.095,.084,.07,.067,.022],[22,14,8][lod],0,ridge=.19)
 for i in range(4):
  start=pts[2+i];d=Vector((.05,(-1 if i%2 else 1)*r.uniform(.2,.32),r.uniform(.05,.13)));m.tube([start,start+d*.6,start+d],[.035,.02,.006],[10,7,5][lod],12)
 if shelf:
  for i in range(7):
   c=pts[1+i]+Vector((0,-.04,.01+i%2*.028));rad=.09+(i%3)*.027;n=[34,22,12][lod];rings=[7,5,3][lod];rows=[]
   for j in range(rings+1):
    f=j/rings;row=[]
    for k in range(n+1):
     an=math.pi*.12+k/n*math.pi*.79;rr=rad*f*(1+.04*math.sin(an*13+i));row.append(c+Vector((rr*math.cos(an),-rr*math.sin(an),rad*(.12*(1-f)+.075*math.sin(an*7+i)*f*f))))
    rows.append(row)
   for j in range(rings):
    for k in range(n):
     q=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]];uv=[(k/n,j/rings),((k+1)/n,j/rings),((k+1)/n,(j+1)/rings),(k/n,(j+1)/rings)];m.face(q,uv,15 if i%2 else 10)
     m.face([q1-Vector((0,0,.009)) for q1 in reversed(q)],list(reversed(uv)),7)
   for k in range(n):m.face([rows[-1][k],rows[-1][k]-Vector((0,0,.009)),rows[-1][k+1]-Vector((0,0,.009)),rows[-1][k+1]],[(k/n,0),(k/n,1),((k+1)/n,1),((k+1)/n,0)],7)
 else:
  rows=[];n=[22,14,8][lod];cross=[10,7,5][lod]
  for j in range(n+1):
   f=j/n;xx=-.48+.83*f;idx=(xx+.61)/.145;ki=min(7,int(idx));cc=pts[ki].lerp(pts[ki+1],idx-ki);rad=[.09,.11,.105,.099,.095,.084,.07,.067,.022][ki];row=[]
   for k in range(cross+1):
    an=(k/cross-.5)*1.8*math.sin(math.pi*f)**.35;rr=rad+.003+.002*math.sin(j*2.7+k*1.8);row.append(cc+Vector((0,math.sin(an)*rr,math.cos(an)*rr)))
   rows.append(row)
  for j in range(n):
   for k in range(cross):m.face([rows[j][k],rows[j+1][k],rows[j+1][k+1],rows[j][k+1]],[(j/n,k/cross),((j+1)/n,k/cross),((j+1)/n,(k+1)/cross),(j/n,(k+1)/cross)],11)
  for i in range([230,120,40][lod]):
   x=r.uniform(-.46,.35);y=r.uniform(-.065,.065);z=.18-.09*(y/.09)**2;an=r.random()*math.tau
   base=Vector((x,y,z));d=Vector((math.cos(an),math.sin(an),.35));side=Vector((-d.y,d.x,0));length=r.uniform(.018,.045)
   m.face([base-side*.004,base+d*length*.5+Vector((0,0,.004)),base+side*.004,base+d*length],[(0,0),(.5,.5),(1,0),(.5,1)],11)
 return m

ids=['ExposedRootFan_A','OakBirchLeafLitter_A','PineLitterCones_A','BrownBoleteCluster_A','GoldenChanterelleCluster_A','ShelfFungiDeadwood_A','MossyFallenBranch_A']
manifest={'revision':'forest-floor-07','license':'CC0-1.0','generator':'Scripts/world_assets/forest_floor_detail/build_forest_floor.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'blender':bpy.app.version_string,'textureSize':N,'coordinateSystem':'Unity Y up, metres; Blender source Z up','sourceTextures':receipt,'assets':[]}
for kind in ids:
 if a.only and a.only!=kind:continue
 obs=[]
 for lod in range(3):
  m=root_mesh(lod) if kind.startswith('Exposed') else litter(lod,kind.startswith('Pine')) if 'Litter' in kind else fungal(lod,'bolete' if kind.startswith('Brown') else 'chanterelle') if 'Cluster' in kind else deadwood(lod,kind.startswith('Shelf'))
  ob=m.object(kind+f'_LOD{lod}');obs.append(ob)
  if kind.startswith('Exposed'):
   source=ob.copy();source.data=ob.data.copy();bpy.context.collection.objects.link(source);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
   remesh=ob.modifiers.new('Organic fused root forks','REMESH');remesh.mode='VOXEL';remesh.voxel_size=[.014,.021,.034][lod];remesh.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=remesh.name)
   smooth=ob.modifiers.new('Root growth blending','SMOOTH');smooth.factor=.75;smooth.iterations=3;bpy.ops.object.modifier_apply(modifier=smooth.name)
   dec=ob.modifiers.new('Root silhouette LOD','DECIMATE');dec.ratio=[.15,.14,.14][lod];bpy.ops.object.modifier_apply(modifier=dec.name)
   if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
   tr=ob.modifiers.new('Longitudinal bark coordinates','DATA_TRANSFER');tr.object=source;tr.use_loop_data=True;tr.data_types_loops={'UV'};tr.loop_mapping='POLYINTERP_NEAREST';bpy.ops.object.modifier_apply(modifier=tr.name);bpy.data.objects.remove(source,do_unlink=True)
   tri=ob.modifiers.new('Root triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name);ob.select_set(False)
 for ob in obs:
  bm=bmesh.new();bm.from_mesh(ob.data);bad=[f for f in bm.faces if f.calc_area()<1e-11]
  if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
  bm.to_mesh(ob.data);bm.free();ob.data.update();ob.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];path=CAT/'Models'/f'{kind}.fbx';slot=bpy.data.materials.new('ForestFloor07ExportSlot')
 for ob in obs:ob.data.materials[0]=slot
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,use_tspace=True,add_leaf_bones=False,path_mode='STRIP',embed_textures=False)
 for ob in obs:ob.data.materials[0]=mat;ob.select_set(False);ob.hide_render=True;ob.hide_set(True)
 bpy.data.materials.remove(slot);verts=obs[0].data.vertices;low=[min(v.co[k] for v in verts) for k in range(3)];high=[max(v.co[k] for v in verts) for k in range(3)]
 manifest['assets'].append({'id':kind,'model':'Models/'+path.name,'triangles':[len(ob.data.polygons) for ob in obs],'boundsBlenderZUp':[low,high],'boundsUnityYUp':[[low[0],low[2],-high[1]],[high[0],high[2],-low[1]]],'rootFootprintRadius':max(math.hypot(v.co.x,v.co.y) for v in verts),'groundAnchor':[0,0,0],'placement':'Terrain-fit rigid local tangent; root tips are buryable below local Y=0.' if kind.startswith('Exposed') else 'Local Y=0 rests on terrain; fit local tangent. Keep mushrooms vertical on mild slopes.','sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
 print('BUILT',kind,manifest['assets'][-1]['triangles'],flush=True)
# Exported FBX, not working meshes, supplies every review view.
bpy.ops.object.select_all(action='DESELECT')
for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
checks=[];review_objects={}
for asset in manifest['assets']:
 kind=asset['id'];bpy.ops.import_scene.fbx(filepath=str(CAT/asset['model']));review_objects[kind]=[]
 for ob in list(bpy.context.selected_objects):
  if ob.type!='MESH':continue
  lod=int(ob.name.split('_LOD')[1][0]);ob.data.materials.clear();ob.data.materials.append(mat);ob.data.calc_tangents()
  finite=all(math.isfinite(c) for v in ob.data.vertices for c in v.co);tangent=all(math.isfinite(c) for loop in ob.data.loops for c in loop.tangent);bad=sum(p.area<1e-12 for p in ob.data.polygons)
  assert finite and tangent and ob.data.uv_layers and bad==0,(kind,lod,bad)
  checks.append({'id':kind,'lod':lod,'triangles':len(ob.data.polygons),'finiteVertices':finite,'finiteTangents':tangent,'zeroAreaTriangles':bad,'uv':True});ob.hide_render=True;ob.hide_set(True);review_objects[kind].append(ob)
manifest['exportReimportChecks']=checks;manifest['textures']=[{'file':'Textures/'+im.name+'.png','bytes':(CAT/'Textures'/f'{im.name}.png').stat().st_size,'sha256':hashlib.sha256((CAT/'Textures'/f'{im.name}.png').read_bytes()).hexdigest()} for im in images]
(CAT/'forest-floor07-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.location.z=-.021;gm=bpy.data.materials.new('Review neutral charcoal earth');gm.diffuse_color=(.048,.057,.035,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';scene=bpy.context.scene;scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-3,-4,6));bpy.context.object.data.energy=650;bpy.context.object.data.size=4
world=scene.world;world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.64,.71,.85,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.38
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=28;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.view_settings.view_transform='AgX';scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for im in images:im.pack();im.filepath='//../../asset-catalog/Assets/BFjord/ForestFloorDetail/Textures/'+im.name+'.png'
for asset in manifest['assets']:
 kind=asset['id'];ob=next(o for o in review_objects[kind] if '_LOD0' in o.name);ob.hide_render=False;ob.hide_set(False)
 lo,hi=asset['boundsBlenderZUp'];span=max(hi[0]-lo[0],hi[1]-lo[1]);height=hi[2];target=Vector(((lo[0]+hi[0])*.5,(lo[1]+hi[1])*.5,height*.50));camera.location=target+Vector((span*.65,-span*.9,span*.78));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=max(span*1.55,height*3.0);scene.render.filepath=str(OUT/'Review'/f'{kind}.png')
 if not a.skip_renders:bpy.ops.render.render(write_still=True)
 ob.hide_render=True;ob.hide_set(True)
# Source opens with a useful root specimen; all editable imported LOD meshes and atlas packed.
if 'ExposedRootFan_A' in review_objects:
 ob=next(o for o in review_objects['ExposedRootFan_A'] if '_LOD0' in o.name);ob.hide_render=False;ob.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Sources/ForestFloor07.blend'),compress=True)
print('FOREST_FLOOR_VERIFIED',len(checks),flush=True)
