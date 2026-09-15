#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Original botanical foliage. Run with Blender --background --python generate.py -- --output PATH.
No downloaded imagery or models are read. Asset outputs are CC0-1.0.
"""
import argparse, json, math, random, sys, hashlib
from pathlib import Path
import bpy
from mathutils import Vector
P=argparse.ArgumentParser(); P.add_argument('--output',required=True); P.add_argument('--review',required=True)
a=P.parse_args(sys.argv[sys.argv.index('--')+1:]); OUT=Path(a.output).resolve(); REVIEW=Path(a.review).resolve()
for p in [OUT/'Models',OUT/'Textures',REVIEW]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
# Four guarded atlas islands: leaf, golden grass, rose petal, cream petal/stem.
N=512; pixels=[]; relief=[]
for y in range(N):
 for x in range(N):
  tile=(x//256)+(y//256)*2; u=(x%256)/255; v=(y%256)/255
  vein=math.exp(-((u-.5)/.025)**2)*.055
  sidevein=math.exp(-(math.sin((v*7-abs(u-.5)*3)*math.pi)/.12)**2)*.025
  grain=math.sin(x*71.3+y*29.7)*.014
  if tile==0: c=(.115+vein+.075*v,.245+vein+sidevein+.11*v,.052+.038*v)
  elif tile==1:c=(.27+.16*v+vein,.31+.10*v+vein,.075+.07*v)
  elif tile==2:c=(.61+.16*v,.095+.14*v,.24+.20*v)
  else:c=(.76+.17*v,.72+.20*v,.53+.39*v)
  pixels.extend([max(0,min(1,k+grain)) for k in c]+[1])
  relief.append((vein*.8+sidevein*.45+grain*.03) if tile<2 else (.003*math.cos((u-.5)*28)*math.sin(v*math.pi)))
im=bpy.data.images.new('OriginalFoliageAtlas',width=N,height=N,alpha=True); im.pixels.foreach_set(pixels); im.filepath_raw=str(OUT/'Textures/FoliageAtlas.png'); im.file_format='PNG'; im.save()
normal_pixels=[]
for y in range(N):
 for x in range(N):
  dx=(relief[y*N+min(N-1,x+1)]-relief[y*N+max(0,x-1)])*20
  dy=(relief[min(N-1,y+1)*N+x]-relief[max(0,y-1)*N+x])*20
  n=Vector((-dx,-dy,1)).normalized();normal_pixels.extend([n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1])
norm=bpy.data.images.new('OriginalFoliageNormal',width=N,height=N,alpha=False);norm.colorspace_settings.name='Non-Color';norm.pixels.foreach_set(normal_pixels);norm.filepath_raw=str(OUT/'Textures/FoliageNormal.png');norm.file_format='PNG';norm.save()
mat=bpy.data.materials.new('OriginalFoliage'); mat.use_nodes=True
nodes=mat.node_tree.nodes; bs=nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.79
tex=nodes.new('ShaderNodeTexImage'); tex.image=im; mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
normal_tex=nodes.new('ShaderNodeTexImage');normal_tex.image=norm;normal_node=nodes.new('ShaderNodeNormalMap');normal_node.inputs['Strength'].default_value=.55;mat.node_tree.links.new(normal_tex.outputs['Color'],normal_node.inputs['Color']);mat.node_tree.links.new(normal_node.outputs['Normal'],bs.inputs['Normal'])
class Mesh:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def face(self,points,uv,tile=0):
  start=len(self.v); self.v.extend(points);self.f.append(tuple(range(start,start+len(points))))
  tx=tile%2; ty=tile//2
  self.uv.extend([((tx+.04+.92*u)*.5,(ty+.04+.92*v)*.5) for u,v in uv])
 def tube(self,points,radius,sides=5,tile=1):
  for j in range(len(points)-1):
   p,q=Vector(points[j]),Vector(points[j+1]); axis=(q-p).normalized(); cross=axis.cross(Vector((0,1,0)))
   if cross.length<.01:cross=axis.cross(Vector((1,0,0)))
   cross.normalize(); other=axis.cross(cross)
   for k in range(sides):
    def ring(pos,i,r):return pos+(cross*math.cos(i*math.tau/sides)+other*math.sin(i*math.tau/sides))*r
    r=radius*(1-.55*j/max(1,len(points)-1)); nr=radius*(1-.55*(j+1)/max(1,len(points)-1))
    self.face([ring(p,k,r),ring(p,k+1,r),ring(q,k+1,nr),ring(q,k,nr)],[(k/sides,j/(len(points)-1)),((k+1)/sides,j/(len(points)-1)),((k+1)/sides,(j+1)/(len(points)-1)),(k/sides,(j+1)/(len(points)-1))],tile)
 def leaf(self,p,d,length,width,tile=0,segments=5,heart=False):
  p=Vector(p); d=Vector(d).normalized(); side=d.cross(Vector((0,0,1)))
  if side.length<.1:side=d.cross(Vector((0,1,0)))
  side.normalize(); normal=side.cross(d).normalized()
  rows=[]
  for j in range(segments+1):
   t=j/segments
   shape=math.sin(math.pi*t)**(.6 if heart else .85)
   # Folded midrib, toothed edge and drooped tip are geometry, not opacity cards.
   w=width*shape*(1+.07*(j%2))
   mid=p+d*(length*t)+normal*(length*(.12*math.sin(math.pi*t)-.10*t*t))
   rows.append((mid-side*w-normal*w*.13,mid+normal*w*.12,mid+side*w-normal*w*.13))
  for j in range(segments):
   for k in range(2):
    points=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]]
    uv=[(k*.5,j/segments),((k+1)*.5,j/segments),((k+1)*.5,(j+1)/segments),(k*.5,(j+1)/segments)]
    if j==0:points.pop(0);uv.pop(0)
    elif j==segments-1:points.pop(2);uv.pop(2)
    self.face(points,uv,tile)
 def flower(self,p,radius,petals,tile,lod,rng):
  p=Vector(p)
  for k in range(petals):
   ang=math.tau*k/petals; d=Vector((math.cos(ang),math.sin(ang),rng.uniform(.06,.22)))
   self.leaf(p+d*radius*.08,d,radius,radius*(.44 if tile==2 else .14),tile,max(2,4-lod),heart=tile==2)
  # Domed disc, separate yellow seed texture island.
  sides=max(6,12-lod*3)
  for k in range(sides):
   t=k*math.tau/sides; u=(k+1)*math.tau/sides; r=radius*.18
   self.face([p+Vector((0,0,r*.55)),p+Vector((r*math.cos(t),r*math.sin(t),0)),p+Vector((r*math.cos(u),r*math.sin(u),0))],[(.5,.95),(.4,.95),(.6,.95)],1)
 def object(self,name):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.materials.append(mat);mesh.update()
  layer=mesh.uv_layers.new(name='UVMap')
  for poly in mesh.polygons:
   poly.use_smooth=True
   for loop in poly.loop_indices:layer.data[loop].uv=self.uv[mesh.loops[loop].vertex_index]
  ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
  # Triangulation makes the normal/tangent/export contract explicit.
  bpy.context.view_layer.objects.active=ob;ob.select_set(True)
  mod=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)
  return ob

def generate(kind,lod):
 rng=random.Random(743); m=Mesh()
 if kind=='RoseThicket_A':
  for branch in range([9,7,5][lod]):
   rng=random.Random(743+branch)
   ang=branch*2.4; height=rng.uniform(.72,1.23); reach=rng.uniform(.38,.64)
   pts=[Vector((math.cos(ang)*reach*t*t,math.sin(ang)*reach*t*t,height*t)) for t in [0,.25,.5,.75,1]]
   m.tube(pts,.012,6-lod)
   for j in range([7,5,4][lod]):
    t=.24+j*.72/([7,5,4][lod]); stem=Vector((math.cos(ang)*reach*t*t,math.sin(ang)*reach*t*t,height*t)); theta=ang+j*2.39
    d=Vector((math.cos(theta),math.sin(theta),.3)); tip=stem+d*.23
    m.tube([stem,tip],.0025,4)
    for n in range(2):
     base=stem+d*(.08+n*.065)
     for sign in [-1,1]:
      leafdir=d*.4+Vector((-d.y,d.x,.25))*sign
      m.leaf(base,leafdir,.12-n*.018,.043,segments=max(3,6-lod*2))
    m.leaf(tip,d,.10,.04,segments=max(3,6-lod*2))
   if branch%2==0:m.flower(pts[-1]+Vector((0,0,.01)),.08,5,2,lod,rng)
 elif kind=='MeadowDaisy_A':
  for i in range([13,9,6][lod]):
   rng=random.Random(834+i)
   ang=i*2.4; r=rng.uniform(.02,.23); h=rng.uniform(.32,.70)
   p=Vector((math.cos(ang)*r,math.sin(ang)*r,0)); tip=p+Vector((math.cos(ang)*.08,math.sin(ang)*.08,h))
   m.tube([p,p.lerp(tip,.5),tip],.0035,5-lod)
   for j in range(3):m.leaf(p.lerp(tip,.22+j*.21),(math.cos(ang+j*2),math.sin(ang+j*2),.3),.14,.021,segments=max(2,5-lod))
   m.flower(tip,.055,[14,11,8][lod],3,lod,rng)
 elif kind=='WoodSorrel_A':
  for i in range([20,14,9][lod]):
   rng=random.Random(947+i)
   ang=i*2.4; r=rng.uniform(.025,.29); h=rng.uniform(.055,.19);p=Vector((math.cos(ang)*r,math.sin(ang)*r,0));tip=p+Vector((.015,0,h))
   m.tube([p,tip],.0017,3)
   for k in range(3):
    theta=k*math.tau/3+ang;m.leaf(tip,(math.cos(theta),math.sin(theta),.22),.068,.044,segments=max(3,6-lod),heart=True)
   if i%6==0:
    flower=tip+Vector((0,0,.075));m.tube([tip,flower],.0012,3);m.flower(flower,.021,5,3,lod,rng)
 else:
  for i in range([74,45,25][lod]):
   rng=random.Random(185+i)
   ang=i*2.4;r=rng.uniform(.015,.21);h=rng.uniform(.28,.83);lean=rng.uniform(.10,.43)
   p=Vector((math.cos(ang)*r,math.sin(ang)*r,0));side=Vector((-math.sin(ang),math.cos(ang),0)); rows=[]; seg=[7,5,3][lod]
   for j in range(seg+1):
    t=j/seg;pos=p+Vector((math.cos(ang)*lean*t*t,math.sin(ang)*lean*t*t,h*(t-.18*t*t*t)));w=.009*(1-t)*(.8+math.sin(t*math.pi)*.5)
    rows.append((pos-side*w,pos+Vector((0,0,w*.45)),pos+side*w))
   for j in range(seg):
    for k in range(2):
     points=[rows[j][k],rows[j][k+1],rows[j+1][k+1],rows[j+1][k]]
     uv=[(k*.5,j/seg),((k+1)*.5,j/seg),((k+1)*.5,(j+1)/seg),(k*.5,(j+1)/seg)]
     if j==seg-1:points.pop(2);uv.pop(2)
     m.face(points,uv,i%2)
  for i in range([12,8,5][lod]):
   rng=random.Random(119+i)
   ang=i*2.4;p=Vector((math.cos(ang)*.11,math.sin(ang)*.11,0));tip=p+Vector((math.cos(ang)*.18,math.sin(ang)*.18,rng.uniform(.8,1.10)))
   m.tube([p,p.lerp(tip,.5),tip],.0024,3)
   for j in range([7,5,4][lod]):
    start=tip-Vector((0,0,.025*j)); theta=ang+j*2.4;d=Vector((math.cos(theta)*.4,math.sin(theta)*.4,.8))
    m.leaf(start,d,.055,.008,1,3)
 return m.object(kind+'_LOD'+str(lod))

ids=['RoseThicket_A','MeadowDaisy_A','WoodSorrel_A','CoastalGrass_A']; manifest={'schemaVersion':1,'license':'CC0-1.0','generatorLicense':'MIT','units':'metres','atlas':'Textures/FoliageAtlas.png','normal':'Textures/FoliageNormal.png','assets':[]}
allobjects=[]
for kind in ids:
 obs=[generate(kind,lod) for lod in range(3)];allobjects.extend(obs)
 bpy.ops.object.select_all(action='DESELECT')
 for ob in obs:ob.select_set(True)
 path=OUT/'Models'/f'{kind}.fbx'
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',use_tspace=True,mesh_smooth_type='FACE',path_mode='STRIP',apply_scale_options='FBX_SCALE_UNITS')
 bounds=[[min(v.co[axis] for v in obs[0].data.vertices) for axis in range(3)],[max(v.co[axis] for v in obs[0].data.vertices) for axis in range(3)]]
 manifest['assets'].append({'id':kind,'model':'Models/'+path.name,'triangles':[len(o.data.polygons) for o in obs],'boundsBlenderZUp':bounds,'lodCount':3,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
# Reimport the deliverables, verify topology and UV contract, render actual export geometry.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
checks=[]
for index,kind in enumerate(ids):
 bpy.ops.import_scene.fbx(filepath=str(OUT/'Models'/f'{kind}.fbx'))
 obs=list(bpy.context.selected_objects)
 for ob in obs:
  if ob.type!='MESH':continue
  lod=int(ob.name.split('_LOD')[1][0]);ob.data.materials.clear();ob.data.materials.append(mat)
  assert ob.data.uv_layers and all(len(p.vertices)==3 for p in ob.data.polygons)
  assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
  assert all(p.area>1e-12 for p in ob.data.polygons), (kind,lod,'degenerate triangle')
  ob.data.calc_tangents()
  assert all(math.isfinite(c) for loop in ob.data.loops for c in loop.tangent)
  # Same scale within each column; each row compares the actual authored LOD.
  ob.location.x+=index*1.8-2.7;ob.location.y+=lod*1.6
  checks.append({'id':kind,'lod':lod,'triangles':len(ob.data.polygons),'uv':True,'finite':True,'nondegenerate':True,'finiteTangents':True})
manifest['exportReimportChecks']=checks
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Blender review scene uses only original assets and neutral stage.
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.name='Review stage';ground.location.z=-.012
stage=bpy.data.materials.new('Review neutral ground');stage.diffuse_color=(.075,.085,.069,1);ground.data.materials.append(stage)
for index,kind in enumerate(ids):
 bpy.ops.object.text_add(location=(index*1.8-3.30,-.75,.01));label=bpy.context.object;label.data.body=kind.replace('_A','');label.data.size=.14
for lod in range(3):
 bpy.ops.object.text_add(location=(-4.10,lod*1.6,.01));label=bpy.context.object;label.data.body='LOD '+str(lod);label.data.size=.18
bpy.ops.object.camera_add(location=(6.6,-9.3,9.0));camera=bpy.context.object;camera.rotation_euler=(Vector((0,1.6,.3))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=9.8;bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-2,-3,7));bpy.context.object.data.energy=1250;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=5
bpy.context.scene.world.color=(.25,.25,.25)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24;scene.render.threads_mode='FIXED';scene.render.threads=3
scene.view_settings.view_transform='AgX';scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
im.pack();norm.pack();bpy.ops.wm.save_as_mainfile(filepath=str(REVIEW.parent/'FoliageDetail.blend'))
scene.render.filepath=str(REVIEW/'exported-lods.png');bpy.ops.render.render(write_still=True)
print('FOLIAGE_VERIFIED',json.dumps(manifest))
