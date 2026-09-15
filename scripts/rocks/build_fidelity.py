# SPDX-License-Identifier: MIT
"""Original eroded rock additions. Blender 5.x, CPU Cycles, no external add-ons.
Keeps the original six assets; appends/replaces only the three fidelity IDs.
"""
import argparse
import importlib.util
import json
import math
from pathlib import Path
import random
import sys
import bpy
from mathutils import Vector, noise

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('rocks', HERE / 'build_rocks.py')
r = importlib.util.module_from_spec(spec); spec.loader.exec_module(r)
OUT = r.OUT
RECIPES = [
    ('coastal_outcrop', 'Eroded coastal outcrop', (8, 5, 4), 1401),
    ('river_ledge', 'Weathered river ledge', (6, 4, 1.8), 1423),
    ('fractured_boulder_cluster', 'Fractured boulder cluster', (4.8, 4, 3.2), 1451),
]

def mass(name, size, position, seed, rounded=.55):
    rng = random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=6, radius=1)
    obj = bpy.context.object; obj.name = name
    offset = Vector((rng.uniform(0,30),rng.uniform(0,30),rng.uniform(0,30)))
    # Unequal oblique fracture planes, independent of the noise displacement.
    planes = []
    for i in range(7):
        normal = Vector((rng.uniform(-1,1),rng.uniform(-1,1),rng.uniform(-.6,1))).normalized()
        planes.append((normal, rng.uniform(.78,1.02)))
    for vertex in obj.data.vertices:
        p = vertex.co.copy()
        p = Vector(tuple(math.copysign(abs(v)**rounded, v) for v in p))
        for normal, distance in planes:
            if p.dot(normal)>distance: p -= normal*(p.dot(normal)-distance)
        n = noise.noise(p*2.8+offset)
        # Broad erosion and smaller granular relief; separate scales avoid a melted sphere.
        fine = noise.noise(p*19+offset)*.022 + noise.noise(p*57+offset)*.008
        erosion = n*.075 + noise.noise(p*7.2+offset)*.032 + fine
        p += p.normalized()*erosion
        # Two nonuniform inclined joints become narrow erosional channels, not stacked rings.
        seam = p.z + p.x*.18 + noise.noise(p*4+offset)*.07
        groove = math.exp(-((seam-.13)/.038)**2)*.075 + math.exp(-((seam+.48)/.029)**2)*.045
        # Weathered vertical joints interrupt the broad cliff planes. Their
        # lateral paths wander with height and never repeat around the whole rock.
        joint_coordinate = p.x + .14*p.z + noise.noise(Vector((p.z*3,p.y*2,offset.x)))*.12
        vertical = sum(math.exp(-((joint_coordinate-c)/width)**2)*depth for c,width,depth in ((-.52,.028,.065),(.28,.021,.05)))
        p.x *= 1-groove; p.y *= 1-groove-vertical
        vertex.co = (p.x*size[0]/2, p.y*size[1]/2, max(0,(p.z+1)*size[2]/2-size[2]*.10))
    obj.location=position
    for face in obj.data.polygons: face.use_smooth=True
    return obj

def shape(asset_id, size, seed):
    if asset_id=='coastal_outcrop':
        specs=[((5.6,3.9,4),(0,.45,0),.42),((3.3,3.4,2.7),(-2.4,.05,0),.52),((2.5,2.7,2.0),(2.5,.1,0),.49),((2.4,2.2,1.15),(-1.6,-1.8,0),.62),((1.3,1.4,.8),(2.8,-1.4,0),.7)]
    elif asset_id=='river_ledge':
        specs=[((5.4,3.5,1.55),(.1,.2,0),.43),((3.6,2.6,.9),(-.6,-1.1,0),.58),((1.6,1.7,.8),(2.1,-.75,0),.68)]
    else:
        specs=[((2.7,2.4,3.2),(-.6,.55,0),.49),((2.4,2.2,2.15),(1.05,-.05,0),.47),((2.3,1.7,1.4),(-1.1,-1.0,0),.62),((1.15,1.2,.8),(1.5,-1.3,0),.67),((.85,.9,.6),(-1.9,.05,0),.73)]
    objects=[]
    for i,(dimensions,location,rounded) in enumerate(specs):
        obj=mass(asset_id+str(i),dimensions,location,seed+i*31,rounded)
        obj.rotation_euler.z=(i*.71)%1.1-.3
        objects.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]; bpy.ops.object.join()
    obj=bpy.context.object; bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    normalize(obj,size)
    return obj

def normalize(obj,size):
    low=[min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    high=[max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    for v in obj.data.vertices:
        for i in range(3):v.co[i]=(v.co[i]-low[i])/(high[i]-low[i])*size[i]-(size[i]/2 if i<2 else 0)
    obj.data.update()

def high_material():
    mat=bpy.data.materials.new('Original weathered stone high source');mat.use_nodes=True
    n,l=mat.node_tree.nodes,mat.node_tree.links;bs=n.get('Principled BSDF')
    tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'Textures/Rock_BaseColor.jpg'))
    tex.projection='BOX';tex.projection_blend=.28
    coord=n.new('ShaderNodeTexCoord');scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=.5
    l.new(coord.outputs['Object'],scale.inputs[0]);l.new(scale.outputs[0],tex.inputs['Vector'])
    broad=n.new('ShaderNodeTexNoise');broad.inputs['Scale'].default_value=.95;broad.inputs['Detail'].default_value=3
    l.new(coord.outputs['Object'],broad.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.22;ramp.color_ramp.elements[0].color=(.36,.38,.38,1)
    ramp.color_ramp.elements[1].position=.76;ramp.color_ramp.elements[1].color=(.82,.80,.73,1)
    l.new(broad.outputs['Fac'],ramp.inputs['Fac'])
    mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.32
    l.new(tex.outputs['Color'],mix.inputs[1]);l.new(ramp.outputs['Color'],mix.inputs[2])
    minerals=n.new('ShaderNodeMixRGB');minerals.blend_type='MIX';minerals.inputs[0].default_value=.18;minerals.inputs[2].default_value=(.56,.53,.47,1)
    l.new(mix.outputs[0],minerals.inputs[1]);l.new(minerals.outputs[0],bs.inputs['Base Color'])
    grain=n.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=31;grain.inputs['Detail'].default_value=3
    l.new(coord.outputs['Object'],grain.inputs['Vector'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.35;bump.inputs['Distance'].default_value=.055
    l.new(grain.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=.87
    return mat

def bake(high,low,asset_id,resolution):
    mat=bpy.data.materials.new('BFjord_'+asset_id);mat.use_nodes=True;low.data.materials.clear();low.data.materials.append(mat)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.20;scene.render.bake.max_ray_distance=.4;scene.render.bake.margin=12
    paths={}
    for kind,suffix in [('DIFFUSE','BaseColor'),('NORMAL','NormalGL')]:
        image=bpy.data.images.new(asset_id+'_'+suffix,width=resolution,height=resolution,alpha=False)
        image.colorspace_settings.name='Non-Color' if kind=='NORMAL' else 'sRGB'
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;mat.node_tree.nodes.active=node
        r.activate(low);high.select_set(True)
        if kind=='DIFFUSE': scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
        bpy.ops.object.bake(type=kind)
        path=f'Textures/{asset_id}_{suffix}.png';image.filepath_raw=str(OUT/path);image.file_format='PNG';image.save();paths[suffix]=path
        bs=mat.node_tree.nodes.get('Principled BSDF')
        if kind=='DIFFUSE':mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
        else:
            normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.8
            mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
        bs.inputs['Roughness'].default_value=.86
    return mat, paths

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--resolution',type=int,default=1024);parser.add_argument('--only',choices=[x[0] for x in RECIPES]);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.resolution not in (512,1024,2048):raise ValueError('Bake resolution must be512,1024or2048')
    manifest=json.loads((OUT/'manifest.json').read_text());added=[]
    for asset_id,label,size,seed in RECIPES:
        if args.only and asset_id!=args.only:continue
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.outliner.orphans_purge(do_recursive=True)
        high=shape(asset_id,size,seed);high.data.materials.append(high_material())
        low=r.duplicate(high,asset_id+'_LOD0');dec=low.modifiers.new('Silhouette preservation','DECIMATE');dec.ratio=6200/r.triangles(low);r.apply(low,dec)
        normalize(low,size)
        r.activate(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(62),island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
        mat,paths=bake(high,low,asset_id,args.resolution);high.hide_render=True;high.hide_set(True)
        lods=[]
        for level,target in enumerate((6200,2100,650)):
            lod=r.duplicate(low,asset_id+f'_LOD{level}')
            if level:
                dec=lod.modifiers.new('UV preserving LOD','DECIMATE');dec.ratio=target/r.triangles(low);r.apply(lod,dec)
            path=f'Models/{asset_id}_LOD{level}.fbx';r.export(lod,OUT/path)
            lods.append({'path':path,'triangles':r.triangles(lod),'screenRelativeHeight':(.5,.2,.06)[level]});bpy.data.objects.remove(lod,do_unlink=True)
        col=r.collision(low,asset_id+'_COL');path=f'Models/{asset_id}_COL.fbx';r.export(col,OUT/path);coll_tri=r.triangles(col);bpy.data.objects.remove(col,do_unlink=True)
        variant={'id':asset_id,'displayName':label,'designType':asset_id,'seed':seed,'dimensionsMeters':[size[0],size[2],size[1]],'boundsSize':[size[0],size[2],size[1]],'bounds':{'min':[-size[0]/2,0,-size[1]/2],'max':[size[0]/2,size[2],size[1]/2]},'lods':lods,'colliderPath':path,'colliderTriangles':coll_tri,'materialId':asset_id,'uvLayout':f'unique{args.resolution} baked atlas; 12pixel gutters','highSourceTriangles':r.triangles(high)}
        material={'id':asset_id,'baseColorPath':paths['BaseColor'],'normalPath':paths['NormalGL'],'roughnessPath':'Textures/Rock_Roughness.png','metallicSmoothnessPath':'Textures/Rock_MetallicSmoothness.png'}
        manifest['variants']=[v for v in manifest['variants'] if v['id']!=asset_id]+[variant]
        manifest['materials']=[v for v in manifest['materials'] if v['id']!=asset_id]+[material]
        # Editable source retains both sculpt-equivalent high mesh and exported low with baked material.
        (OUT/'Sources').mkdir(exist_ok=True)
        for image in bpy.data.images:
            if image.filepath and Path(bpy.path.abspath(image.filepath)).is_relative_to(OUT):
                image.filepath = '//../' + str(Path(bpy.path.abspath(image.filepath)).relative_to(OUT))
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'Sources/{asset_id}.blend'),compress=True)
        (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');added.append(variant)
        print('FIDELITY_ROCK_COMPLETE '+json.dumps(variant),flush=True)
    print('FIDELITY_LIBRARY_COMPLETE '+str(len(added)),flush=True)

if __name__=='__main__':main()
