# SPDX-License-Identifier: MIT
"""Original shore detail, deterministic Blender 4.5+/5.x authoring and FBX export.
Run Blender --background --threads 4 --python Scripts/world_assets/shore_detail/build_shore.py
Generated original geometry/textures are CC0-1.0. No external add-on or reference pixels.
"""
import bpy, math, random, json, hashlib, shutil, uuid, subprocess
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools'):
            if candidate.is_dir():return candidate
    raise FileNotFoundError('Could not find art/BFjordTools or assets/BFjordTools above '+str(script_file))

ART=find_art_root(__file__)
OUT=ART/'ShoreDetail'
CAT=ART/'asset-catalog/Assets/BFjord/ShoreDetail'
for d in ['Models','Textures','Sources','Review']:(OUT/d).mkdir(parents=True,exist_ok=True)
for d in ['Models','Textures']:(CAT/d).mkdir(parents=True,exist_ok=True)
N=2048
COLOR_ONLY=False

def png(name,a,color=False):
    if COLOR_ONLY and not color:return None
    h,w=a.shape[:2]
    if a.shape[2]==3:a=np.concatenate([a,np.ones((h,w,1),np.float32)],axis=2)
    # These arrays already contain encoded sRGB color values. Blender saves this
    # generated PNG buffer directly; decoding here would make Unity decode twice.
    im=bpy.data.images.new(name,width=w,height=h,alpha=True,float_buffer=False)
    im.colorspace_settings.name='sRGB' if color else 'Non-Color'
    im.pixels.foreach_set(np.clip(a,0,1).astype(np.float32).ravel())
    im.filepath_raw=str(OUT/'Textures'/name);im.file_format='PNG';im.save()
    # Strip unused opaque alpha and optimize losslessly for catalog distribution.
    mode='RGB' if a.shape[2]==4 and 'Mask' not in name else 'RGBA'
    subprocess.run(['/opt/homebrew/bin/python3','-c','from PIL import Image; import sys; p=sys.argv[1]; im=Image.open(p).convert(sys.argv[2]); im.save(p,optimize=True)',im.filepath_raw,mode],check=True)
    im.reload()
    return im

def maps():
    size=N//2; y,x=np.mgrid[0:size,0:size]/(size-1)
    rng=np.random.default_rng(90315)
    color=np.zeros((N,N,3),np.float32);normal=np.zeros((N,N,3),np.float32);mask=np.zeros((N,N,4),np.float32)
    for q in range(4):
        noise=rng.normal(0,1,(size,size)); waves=np.sin(x*34+np.sin(y*11)*.7)
        rings=np.sin(y*190+np.sin(x*22)*.8)
        if q==0: # radial u, growth v, cockle/chalk exterior
            h=.035*waves+.018*rings+.01*noise
            stain=np.maximum(0,np.sin(x*18+1.5*np.sin(y*5)))*(.4+.6*y)
            base=np.array([.82,.75,.63]);c=base[None,None,:]*(1-.24*stain[:,:,None])
            c=c+(.025*rings+.027*noise)[:,:,None];rough=.69+.08*noise
        elif q==1: # blue-black mussel exterior; pale rim encoded by growth coordinate
            h=.016*rings+.008*noise
            c=np.zeros((size,size,3))+np.array([.095,.12,.145])
            growth=(.5+.5*np.sin(y*96+np.sin(x*9)))
            c+=growth[:,:,None]*np.array([.055,.060,.052])
            rim=np.clip((y-.9)*10,0,1)[:,:,None]
            c=c*(1-rim)+np.array([.49,.43,.32])*rim
            c+=noise[:,:,None]*.017;rough=.36+.055*noise
        elif q==2: # amber olive wrack with axial vein and mottled desiccation
            h=.014*np.sin(x*115+np.sin(y*15))+.01*noise
            pigment=.5+.25*np.sin(x*10+y*8)+.15*np.sin(y*37+x*26)
            c=np.array([.13,.115,.045])+pigment[:,:,None]*np.array([.20,.15,.045])
            vein=np.exp(-((x-.5)/.024)**2)
            c+=vein[:,:,None]*np.array([.045,.045,.014]);c+=noise[:,:,None]*.018
            rough=.48+.07*noise
        else: # bleached wood, branching longitudinal grain and hairline checks
            grain=np.sin(x*280+np.sin(y*13)*2+np.sin(x*22+y*9))
            broad=np.sin(x*43+np.sin(y*7)*.9)
            checks=np.maximum(0,grain-.73)**2
            h=.022*grain+.042*broad-.1*checks+.005*noise
            c=np.array([.66,.61,.50])+(.075*broad+.025*grain-.35*checks+.022*noise)[:,:,None]
            rough=.87+.04*noise
        gy,gx=np.gradient(h);n=np.stack([-gx*80,-gy*80,np.ones_like(gx)],2);n/=np.linalg.norm(n,axis=2)[:,:,None]
        row=(q//2)*size;col=(q%2)*size
        color[row:row+size,col:col+size]=c
        normal[row:row+size,col:col+size]=n*.5+.5
        mask[row:row+size,col:col+size]=np.stack([np.zeros_like(x),np.clip(.94+noise*.01,0,1),np.clip(.5+h,0,1),np.clip(1-rough,0,1)],2)
    return [png('ShoreAtlas_Color.png',color,True),png('ShoreAtlas_NormalGL.png',normal),png('ShoreAtlas_Mask.png',mask)]

def material(images,name='BFjord_ShoreAtlas'):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links;bs=nodes.get('Principled BSDF')
    for i,im in enumerate(images):
        tex=nodes.new('ShaderNodeTexImage');tex.image=im
        if i==0:links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        elif i==1:
            n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.7;links.new(tex.outputs['Color'],n.inputs['Color']);links.new(n.outputs['Normal'],bs.inputs['Normal'])
        else:
            inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
            links.new(tex.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness'])
    return mat

def uv(u,v,q):return ((q%2)*.5+.008+.484*u,(q//2)*.5+.008+.484*v)

class Mesh:
    def __init__(self):self.v=[];self.f=[];self.uv=[]
    def grid(self,points,coords,rows,cols,reverse=False):
        off=len(self.v);self.v+=points;self.uv+=coords
        for j in range(rows-1):
            for i in range(cols-1):
                a=off+j*cols+i;face=(a,a+1,a+1+cols,a+cols)
                self.f.append(face[::-1] if reverse else face)
        return off
    def object(self,name,mat):
        me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update()
        ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);me.materials.append(mat)
        layer=me.uv_layers.new(name='UVMap')
        for p in me.polygons:
            p.use_smooth=True
            for li in p.loop_indices:layer.data[li].uv=self.uv[me.loops[li].vertex_index]
        return ob

def shell(m,pos,scale,angle,lod,mussel=False,flip=False,fragment=False):
    nr=([8,5,3] if fragment else [12,6,3])[lod]
    nt=([48,24,12] if fragment else [64,32,12] if mussel else [96,48,24])[lod]
    rot=Matrix.Rotation(angle,3,'Z');q=1 if mussel else 0
    # Paired outside/inside surfaces, connected at a scalloped broken lip.
    for side in range(2):
        points=[];coords=[]
        for j in range(nr+1):
            r=.006+.994*j/nr
            for k in range(nt+1):
                t=k/nt*math.tau
                if fragment:t=.2+k/nt*3.1
                ripple=(.5+.5*math.cos(t*(22 if not mussel else 8)))
                edge=1+.012*math.sin(t*22)+.006*math.sin(t*51)
                xx=r*math.cos(t)*(.50 if not mussel else .33)*(1 if not mussel else .7+.3*math.sin(t))
                yy=r*math.sin(t)*.5-(1-r)*(.32 if not mussel else .35)
                z=(1-r*r)**.75*(.23 if not mussel else .15)
                if not side:z+=.016*ripple*math.sin(r*math.pi)**.3+.002*math.sin(r*94)
                else:z-=.012
                if flip:z=.245-z
                p=rot@Vector((xx*edge*scale,yy*edge*scale,z*scale)) + Vector(pos)
                points.append(tuple(p));coords.append(uv(k/nt,r,q if not side else 0))
        off=m.grid(points,coords,nr+1,nt+1,reverse=(side==0) ^ flip)
        if side==0:outer=off
        else:
            for k in range(nt):
                a=outer+nr*(nt+1)+k;b=off+nr*(nt+1)+k;m.f.append((a,b,b+1,a+1))

def ribbon(m,start,angle,length,width,seed,lod,q=2):
    rng=random.Random(seed);ns=[36,18,8][lod];nw=([4,2,1] if width<.035 else [8,4,2])[lod];phase=rng.uniform(0,6);bend=rng.uniform(-.7,.7)
    rot=Matrix.Rotation(angle,3,'Z')
    for side in range(2):
        points=[];coords=[]
        for j in range(ns+1):
            t=j/ns
            for k in range(nw+1):
                u=k/nw*2-1;w=width*(.05+.95*math.sin(math.pi*t)**.65)
                edge=1+.1*math.sin(t*59+phase)
                x=u*w*.5*edge+length*bend*t*t
                y=length*t
                z=.010+length*.09*math.sin(t*math.pi)+width*.23*math.sin(t*32+phase)*abs(u)**1.5+width*.16*u*u
                z+=.001 if side==0 else -.001
                points.append(tuple(rot@Vector((x,y,z))+Vector(start)));coords.append(uv(k/nw,t,q))
        off=m.grid(points,coords,ns+1,nw+1,reverse=side==1)
        if side==0:front=off
        else:
            for j in range(ns):
                for k in [0,nw]:
                    a=front+j*(nw+1)+k;b=off+j*(nw+1)+k;m.f.append((a,a+nw+1,b+nw+1,b))

def tube(m,path,radii,lod,q=3,seed=1):
    rng=random.Random(seed);count=([64,28,12] if q==3 else [12,8,5])[lod];phase=rng.random()*6
    pts=[];coords=[]
    for j,p in enumerate(path):
        v=Vector(path[min(len(path)-1,j+1)])-Vector(path[max(0,j-1)])
        v.normalize();a=v.cross(Vector((0,0,1))).normalized();b=v.cross(a).normalized()
        for k in range(count+1):
            t=k/count*math.tau;r=radii[j]*(1+.12*math.sin(t*5+phase)+.07*math.sin(t*11+j*.4))
            if q==3:
                grain=max(0,math.cos(t*13+math.sin(j*.18)*.25))**12
                r*=1-.24*grain+.09*math.sin(j*.87+t*9)+.06*math.sin(j*2.31+t*17)
            pt=Vector(p)+r*(math.cos(t)*a+math.sin(t)*b)
            if j in [0,len(path)-1]:pt+=v*math.sin(t*7+phase)*r*.8
            pts.append(tuple(pt));coords.append(uv(k/count,j/(len(path)-1),q))
    off=m.grid(pts,coords,len(path),count+1)
    # Closed irregular broken ends; all faces triangulated before exporting.
    m.f.append(tuple(off+k for k in range(count-1,-1,-1)))
    m.f.append(tuple(off+(len(path)-1)*(count+1)+k for k in range(count)))

def bladder(m,p,r,lod):
    nr=[6,4,3][lod];nt=[8,6,5][lod];pts=[];coords=[]
    for j in range(nr+1):
        ph=.01+(math.pi-.02)*j/nr
        for k in range(nt+1):
            t=k/nt*math.tau;pts.append((p[0]+r*math.sin(ph)*math.cos(t),p[1]+r*math.sin(ph)*math.sin(t),p[2]+r*.7*math.cos(ph)));coords.append(uv(k/nt,j/nr,2))
    m.grid(pts,coords,nr+1,nt+1,reverse=True)

IDS=['CockleShells_A','MusselShells_A','ShellFragments_A','KelpWrack_A','BladderWrack_A','BleachedDriftwood_A']

def build(id,lod,mat):
    m=Mesh();rng=random.Random(505)
    if id=='CockleShells_A':
        shell(m,(-.052,0,0),.082,.4,lod);shell(m,(.048,.014,.005),.067,-1,lod,flip=True)
    elif id=='MusselShells_A':
        for x,y,s,a in [(-.065,-.01,.085,.6),(0,.018,.10,-.4),(.055,-.026,.078,-1.3),(.021,-.058,.065,1.4)]:shell(m,(x,y,0),s,a,lod,True)
    elif id=='ShellFragments_A':
        for i in range(7):shell(m,(rng.uniform(-.1,.1),rng.uniform(-.09,.09),0),rng.uniform(.025,.052),rng.uniform(0,6),lod,i%3==0,fragment=True)
    elif id=='KelpWrack_A':
        for i in range(15):
            ribbon(m,(rng.uniform(-.22,.22),rng.uniform(-.13,.13),rng.uniform(0,.022)),rng.uniform(0,6),rng.uniform(.24,.65),rng.uniform(.045,.085),701+i,lod)
        for i in range(14):
            ribbon(m,(rng.uniform(-.25,.25),rng.uniform(-.22,.22),rng.uniform(0,.02)),rng.uniform(0,6),rng.uniform(.06,.17),rng.uniform(.018,.032),760+i,lod)
        for i in range(5):tube(m,[(-.1+i*.027,j*.018-.1,.025+.01*math.sin(j)) for j in range(20)],[.003*(1-j/23) for j in range(20)],lod,2,30+i)
    elif id=='BladderWrack_A':
        for i in range(12):
            start=(rng.uniform(-.18,.18),rng.uniform(-.15,.15),rng.uniform(0,.035));angle=rng.uniform(0,6);length=rng.uniform(.18,.40)
            ribbon(m,start,angle,length,.020,800+i,lod)
            for j in range(3):
                t=(j+1)*.2;br=random.Random(800+i);br.random();bend=br.uniform(-.7,.7)
                xx=length*bend*t*t;yy=length*t
                pt=(start[0]+math.cos(angle)*xx-math.sin(angle)*yy,start[1]+math.sin(angle)*xx+math.cos(angle)*yy,start[2]+.010+length*.09*math.sin(t*math.pi))
                ribbon(m,pt,angle+(-1 if j%2 else 1)*.9,length*.35,.015,880+i*7+j,lod)
                for sign in [-1,1]:bladder(m,(pt[0]+sign*.006*math.cos(angle),pt[1]+sign*.006*math.sin(angle),pt[2]+.002),.0065,lod)
    else:
        ns=[50,25,12][lod];path=[];r=[]
        for j in range(ns+1):
            t=j/ns;path.append(((t-.5)*1.18,.046*math.sin(t*7)+.009*math.sin(t*18),.08+.025*math.sin(t*3.5)+.01*math.sin(t*14)))
            r.append(.055*(.78+.22*math.sin(t*math.pi))*(.82+.18*math.sin(t*23+.5))*(.72+.28*min(1,t*9+.2, (1-t)*9+.2)))
        tube(m,path,r,lod)
        for i in range(4):
            start=Vector(path[int(ns*(.18+i*.17))]);pts=[];rr=[]
            for j in range([16,10,6][lod]):
                t=j/([16,10,6][lod]-1);pts.append(tuple(start+Vector((t*.09+.013*math.sin(t*4),(-1 if i%2 else 1)*t*(.12+i*.016),t*.055+.01*math.sin(t*5)))))
                rr.append(.028*(1-t*.64)*(1+.12*math.sin(t*17)))
            tube(m,pts,rr,lod,3,150+i)
    ob=m.object(id+'_LOD'+str(lod),mat)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    tri=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    minz=min(v.co.z for v in ob.data.vertices)
    for v in ob.data.vertices:v.co.z-=minz
    ob.data.update();ob.data.calc_tangents()
    assert all(math.isfinite(c) for v in ob.data.vertices for c in v.co)
    assert all(math.isfinite(c) for l in ob.data.loops for c in (*l.normal,*l.tangent))
    return ob

def bounds(ob):
    lo=[min(v.co[i] for v in ob.data.vertices) for i in range(3)];hi=[max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    return {'min':[lo[0],lo[2],-hi[1]],'max':[hi[0],hi[2],-lo[1]]}

def meta(path):
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'bfjord:shore-detail:v1:'+path.relative_to(CAT).as_posix()).hex
    if path.suffix=='.fbx':
        text=(ART/'asset-catalog/Assets/BFjord/OriginalFoliage/Models/WoodSorrel_A.fbx.meta').read_text()
        import re;text=re.sub(r'guid: [a-f0-9]+','guid: '+guid,text,count=1)
    elif path.suffix=='.png':
        normal='Normal' in path.name;color='Color' in path.name
        text=f'''fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: {int(color)}\n  isReadable: 0\n  textureType: {int(normal)}\n  textureShape: 1\n  alphaUsage: 1\n  alphaIsTransparency: 0\n  maxTextureSize: 2048\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 2\n    aniso: 8\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n  userData: Shore detail CC0. OpenGL normal +Y; R metal G AO B height A smoothness.\n'''
    else:text=f'fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: CC0-1.0\n'
    path.with_name(path.name+'.meta').write_text(text)

def sand():
    source=OUT/'Sources';arrays={}
    for role in ['diff','nor_gl','arm','disp']:
        p=source/f'sand_02_{role}_2k.png';im=bpy.data.images.load(str(p));im.colorspace_settings.name='Non-Color';a=np.empty(N*N*4,np.float32);im.pixels.foreach_get(a);arrays[role]=a.reshape((N,N,4))
    color=arrays['diff'][:,:,:3];normal=arrays['nor_gl'][:,:,:3];arm=arrays['arm'];height=arrays['disp'][:,:,0]
    png('BeachSand_Color.png',color,True);png('BeachSand_NormalGL.png',normal)
    png('BeachSand_Mask.png',np.stack([np.zeros_like(height),arm[:,:,0],height,1-arm[:,:,1]],2))
    # Original tileable low-relief wind ripple layer combined with the verified scan.
    y,x=np.mgrid[0:N,0:N]/N;phase=y*math.tau*12+.42*np.sin(x*math.tau*3)+.15*np.sin(x*math.tau*7)
    ripple=np.sin(phase)+.22*np.sin(phase*2)
    ny=normal*2-1;dx=.42*np.cos(x*math.tau*3)*math.tau*3+.15*np.cos(x*math.tau*7)*math.tau*7
    slope=(np.cos(phase)+.44*np.cos(phase*2))*.009/2.1
    ny[:,:,0]-=slope*dx;ny[:,:,1]-=slope*math.tau*12;ny/=np.linalg.norm(ny,axis=2)[:,:,None]
    png('RippleSand_Color.png',color,True);png('RippleSand_NormalGL.png',ny*.5+.5)
    png('RippleSand_Mask.png',np.stack([np.zeros_like(height),arm[:,:,0],np.clip(height*.55+.22+ripple*.14,0,1),1-arm[:,:,1]],2))

def main():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    mat=material(maps());sand();variants=[]
    for id in IDS:
        lods=[];objects=[]
        for lod in range(3):
            bpy.ops.object.select_all(action='DESELECT');ob=build(id,lod,mat)
            # Blank material copy prevents FBX exporters serializing machine-local image paths.
            ob.data.materials.clear();slot=bpy.data.materials.get('BFjord_ShoreAtlas_Export') or bpy.data.materials.new('BFjord_ShoreAtlas_Export');ob.data.materials.append(slot)
            p=OUT/'Models'/f'{id}.fbx'
            lods.append({'path':'Models/'+p.name,'meshName':ob.name,'triangles':len(ob.data.polygons),'bounds':bounds(ob),'screenRelativeHeight':[.55,.22,.055][lod]})
            objects.append(ob)
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(p),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
        for lod,ob in enumerate(objects):
            ob.data.materials.clear();ob.data.materials.append(mat);ob.hide_render=lod!=0;ob.hide_set(lod!=0)
        variants.append({'id':id,'displayName':id.replace('_A',''),'materialSlots':['BFjord_ShoreAtlas'],'groundOffsetMeters':0,'bounds':lods[0]['bounds'],'lods':lods})
    for im in bpy.data.images:
        if im.filepath:im.filepath=bpy.path.relpath(im.filepath,start=str(OUT/'Sources'))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Sources/ShoreDetail.blend'))
    data={'schemaVersion':1,'id':'bfjord-shore-detail','license':'CC0-1.0','coordinateSystem':'Unity metres; +Y up; bottom ground pivot','source':'Original Blender geometry and atlas; Poly Haven Sand 02 by Charlotte Baglioni CC0. Original wind-ripple modulation.','material':{'id':'BFjord_ShoreAtlas','color':'Textures/ShoreAtlas_Color.png','normal':'Textures/ShoreAtlas_NormalGL.png','mask':'Textures/ShoreAtlas_Mask.png','channels':'Mask R metallic=0 G AO B height A smoothness. Normal OpenGL +Y; no green flip.','opaque':True,'resolution':2048},'sand':{'tileMeters':2.1,'dry':['BeachSand_Color.png','BeachSand_NormalGL.png','BeachSand_Mask.png'],'ripple':['RippleSand_Color.png','RippleSand_NormalGL.png','RippleSand_Mask.png'],'wetShading':{'baseColorMultiplier':[.62,.64,.65,1],'smoothnessRange':[.42,.72],'normalScale':.5},'rippleHeightMeters':.009,'rippleWavelengthMeters':.175},'variants':variants,'limitations':['Authored procedural details, not scanned shell or wrack models.','FBX reimport is a software check; Unity/URP, runtime costs and iPad visual acceptance are root integration work.','Sand source contains existing subtle footprints; original wind ripples supplement that source.']}
    (OUT/'manifest.json').write_text(json.dumps(data,indent=2)+'\n')
    for folder in ['Models','Textures']:
        for p in (OUT/folder).iterdir():shutil.copy2(p,CAT/folder/p.name)
    shutil.copy2(OUT/'manifest.json',CAT/'manifest.json');shutil.copy2(OUT/'Sources/sand-source.json',CAT/'sand-source.json')
    for p in CAT.rglob('*'):
        if p.is_file() and p.suffix!='.meta':meta(p)
    entries=[]
    for p in sorted(CAT.rglob('*')):
        if p.is_file():entries.append({'path':p.relative_to(CAT.parents[2]).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    (OUT/'catalog-additions.json').write_text(json.dumps({'schemaVersion':1,'files':entries},indent=2)+'\n')
    print('SHORE_BUILD_COMPLETE',json.dumps({'variants':len(variants),'catalogBytes':sum(p['bytes'] for p in entries)}))

if __name__=='__main__':
    import sys
    if '--colors-only' in sys.argv:
        COLOR_ONLY=True
        maps();sand()
        for p in (OUT/'Textures').glob('*Color.png'):shutil.copy2(p,CAT/'Textures'/p.name)
        entries=[]
        for p in sorted(CAT.rglob('*')):
            if p.is_file():entries.append({'path':p.relative_to(CAT.parents[2]).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
        (OUT/'catalog-additions.json').write_text(json.dumps({'schemaVersion':1,'files':entries},indent=2)+'\n')
        print('SHORE_COLOR_REPACK_COMPLETE')
    else:main()
