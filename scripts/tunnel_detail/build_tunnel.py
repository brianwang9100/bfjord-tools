# SPDX-License-Identifier: MIT
"""Original metric tunnel masonry. Blender 5.2, --threads 4, --python-exit-code 1.
No downloaded geometry or image inputs. Export +X right/+Y up/+Z into bore.
"""
import bpy, bmesh, json, math, random, sys, hashlib, uuid, struct, zlib
from pathlib import Path
import numpy as np
from mathutils import Vector
BASE = next(candidate for parent in Path(__file__).resolve().parents
            for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools') if candidate.is_dir())
ART = BASE/'TunnelDetail'
CATALOG = BASE/'asset-catalog'
OUT = CATALOG / 'Assets/BFjord/TunnelDetail'
ART.mkdir(parents=True, exist_ok=True); OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.render.threads_mode='FIXED'; bpy.context.scene.render.threads=4

def png(path, values):
    a=np.uint8(np.rint(np.clip(values,0,1)*255)); h,w=a.shape[:2]
    def c(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+c(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+c(b'IDAT',zlib.compress(b''.join(b'\0'+r.tobytes() for r in a),9))+c(b'IEND',b''))

n=512; rng=np.random.default_rng(915503); f=np.fft.fftfreq(n); freq=np.sqrt(f[:,None]**2+f[None,:]**2)
def noise(scale):
    a=np.fft.ifft2(np.fft.fft2(rng.standard_normal((n,n)))*np.exp(-(freq*scale)**2)).real
    return a/max(a.std(),1e-9)
broad=noise(85); mid=noise(12); fine=noise(1.5)
height=.45+.035*mid+.008*fine
base=np.clip(.49+.034*broad+.022*mid+.013*fine,.22,.7)
rgba=np.ones((n,n,4)); rgba[:,:,:3]=base[:,:,None]*np.array([1.03,1,.92]);png(OUT/'Stone_BaseColor.png',rgba)
gy,gx=np.gradient(height); normals=np.stack((-gx*9,gy*9,np.ones_like(gx)),axis=-1);normals/=np.linalg.norm(normals,axis=-1)[:,:,None]
rgba[:,:,:3]=normals*.5+.5;png(OUT/'Stone_Normal.png',rgba)
rgba[:,:,:3]=0;rgba[:,:,3]=np.clip(.19+.025*mid,.1,.3);png(OUT/'Stone_Mask.png',rgba)
# Concrete is a restrained, original, metric tile with pores and construction seams.
y,x=np.mgrid[:n,:n]/n
seam=(np.minimum(y% .25,.25-y% .25)<.0025)
base=np.clip(.46+.018*broad+.012*mid+.005*fine-.065*seam,.22,.65)
rgba[:,:,:3]=base[:,:,None]*np.array([1.01,1,.94]);rgba[:,:,3]=1;png(OUT/'Lining_BaseColor.png',rgba)
mat=bpy.data.materials.new('Original grey granite');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.8
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'Stone_BaseColor.png'));mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])

# Mesh coordinates are authored directly in Unity basis then transformed to Blender.
def obj(name, verts, faces):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in verts],[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);return ob

def block(name,x0,x1,y0,y1,z0,z1):
    v=[(x,y,z) for z in (z0,z1) for y in (y0,y1) for x in (x0,x1)]
    return obj(name,v,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)])

def wedge(a,b,r0,r1,rise0,rise1,z0,z1,name):
    verts=[]
    for z in (z0,z1):
        for r,h in ((r0,rise0),(r1,rise1)):
            for t in (a,b):verts.append((math.cos(t)*r,1.5+math.sin(t)*h,z))
    return obj(name,verts,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)])

def make(lod):
    objects=[];rnd=random.Random(24015)
    # Vertical joints are staggered. Separate stones have bevels and restrained uneven face relief.
    for side in (-1,1):
        for row in range(10):
            y0=-.3+row*.69; y1=y0+.667
            for col in range(2):
                left=4.01+col*1.37
                if row%2:left+=.08 if col==0 else -.08
                ob=block('Wing ashlar',side*left,side*(left+1.34),y0,y1,-.15-rnd.uniform(0,.035),1.0)
                objects.append(ob)
        for row in range(3):
            objects.append(block('Jamb',min(side*4.0,side*4.82),max(side*4.0,side*4.82),row*.5-.03,(row+1)*.5-.045,-.30,1.08))
        for col in range(3):
            left=4.0+col*.98
            objects.append(block('Wing coping',min(side*(left-.06),side*(left+.94)),max(side*(left-.06),side*(left+.94)),6.58,6.84,-.27,1.14))
        objects.append(block('Footing',min(side*3.98,side*6.94),max(side*3.98,side*6.94),-.35,.1,-.38,1.2))
    # The opening follows the existing 8m-wide x 5.5m-clear elliptical bore exactly.
    for i in range(25):
        a=i*math.pi/25+.003;b=(i+1)*math.pi/25-.003
        objects.append(wedge(a,b,4,4.88,4,4.85,-.32-rnd.uniform(0,.025),1.02,'Arch voussoir'))
    for i in range(28):
        a=i*math.pi/28+.001;b=(i+1)*math.pi/28-.001
        objects.append(wedge(a,b,4.9,5.15,4.88,5.14,-.13,.86,'Arch hood'))
    for ob in objects:
        bpy.context.view_layer.objects.active=ob;ob.select_set(True)
        # Recalculate outward orientation before bevel, including reflected left blocks.
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(ob.data);bm.free()
        if lod<2:
            bevel=ob.modifiers.new('Worn arris','BEVEL');bevel.width=.025 if lod==0 else .018;bevel.segments=2 if lod==0 else 1
            bpy.ops.object.modifier_apply(modifier=bevel.name)
        ob.select_set(False)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object;ob.name='MasonryPortal_LOD'+str(lod)
    # Per-face metric box projection. Repeat at 2m; never normalize across total portal width.
    uv=ob.data.uv_layers.new(name='Metric stone 2m')
    for p in ob.data.polygons:
        normal=p.normal;drop=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=drop]
        for li in p.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(co[axes[0]]/2,co[axes[1]]/2)
    ob.data.materials.append(mat);ob.data.calc_loop_triangles()
    vertices=[];normals=[];uvs=[];triangles=[]
    for tri in ob.data.loop_triangles:
        for li in tri.loops:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co;no=ob.data.polygons[tri.polygon_index].normal;u=uv.data[li].uv
            vertices.append(dict(x=round(co.x,6),y=round(co.z,6),z=round(-co.y,6)))
            normals.append(dict(x=round(no.x,6),y=round(no.z,6),z=round(-no.y,6)))
            uvs.append(dict(x=round(u.x,6),y=round(u.y,6)));triangles.append(len(triangles))
    # Rotation above preserves handedness. Index winding needs no reflection.
    result=dict(vertices=vertices,normals=normals,uv=uvs,triangles=triangles)
    assert all(math.isfinite(a) for v in vertices for a in v.values())
    assert min(v['y'] for v in vertices)>=-.351
    assert not any(abs(v['x'])<3.99 and .2<v['y']<1.49 for v in vertices)
    for i in range(0,len(vertices),3):
        a,b,c=[Vector(tuple(vertices[k].values())) for k in range(i,i+3)]
        assert (b-a).cross(c-a).length>1e-9
        assert (b-a).cross(c-a).normalized().dot(Vector(tuple(normals[i].values())))>.99
    ob.hide_render=lod!=0;ob.hide_set(lod!=0)
    return ob,result

lods=[];objects=[]
for level in range(3):
    ob,data=make(level);objects.append(ob);lods.append(data)
(OUT/'MasonryPortal.json').write_text(json.dumps(dict(schemaVersion=1,width=8,clearance=5.5,lods=lods),separators=(',',':'))+'\n')
# Collision uses the existing closed shell; the decorative stonework adds no drive-surface collision.
manifest=dict(schemaVersion=1,license='CC0-1.0',generatorLicense='MIT',blender=bpy.app.version_string,threads=4,
    basis='+X right/+Y up/+Z into tunnel',width=8,clearance=5.5,contactY=-.35,
    lodTriangles=[len(l['triangles'])//3 for l in lods],lodVertices=[len(l['vertices']) for l in lods],
    checks=['finite positions and normals','positive triangle area','face/winding normal agreement','metric UVs','clear bore','planted footings'],
    collision='Existing closed procedural tunnel shell; no decorative colliders',textureSize=512)
(ART/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Native Blender source and representative studio render are evidence, not runtime dependencies.
bpy.ops.object.select_all(action='DESELECT');objects[0].hide_set(False);objects[0].select_set(True);bpy.context.view_layer.objects.active=objects[0]
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'MasonryPortal.blend'))
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.25,.29,.36,1)
for name,location,energy,size in [('Key',(-8,9,12),2600,8),('Fill',(7,4,7),1500,7)]:
    bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.name=name;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,3))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(11,19,10));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,3))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=19;bpy.context.scene.camera=camera
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.render.filepath=str(ART/'portal-review.png');bpy.ops.render.render(write_still=True)
entries=[]
for path in sorted(OUT.iterdir()):
    if path.suffix not in ('.png','.json'):continue
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'bfjord-tools:tunnel-detail:v1:'+path.name).hex
    if path.suffix=='.json':meta='fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: Original CC0 tunnel mesh\n'
    else:
        normal='_Normal' in path.name;linear=normal or '_Mask' in path.name
        meta=f'fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: {0 if linear else 1}\n  textureType: {1 if normal else 0}\n  textureShape: 1\n  maxTextureSize: 512\n  textureSettings:\n    filterMode: 2\n    aniso: 4\n    wrapU: 0\n    wrapV: 0\n  userData: Original CC0 metric tunnel texture\n'
    path.with_suffix(path.suffix+'.meta').write_text(meta)
    for file in (path,path.with_suffix(path.suffix+'.meta')):entries.append(dict(path=file.relative_to(CATALOG).as_posix(),bytes=file.stat().st_size,sha256=hashlib.sha256(file.read_bytes()).hexdigest()))
(ART/'catalog-additions.json').write_text(json.dumps(dict(schemaVersion=1,files=entries),indent=2)+'\n')
print('TUNNEL_ASSET_CHECKS_PASS '+json.dumps(manifest))
