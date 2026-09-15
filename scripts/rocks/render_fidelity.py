# SPDX-License-Identifier: MIT
"""Review actual exported fidelity FBXs and their baked maps, never high meshes."""
import json
from pathlib import Path
import bpy
from mathutils import Vector
OUT = next(candidate for parent in Path(__file__).resolve().parents
           for candidate in (parent / 'assets/BFjordTools/Rocks', parent / 'art/BFjordTools/Rocks')
           if (candidate / 'manifest.json').is_file())
manifest=json.loads((OUT/'manifest.json').read_text());variants=manifest['variants'][-3:]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
clay=bpy.data.materials.new('Neutral clay');clay.use_nodes=True;clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.36,.39,.40,1)
clay.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85
models=[]
for row,v in enumerate(variants):
    source=next(m for m in manifest['materials'] if m['id']==v['materialId'])
    mat=bpy.data.materials.new(v['id']);mat.use_nodes=True;n,l=mat.node_tree.nodes,mat.node_tree.links;bs=n.get('Principled BSDF');bs.inputs['Roughness'].default_value=.86
    c=n.new('ShaderNodeTexImage');c.image=bpy.data.images.load(str(OUT/source['baseColorPath']));l.new(c.outputs['Color'],bs.inputs['Base Color'])
    t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(OUT/source['normalPath']));t.image.colorspace_settings.name='Non-Color'
    normal=n.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.8;l.new(t.outputs['Color'],normal.inputs['Color']);l.new(normal.outputs['Normal'],bs.inputs['Normal'])
    for column,lod in enumerate(v['lods']):
        bpy.ops.import_scene.fbx(filepath=str(OUT/lod['path']));obj=next(o for o in bpy.context.selected_objects if o.type=='MESH')
        obj.data.materials.clear();obj.data.materials.append(mat);obj.scale*=4.5/max(v['boundsSize']);obj.location=((column-1)*6.3,(2-row)*5.6,0);models.append(obj)
        bpy.ops.object.text_add(location=(obj.location.x-2.6,obj.location.y-2.5,.01));label=bpy.context.object
        label.data.body=f"{v['displayName']}\nLOD {column} / {lod['triangles']} triangles";label.data.size=.22;label.data.materials.append(clay)
bpy.ops.mesh.primitive_plane_add(size=200);ground=bpy.context.object;ground.location.z=-.02
mat=bpy.data.materials.new('Ground');mat.diffuse_color=(.11,.13,.14,1);ground.data.materials.append(mat)
bpy.ops.object.camera_add(location=(11,-23,25));cam=bpy.context.object;cam.rotation_euler=(Vector((0,5.5,.4))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=23
scene=bpy.context.scene;scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-8,-8,18));bpy.context.object.data.energy=3000;bpy.context.object.data.size=9
bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.55,-.6,-.45);bpy.context.object.data.energy=2
scene.world.color=(.25,.25,.25);scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=20
scene.render.resolution_x=1500;scene.render.resolution_y=1250;scene.render.resolution_percentage=100
for mode in ('pbr','clay'):
    if mode=='clay':
        for obj in models:obj.data.materials[0]=clay
    scene.render.filepath=str(OUT/f'Review/fidelity-lods-{mode}.png');bpy.ops.render.render(write_still=True)
print('FIDELITY_REVIEW_COMPLETE')
