# SPDX-License-Identifier: MIT
"""Original fractured/weathered rocks. Run with Blender --background --python.

Geometry is authored from irregular geological wedges, independently of third-party
generators. UVs use meters / 2 to keep the shared material scale across the library.
"""
import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector, noise

OUT = next(candidate for parent in Path(__file__).resolve().parents
           for candidate in (parent / 'assets/BFjordTools/Rocks', parent / 'art/BFjordTools/Rocks')
           if (candidate / 'manifest.json').is_file())
RECIPES = [
    ('granite_boulder', 'Granite boulder', 701, (3.1, 2.5, 2.25)),
    ('river_stone', 'Rounded river stone', 809, (2.7, 1.9, 1.15)),
    ('stratified_outcrop', 'Stratified outcrop', 911, (5.8, 3.5, 3.2)),
    ('cliff_slab', 'Fractured cliff slab', 1013, (5.0, 1.9, 4.8)),
    ('scree_cluster', 'Scree cluster', 1117, (4.2, 3.1, 1.3)),
    ('upright_crag', 'Upright crag', 1219, (2.6, 2.2, 5.0)),
]


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply(obj, modifier):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def image_pixels(name, values, path):
    h, w, _ = values.shape
    img = bpy.data.images.new(name, width=w, height=h, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(values.astype(np.float32).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    return img


def material():
    # Roughness is an original bounded approximation, not a measured scan channel.
    rough = np.ones((256, 256, 4), dtype=np.float32)
    y, x = np.mgrid[0:256, 0:256]
    value = .84 + .025 * np.sin(x * math.tau / 256 * 4) * np.cos(y * math.tau / 256 * 5)
    rough[:, :, :3] = value[:, :, None]
    rough_image = image_pixels('Rock roughness', rough, OUT / 'Textures/Rock_Roughness.png')
    packed = np.zeros_like(rough)
    packed[:, :, 3] = 1 - value
    image_pixels('Rock metallic smoothness', packed, OUT / 'Textures/Rock_MetallicSmoothness.png')
    mat = bpy.data.materials.new('BFjord_Rock')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    color = nodes.new('ShaderNodeTexImage')
    color.image = bpy.data.images.load(str(OUT / 'Textures/Rock_BaseColor.jpg'))
    links.new(color.outputs['Color'], bsdf.inputs['Base Color'])
    normal = nodes.new('ShaderNodeTexImage')
    normal.image = bpy.data.images.load(str(OUT / 'Textures/Rock_NormalGL.jpg'))
    normal.image.colorspace_settings.name = 'Non-Color'
    nmap = nodes.new('ShaderNodeNormalMap')
    nmap.inputs['Strength'].default_value = .65
    links.new(normal.outputs['Color'], nmap.inputs['Color'])
    links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    rnode = nodes.new('ShaderNodeTexImage')
    rnode.image = rough_image
    links.new(rnode.outputs['Color'], bsdf.inputs['Roughness'])
    return mat


def wedge(name, rng, size, location=(0, 0, 0), rounded=False):
    sx, sy, sz = size
    if rounded:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=24)
        obj = bpy.context.object
        offset = Vector((rng.random() * 3, .2, .9))
        for v in obj.data.vertices:
            p = v.co.copy()
            amount = 1 + .055 * noise.noise(p * 2.3 + offset)
            v.co = (p.x * sx * .5 * amount, p.y * sy * .5 * amount,
                    max(0, (p.z * .5 + .5) * sz * amount - sz * .075))
    else:
        # Three uneven polygonal rings form broad fracture planes, then bevel
        # rounds only exposed seams. This preserves rock mass and large planes.
        verts = []
        sides = 7
        angles = [math.tau * i / sides + rng.uniform(-.16, .16) for i in range(sides)]
        for level, z in enumerate((0, .48, 1)):
            radius = (.83, 1.0, rng.uniform(.46, .76))[level]
            driftx = (.0, -.07, .10)[level] * sx
            drifty = (.0, .02, -.08)[level] * sy
            for a in angles:
                jitter = rng.uniform(.82, 1.12)
                zz = z * sz + (rng.uniform(-.12, .09) * sz if level == 2 else 0)
                verts.append((math.cos(a) * sx * .5 * radius * jitter + driftx,
                              math.sin(a) * sy * .5 * radius * jitter + drifty, zz))
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        for p in verts:
            bm.verts.new(p)
        bmesh.ops.convex_hull(bm, input=list(bm.verts), use_existing_faces=False)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bevel = obj.modifiers.new('Weathered fracture edges', 'BEVEL')
        bevel.width = min(size) * .048
        bevel.segments = 3
        apply(obj, bevel)
        tri = obj.modifiers.new('Surface tessellation', 'TRIANGULATE')
        apply(obj, tri)
        sub = obj.modifiers.new('Weathering samples', 'SUBSURF')
        sub.subdivision_type = 'SIMPLE'
        sub.levels = 2
        apply(obj, sub)
        obj.data.update()
        offset = Vector((rng.random() * 10, rng.random() * 10, rng.random() * 10))
        for v in obj.data.vertices:
            base = v.co.copy()
            amplitude = min(size) * .018
            displacement = noise.noise(base * 3.2 + offset) + .3 * noise.noise(base * 11 + offset)
            v.co += v.normal * displacement * amplitude
            if base.z < .025 * sz:
                v.co.z = 0
    obj.name = name
    obj.location = location
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def build_shape(asset_id, seed, size):
    rng = random.Random(seed)
    pieces = []
    if asset_id == 'stratified_outcrop':
        for i in range(4):
            pieces.append(wedge(f'{asset_id}_{i}', rng,
                                (size[0] * (1 - i * .065), size[1] * (1 - i * .08), size[2] * .32),
                                (i * .10, math.sin(i) * .12, i * size[2] * .225)))
    elif asset_id == 'scree_cluster':
        for i in range(11):
            a = i * 2.399
            radius = .42 if i == 0 else .7 + .25 * (i % 3)
            stone = wedge(f'{asset_id}_{i}', rng, (rng.uniform(.7, 1.55), rng.uniform(.55, 1.1), rng.uniform(.45, 1.15)),
                          (math.cos(a) * radius, math.sin(a) * radius * .75, 0))
            stone.rotation_euler.z = rng.random() * math.tau
            pieces.append(stone)
    elif asset_id == 'cliff_slab':
        pieces.append(wedge(asset_id, rng, size))
        pieces.append(wedge(asset_id + '_buttress', rng, (size[0] * .38, size[1] * .72, size[2] * .65),
                            (-size[0] * .32, .12, 0)))
    elif asset_id == 'upright_crag':
        pieces.append(wedge(asset_id, rng, size))
        pieces.append(wedge(asset_id + '_spur', rng, (size[0] * .47, size[1] * .58, size[2] * .57),
                            (size[0] * .32, .18, 0)))
    else:
        pieces.append(wedge(asset_id, rng, size, rounded=asset_id == 'river_stone'))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mins = Vector(tuple(min(v.co[a] for v in obj.data.vertices) for a in range(3)))
    maxs = Vector(tuple(max(v.co[a] for v in obj.data.vertices) for a in range(3)))
    center = (mins + maxs) * .5
    for v in obj.data.vertices:
        v.co.x = (v.co.x - center.x) * size[0] / (maxs.x - mins.x)
        v.co.y = (v.co.y - center.y) * size[1] / (maxs.y - mins.y)
        v.co.z = (v.co.z - mins.z) * size[2] / (maxs.z - mins.z)
    uv = obj.data.uv_layers.new(name='UVMap')
    obj.data.update()
    # Per-face dominant projection in world meters keeps texel density stable
    # and provides deterministic UVs on every exported LOD. Unity may triplanar.
    for face in obj.data.polygons:
        axis = max(range(3), key=lambda a: abs(face.normal[a]))
        axes = ((1, 2), (0, 2), (0, 1))[axis]
        for li in face.loop_indices:
            p = obj.data.vertices[obj.data.loops[li].vertex_index].co
            uv.data[li].uv = (p[axes[0]] / 2, p[axes[1]] / 2)
    return obj


def duplicate(obj, name):
    result = obj.copy()
    result.data = obj.data.copy()
    result.name = name
    bpy.context.collection.objects.link(result)
    return result


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def export(obj, path):
    activate(obj)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                             bake_space_transform=True, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', use_tspace=True, add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', use_custom_props=False)


def collision(obj, name):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    # Uniformly sampled hull followed by collapse bounds collision cost.
    for vertex in obj.data.vertices:
        bm.verts.new(vertex.co)
    result = bmesh.ops.convex_hull(bm, input=list(bm.verts), use_existing_faces=False)
    bmesh.ops.delete(bm, geom=result['geom_interior'], context='VERTS')
    bm.to_mesh(mesh)
    bm.free()
    col = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(col)
    dec = col.modifiers.new('Bounded collision hull', 'DECIMATE')
    dec.ratio = min(1, 160 / max(1, triangles(col)))
    apply(col, dec)
    return col


def review_scene(objects):
    for i, obj in enumerate(objects):
        obj.location = ((i % 3 - 1) * 7, (i // 3) * 7, 0)
    bpy.ops.mesh.primitive_plane_add(size=200)
    ground = bpy.context.object
    mat = bpy.data.materials.new('Review clay')
    mat.diffuse_color = (.13, .16, .14, 1)
    ground.data.materials.append(mat)
    bpy.ops.object.camera_add(location=(17, -28, 23))
    cam = bpy.context.object
    cam.rotation_euler = (Vector((0, 3, 1.4)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 26
    scene = bpy.context.scene
    scene.camera = cam
    bpy.ops.object.light_add(type='AREA', location=(-7, -8, 20))
    light = bpy.context.object
    light.data.energy = 3800
    light.data.shape = 'DISK'
    light.data.size = 9
    bpy.ops.object.light_add(type='SUN', location=(4, 3, 12))
    bpy.context.object.rotation_euler = (.5, -.6, -.5)
    bpy.context.object.data.energy = 2
    scene.world.color = (.24, .24, .24)
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(OUT / 'Review/library-contact.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'original.blend'))
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', choices=[r[0] for r in RECIPES])
    parser.add_argument('--seed', type=int)
    parser.add_argument('--dimensions', nargs=3, type=float, metavar=('WIDTH', 'DEPTH', 'HEIGHT'))
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for part in ('Models', 'Textures', 'Review'):
        (OUT / part).mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = 'METRIC'
    mat = material()
    variants, originals = [], []
    for asset_id, label, default_seed, default_size in RECIPES:
        if args.only and args.only != asset_id:
            continue
        seed = args.seed if args.seed is not None else default_seed
        size = args.dimensions or default_size
        if any(not math.isfinite(v) or v <= 0 or v > 100 for v in size):
            raise ValueError('Dimensions must be finite, positive, and no greater than 100 m.')
        obj = build_shape(asset_id, seed, size)
        obj.data.materials.append(mat)
        originals.append(obj)
        lods = []
        base_triangles = triangles(obj)
        for level, target in enumerate((2400, 850, 230)):
            lod = duplicate(obj, f'{asset_id}_LOD{level}')
            dec = lod.modifiers.new('LOD simplification', 'DECIMATE')
            dec.ratio = min(1, target / base_triangles)
            apply(lod, dec)
            path = f'Models/{asset_id}_LOD{level}.fbx'
            export(lod, OUT / path)
            lods.append({'path': path, 'screenRelativeHeight': (.5, .2, .06)[level], 'triangles': triangles(lod)})
            bpy.data.objects.remove(lod, do_unlink=True)
        col = collision(obj, asset_id + '_COL')
        col_path = f'Models/{asset_id}_COL.fbx'
        export(col, OUT / col_path)
        col_triangles = triangles(col)
        bpy.data.objects.remove(col, do_unlink=True)
        unity_size = [size[0], size[2], size[1]]
        variants.append({'id': asset_id, 'displayName': label, 'designType': asset_id, 'seed': seed,
                         'dimensionsMeters': unity_size, 'boundsSize': unity_size,
                         'bounds': {'min': [-size[0]/2, 0, -size[1]/2], 'max': [size[0]/2, size[2], size[1]/2]},
                         'lods': lods, 'colliderPath': col_path, 'colliderTriangles': col_triangles,
                         'materialId': 'rock', 'uvMetersPerRepeat': 2})
    manifest = {'schemaVersion': 1, 'id': 'bfjord-original-rocks', 'license': 'CC0-1.0',
                'coordinateSystem': 'Unity +Y up, meters; origin at bottom horizontal center',
                'source': 'Original Blender geometry; ambientCG Rock030 CC0 color and OpenGL normal maps',
                'materials': [{'id': 'rock', 'baseColorPath': 'Textures/Rock_BaseColor.jpg',
                               'normalPath': 'Textures/Rock_NormalGL.jpg', 'roughnessPath': 'Textures/Rock_Roughness.png',
                               'metallicSmoothnessPath': 'Textures/Rock_MetallicSmoothness.png'}],
                'variants': variants}
    (OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    if not args.no_render:
        review_scene(originals)
    print('ROCK_LIBRARY_COMPLETE ' + json.dumps({v['id']: [x['triangles'] for x in v['lods']] for v in variants}))


if __name__ == '__main__':
    main()
