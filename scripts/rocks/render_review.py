# SPDX-License-Identifier: MIT
"""Render the actual exported LODs with neutral clay and source PBR materials."""
import importlib.util
import json
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('rocks', HERE / 'build_rocks.py')
rocks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rocks)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
manifest = json.loads((rocks.OUT / 'manifest.json').read_text())
pbr = rocks.material()
clay = bpy.data.materials.new('Neutral clay')
clay.use_nodes = True
clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.34, .37, .38, 1)
clay.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .82
models = []
for row, variant in enumerate(manifest['variants']):
    for column, lod in enumerate(variant['lods']):
        bpy.ops.import_scene.fbx(filepath=str(rocks.OUT / lod['path']))
        obj = next(o for o in bpy.context.selected_objects if o.type == 'MESH')
        obj.data.materials.clear()
        obj.data.materials.append(pbr)
        scale = 3.2 / max(variant['boundsSize'])
        obj.scale *= scale
        obj.location = ((column - 1) * 5, (5 - row) * 4.3, 0)
        models.append(obj)
        bpy.ops.object.text_add(location=(obj.location.x-1.9, obj.location.y-1.8, .02))
        label = bpy.context.object
        label.data.body = f"{variant['displayName']}\nLOD {column} | {lod['triangles']} triangles"
        label.data.size = .21
        label.data.extrude = .001
        label.data.materials.append(clay)
bpy.ops.mesh.primitive_plane_add(size=200)
plane = bpy.context.object
plane.location.z = -.015
ground = bpy.data.materials.new('Ground')
ground.diffuse_color = (.18, .2, .2, 1)
plane.data.materials.append(ground)
scene = bpy.context.scene
bpy.ops.object.camera_add(location=(12, -19, 37))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 10, 0)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 33
scene.camera = camera
bpy.ops.object.light_add(type='AREA', location=(-10, -8, 25))
bpy.context.object.data.energy = 5200
bpy.context.object.data.size = 12
bpy.ops.object.light_add(type='SUN')
bpy.context.object.rotation_euler = (.45, -.6, -.5)
bpy.context.object.data.energy = 2
scene.world.color = (.3, .3, .3)
scene.render.engine = 'CYCLES'
scene.cycles.samples = 20
scene.render.resolution_x = 1800
scene.render.resolution_y = 2200
scene.render.resolution_percentage = 100
for mode in ('pbr', 'clay'):
    if mode == 'clay':
        for obj in models:
            obj.data.materials[0] = clay
    scene.render.filepath = str(rocks.OUT / f'Review/exported-lods-{mode}.png')
    bpy.ops.render.render(write_still=True)
print('ROCK_EXPORTED_LOD_REVIEW_COMPLETE')
