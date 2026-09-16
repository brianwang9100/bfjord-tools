# SPDX-License-Identifier: MIT
"""Reimport the admitted FBX bytes, validate mesh data, render individual review plates."""
import bpy,sys,json,math,hashlib,importlib.util
from pathlib import Path
from mathutils import Vector
spec=importlib.util.spec_from_file_location('shore',Path(__file__).with_name('build_shore.py'));s=importlib.util.module_from_spec(spec);spec.loader.exec_module(s)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
ims=[]
for suffix in ['Color','NormalGL','Mask']:
    im=bpy.data.images.load(str(s.OUT/'Textures'/f'ShoreAtlas_{suffix}.png'));im.colorspace_settings.name='sRGB' if suffix=='Color' else 'Non-Color';ims.append(im)
mat=s.material(ims)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.cycles.device='CPU'
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world.color=(.32,.32,.32);scene.view_settings.view_transform='AgX'
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
bpy.ops.object.light_add(type='AREA',location=(1,-1.5,2.5));key=bpy.context.object;key.data.energy=220;key.data.shape='DISK';key.data.size=2
key.rotation_euler=(Vector((0,0,0))-key.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='AREA',location=(-1,1,1.8));fill=bpy.context.object;fill.data.energy=100;fill.data.size=3
fill.rotation_euler=(Vector((0,0,0))-fill.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.001));ground=bpy.context.object;gmat=bpy.data.materials.new('Studio ground');gmat.diffuse_color=(.16,.18,.19,1);ground.data.materials.append(gmat)
report={'schemaVersion':1,'source':'Fresh Blender FBX import, not original source mesh','variants':[]}
for id in s.IDS:
    before=set(bpy.data.objects);p=s.CAT/'Models'/f'{id}.fbx';bpy.ops.import_scene.fbx(filepath=str(p));obs=[o for o in set(bpy.data.objects)-before if o.type=='MESH'];records=[]
    assert len(obs)==3,(id,len(obs))
    for o in sorted(obs,key=lambda o:o.name):
        o.data.calc_tangents();o.data.calc_loop_triangles()
        assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
        assert all(math.isfinite(c) for l in o.data.loops for c in (*l.normal,*l.tangent))
        assert all(math.isfinite(c) for u in o.data.uv_layers.active.data for c in u.uv)
        assert len(o.data.materials)==1
        zero=sum(t.area<1e-15 for t in o.data.loop_triangles)
        world=[o.matrix_world@v.co for v in o.data.vertices]
        records.append({'name':o.name,'triangles':len(o.data.loop_triangles),'zeroAreaTriangles':zero,'min':[min(v[i] for v in world) for i in range(3)],'max':[max(v[i] for v in world) for i in range(3)],'finiteUVNormalsTangents':True})
        o.hide_render=not o.name.endswith('LOD0');o.data.materials.clear();o.data.materials.append(mat)
    hero=next(o for o in obs if o.name.endswith('LOD0'));vs=[hero.matrix_world@v.co for v in hero.data.vertices]
    lo=Vector(tuple(min(v[i] for v in vs) for i in range(3)));hi=Vector(tuple(max(v[i] for v in vs) for i in range(3)));center=(lo+hi)/2;size=max(hi-lo)
    cam.location=center+Vector((.45,-.72,.82))*size;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=size*1.45
    # Imported metres and +Y-up conversion must return to Blender Z-up.
    assert hi.z-lo.z<size*.8 or id=='ShellFragments_A'
    scene.render.filepath=str(s.OUT/'Review'/f'{id}_reimport.png');bpy.ops.render.render(write_still=True)
    report['variants'].append({'id':id,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'meshes':records})
    for o in obs:bpy.data.objects.remove(o,do_unlink=True)
(s.OUT/'Review/reimport-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('SHORE_REIMPORT_REVIEW_COMPLETE')
