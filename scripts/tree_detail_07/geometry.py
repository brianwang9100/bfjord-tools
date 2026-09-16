# SPDX-License-Identifier: MIT
import math
import bpy
from mathutils import Vector, Quaternion
mat=None
class Mesh:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def face(self,pts,uv,t):
  start=len(self.v);self.v.extend(pts);self.f.append(tuple(range(start,start+len(pts))));self.uv.extend([(.012+.476*s,q) if t==0 else (.5+((t-1)%2+.025+.95*s)/4,((t-1)//2+.025+.95*q)/2) for s,q in uv])
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
    n=(k+1)%sides;self.face([rings[j][k],rings[j][n],rings[j+1][n],rings[j+1][k]],[(k/sides,dist[j]/2.4),((k+1)/sides,dist[j]/2.4),((k+1)/sides,dist[j+1]/2.4),(k/sides,dist[j+1]/2.4)],t)
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
