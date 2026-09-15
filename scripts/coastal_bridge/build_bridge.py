# SPDX-License-Identifier: GPL-3.0-or-later
"""Author an original open-spandrel bridge from a bounded design recipe.

Run with Blender 5.2.x --background --factory-startup --disable-autoexec
--python-exit-code 1 --python build_bridge.py -- --recipe RECIPE --output DIRECTORY.
Blender source stays editable; exported meshes use the established Bwork Unity basis.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import tempfile
import zlib


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--concrete-source', type=Path, help='Optional local CC0 map/source JSON')
    parser.add_argument('--skip-renders', action='store_true')
    parser.add_argument('--render-samples', type=int, default=32)
    return parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:])


ARGS = arguments()
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import design
import structures
RECIPE = design.load(ARGS.recipe)

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

if bpy.app.version[:2] != (5, 2):
    raise RuntimeError('This generator is pinned to Blender 5.2.x')

OUT = ARGS.output.resolve()
OUT.parent.mkdir(parents=True, exist_ok=True)
WORK = Path(tempfile.mkdtemp(prefix='.' + RECIPE['id'] + '-working-', dir=OUT.parent))
(WORK / 'textures').mkdir()
(WORK / 'review').mkdir()
MATERIALS = {}
MAT_RECORDS = []
SOURCES = [dict(name='Original Bwork bridge-family geometry', license='original', url='')]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def unity_to_blender(point):
    return (point[0], -point[2], point[1])


def blender_to_unity(point):
    return (point[0], point[2], -point[1])


def write_image(path, values, color=False):
    values = np.asarray(values, dtype=np.float32)
    if values.ndim == 2:
        values = np.repeat(values[:, :, None], 3, axis=2)
    if values.shape[2] == 3:
        values = np.concatenate((values, np.ones((*values.shape[:2], 1), np.float32)), axis=2)
    # Preserve byte-image values in their stored color space, matching the existing Bwork prep.
    pixels = np.flipud(np.uint8(np.rint(np.clip(values, 0, 1) * 255)))
    h, w = values.shape[:2]
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(b''.join(b'\0' + row.tobytes() for row in pixels), 9)) + chunk(b'IEND', b''))


def read_image(path, size=1024, color=False):
    image = bpy.data.images.load(str(path), check_existing=False)
    image.colorspace_settings.name = 'Non-Color'
    image.alpha_mode = 'CHANNEL_PACKED'
    width, height = (size, size) if isinstance(size, int) else size
    image.scale(width, height)
    data = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(data)
    bpy.data.images.remove(image)
    return data.reshape(height, width, 4)


def filtered_noise(rng, size, scale):
    white = np.fft.fft2(rng.standard_normal((size, size)))
    f = np.fft.fftfreq(size)
    frequency = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    noise = np.fft.ifft2(white * np.exp(-(frequency * scale) ** 2)).real
    return noise / max(noise.std(), 1e-6)


def prepare_textures():
    n = 1024
    rng = np.random.default_rng(915180)
    broad = filtered_noise(rng, n, 100)
    medium = filtered_noise(rng, n, 15)
    fine = filtered_noise(rng, n, 2)
    yy, xx = np.mgrid[:n, :n] / n
    # Faint 0.4m cast-board seams and tiny pores live at concrete scale, not as macro grime.
    seam = np.exp(-((np.sin(yy * math.pi * 5) / .018) ** 2))
    pores = np.maximum(-fine - 1.9, 0)
    color = np.stack([.49 + broad * .018 + medium * .007 - pores * .012 - seam * .018,
                      .475 + broad * .017 + medium * .007 - pores * .012 - seam * .017,
                      .44 + broad * .016 + medium * .006 - pores * .011 - seam * .016], axis=-1)
    relief = medium * .00025 + fine * .00012 - pores * .00055 - seam * .00055
    dx = (np.roll(relief, -1, 1) - np.roll(relief, 1, 1)) * n / 4
    dy = (np.roll(relief, -1, 0) - np.roll(relief, 1, 0)) * n / 4
    normals = np.stack((-dx, -dy, np.ones_like(dx)), axis=-1)
    normals /= np.linalg.norm(normals, axis=-1, keepdims=True)
    normal = normals * .5 + .5
    mask = np.ones((n, n, 4), dtype=np.float32)
    mask[:, :, 0] = 0
    mask[:, :, 1] = np.clip(1 - pores * .025 - seam * .04, .85, 1)
    mask[:, :, 2] = 0
    mask[:, :, 3] = np.clip(.18 + broad * .018 + medium * .01, .1, .28)
    tile = 2.0
    if ARGS.concrete_source:
        packet = json.loads(ARGS.concrete_source.read_text())
        if packet['source']['license'] != 'CC0-1.0':
            raise ValueError('The optional concrete source must carry the reviewed CC0-1.0 receipt')
        resolve = lambda p: (ARGS.concrete_source.parent / p).resolve()
        repeats = int(packet.get('squareTileRepeatY', 1))
        def source_pixels(path):
            pixels = read_image(resolve(path), (n, n // repeats))
            return np.tile(pixels, (repeats, 1, 1))
        color = source_pixels(packet['baseColorPath'])[:, :, :3]
        normal = source_pixels(packet['normalPath'])[:, :, :3]
        mask[:, :, 1] = 1
        if packet.get('roughnessPath'):
            mask[:, :, 3] = 1 - source_pixels(packet['roughnessPath'])[:, :, 0]
        if packet.get('aoPath'):
            mask[:, :, 1] = source_pixels(packet['aoPath'])[:, :, 0]
        tile = float(packet.get('tilingMeters', 2))
        SOURCES.append(packet['source'])
    else:
        SOURCES.append(dict(name='Original Bwork cast-concrete pore/formwork material', license='original', url=''))
    write_image(WORK / 'textures/concrete-basecolor.png', color, True)
    write_image(WORK / 'textures/concrete-normal.png', normal)
    write_image(WORK / 'textures/concrete-mask.png', mask)
    # Reuse the reviewed, exact CC0 asphalt inputs rather than synthesize a different road surface.
    root = HERE.parents[2]
    asset_root = root / 'art/CoastalBridgeTool'
    if not asset_root.is_dir():
        asset_root = HERE.parents[1] / 'assets/CoastalBridgeTool'
    asphalt = asset_root / 'inputs/asphalt'
    for role, filename, color_space in [('basecolor', 'Asphalt_BaseMap.jpg', True),
                                      ('normal', 'Asphalt_Normal.jpg', False),
                                      ('mask', 'Asphalt_MetallicSmoothness.png', False)]:
        write_image(WORK / ('textures/asphalt-' + role + '.png'), read_image(asphalt / filename, n, color_space), color_space)
    SOURCES.append(dict(name='Poly Haven clean_asphalt, reused reviewed RoadQuality maps',
                        license='CC0-1.0', url='https://polyhaven.com/a/clean_asphalt'))
    if RECIPE['designType'] != 'coastalArch':
        # Original physical defaults: coursed stone and longitudinal timber grain.
        # They are independent material assets; mesh generation reads no texture parameters.
        yy, xx = np.mgrid[0:n, 0:n] / n
        for key in ('stone', 'timber'):
            if key == 'stone':
                row = np.floor(yy * 5)
                mortar = np.minimum(np.mod(yy * 5, 1), 1 - np.mod(yy * 5, 1)) < .028
                brick_x = np.mod(xx * 3 + np.mod(row, 2) * .5, 1)
                mortar |= np.minimum(brick_x, 1 - brick_x) < .018
                variation = .035 * np.sin(np.floor(xx * 3 + np.mod(row, 2) * .5) * 8.71 + row * 9.12)
                shade = broad * .025 + medium * .018 + fine * .008 + variation - mortar * .12
                color = np.stack((.43 + shade, .405 + shade, .35 + shade), axis=-1)
                height = medium * .0006 + fine * .0002 - mortar * .009
            else:
                grain = np.sin(xx * math.pi * 115 + np.sin(yy * 11) * 1.5 + medium * .14)
                shade = broad * .022 + grain * .025 + fine * .007
                color = np.stack((.265 + shade, .19 + shade * .78, .12 + shade * .55), axis=-1)
                height = grain * .0006 + fine * .00015
            dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * n / 4
            dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * n / 4
            norm = np.stack((-dx, -dy, np.ones_like(dx)), axis=-1)
            norm /= np.linalg.norm(norm, axis=-1, keepdims=True)
            mask = np.ones((n, n, 4), dtype=np.float32)
            mask[:, :, 0] = 0; mask[:, :, 2] = 0; mask[:, :, 3] = .12 if key == 'stone' else .21
            write_image(WORK / ('textures/' + key + '-basecolor.png'), color, True)
            write_image(WORK / ('textures/' + key + '-normal.png'), norm * .5 + .5)
            write_image(WORK / ('textures/' + key + '-mask.png'), mask)
        SOURCES.append(dict(name='Original Bwork coursed stone and sawn timber material maps', license='original', url=''))
    return tile


def material(name, key, texture_key=None, tile=1, color=(1, 1, 1, 1), metallic=0, smoothness=.2):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = 1 - smoothness
    record = dict(key=key, tilingMeters=tile, baseColor=list(color),
                  metallic=metallic, smoothness=smoothness)
    if texture_key:
        texcoord = nodes.new('ShaderNodeTexCoord')
        mapping = nodes.new('ShaderNodeVectorMath'); mapping.operation = 'SCALE'
        mapping.inputs[3].default_value = 1 / tile
        links.new(texcoord.outputs['UV'], mapping.inputs[0])
        for role, suffix in [('baseColorPath', 'basecolor'), ('normalPath', 'normal'), ('maskPath', 'mask')]:
            relative = 'textures/' + texture_key + '-' + suffix + '.png'
            texture = nodes.new('ShaderNodeTexImage')
            texture.image = bpy.data.images.load(str(WORK / relative), check_existing=True)
            texture.image.colorspace_settings.name = 'sRGB' if suffix == 'basecolor' else 'Non-Color'
            texture.image.alpha_mode = 'CHANNEL_PACKED'
            links.new(mapping.outputs['Vector'], texture.inputs['Vector'])
            if suffix == 'basecolor':
                tint = nodes.new('ShaderNodeMixRGB'); tint.blend_type = 'MULTIPLY'
                tint.inputs[0].default_value = 1; tint.inputs[2].default_value = color
                links.new(texture.outputs['Color'], tint.inputs[1]); links.new(tint.outputs[0], bsdf.inputs['Base Color'])
            elif suffix == 'normal':
                bump = nodes.new('ShaderNodeNormalMap')
                links.new(texture.outputs['Color'], bump.inputs['Color']); links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
            else:
                roughness = nodes.new('ShaderNodeMath'); roughness.operation = 'SUBTRACT'; roughness.inputs[0].default_value = 1
                links.new(texture.outputs['Alpha'], roughness.inputs[1]); links.new(roughness.outputs[0], bsdf.inputs['Roughness'])
            record[role] = relative
    MATERIALS[name] = mat
    MAT_RECORDS.append(record)
    return mat


class Geometry:
    def __init__(self):
        self.vertices = []; self.faces = []; self.materials = []; self.face_materials = []

    def polyhedron(self, vertices, faces, mat='BridgeConcrete'):
        offset = len(self.vertices)
        if mat not in self.materials:
            self.materials.append(mat)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(offset + i for i in face) for face in faces)
        self.face_materials.extend([self.materials.index(mat)] * len(faces))

    def box(self, x0, x1, y0, y1, z0, z1, mat='BridgeConcrete'):
        if min(x1 - x0, y1 - y0, z1 - z0) <= 1e-7:
            return
        self.polyhedron([(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)],
                        [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)], mat)

    def tapered_column(self, x, z, bottom, top, bottom_width, top_width, bottom_depth, top_depth):
        self.polyhedron([(x + sx * w / 2, y, z + sz * d / 2)
                         for y, w, d in ((bottom, bottom_width, bottom_depth), (top, top_width, top_depth))
                         for sz, sx in ((-1, -1), (-1, 1), (1, 1), (1, -1))],
                        [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)])

    def beam(self, a, b, width, depth, mat='BridgeConcrete'):
        a, b = Vector(a), Vector(b)
        tangent = (b - a).normalized()
        ref = Vector((1, 0, 0)) if abs(tangent.x) < .8 else Vector((0, 0, 1))
        u = (ref - tangent * tangent.dot(ref)).normalized() * width / 2
        v = tangent.cross(u).normalized() * depth / 2
        self.polyhedron([tuple(p + sx * u + sy * v) for p in (a, b)
                         for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))],
                        [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], mat)

    def object(self, name, collection, bevel=0):
        if not self.vertices:
            return None
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([unity_to_blender(v) for v in self.vertices], [], self.faces)
        for mat in self.materials:
            mesh.materials.append(MATERIALS[mat])
        for face, index in zip(mesh.polygons, self.face_materials):
            face.material_index = index
        mesh.update()
        bm = bmesh.new(); bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh); bm.free(); mesh.update()
        uv = mesh.uv_layers.new(name='UVMap')
        for face in mesh.polygons:
            normal = blender_to_unity(face.normal)
            # Metric UV0: V is vertical on walls; top surfaces use longitudinal distance.
            axes = (0, 2) if abs(normal[1]) > .7 else ((2, 1) if abs(normal[0]) > abs(normal[2]) else (0, 1))
            for li in face.loop_indices:
                point = blender_to_unity(mesh.vertices[mesh.loops[li].vertex_index].co)
                uv.data[li].uv = (point[axes[0]], point[axes[1]])
            face.use_smooth = True
        obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj)
        if bevel:
            modifier = obj.modifiers.new('Cast edge radius', 'BEVEL')
            modifier.width = bevel; modifier.segments = 2; modifier.limit_method = 'ANGLE'
            modifier.angle_limit = .45; modifier.use_clamp_overlap = True
        modifier = obj.modifiers.new('Concrete face normals', 'WEIGHTED_NORMAL')
        modifier.keep_sharp = True; modifier.weight = 50
        return obj


def build_lod(lod):
    if RECIPE['designType'] != 'coastalArch':
        return structures.build_lod(RECIPE, lod, Geometry, bpy)
    r = RECIPE; length = r['totalLength']; half = r['archSpan'] / 2; width = r['deckWidth']
    deck = Geometry(); arches = Geometry(); supports = Geometry(); rails = Geometry(); details = Geometry(); markings = Geometry()
    collection = bpy.data.collections.new('Editable_LOD' + str(lod)); bpy.context.scene.collection.children.link(collection)
    deck.box(-width / 2, width / 2, -r['deckThickness'], -.045, -length / 2, length / 2)
    deck.box(-width / 2 + .5, width / 2 - .5, -.045, 0, -length / 2, length / 2, 'BridgeAsphalt')
    # Narrow physical paint strips retain their width at every LOD. Stop at expansion joints.
    joints = [-length / 2 + .35, -half, half, length / 2 - .35]
    paint_intervals = list(zip([-length / 2] + [z + .06 for z in joints],
                               [z - .06 for z in joints] + [length / 2]))
    for z0, z1 in paint_intervals:
        for side in (-1, 1):
            markings.box(side * .09 - .045, side * .09 + .045, .001, .004, z0, z1, 'BridgeRoadPaintAmber')
            edge = side * (width / 2 - .7)
            markings.box(edge - .05, edge + .05, .001, .004, z0, z1, 'BridgeRoadPaintWhite')
    if lod == 0:
        for z in np.arange(-length / 2 + 6, length / 2, 12):
            details.box(-.045, .045, .004, .020, z - .05, z + .05, 'BridgeMetal')
            details.box(-.034, .034, .020, .025, z - .032, z + .032, 'BridgeRoadPaintAmber')
    rib_x = width * .36
    # Twin continuous arch ribs: thicker springings, thin crown, closed underside.
    segments = (128, 72, 36)[lod]
    for sign in (-1, 1):
        x0 = sign * rib_x - r['archRibWidth'] / 2; x1 = x0 + r['archRibWidth']
        points = np.linspace(-half, half, segments + 1)
        verts = []
        for z in points:
            top = design.arch_top(r, float(z)); bottom = top - design.arch_depth(r, float(z))
            verts.extend([(x0, bottom, float(z)), (x1, bottom, float(z)), (x1, top, float(z)), (x0, top, float(z))])
        faces = [(3, 2, 1, 0)]
        for i in range(segments):
            a = i * 4; b = a + 4
            faces.extend([(a + j, a + (j + 1) % 4, b + (j + 1) % 4, b + j) for j in range(4)])
        faces.append(tuple(segments * 4 + j for j in range(4)))
        arches.polyhedron(verts, faces)
    stations = design.spandrel_stations(r)
    for index, z in enumerate(stations):
        bottom = design.arch_top(r, z) - .05; top = -r['deckThickness']
        h = top - bottom; bw = .55 + min(h / 35, 1) * .36; tw = .46 + min(h / 35, 1) * .18
        for side in (-1, 1):
            supports.tapered_column(side * rib_x, z, bottom, top, bw, tw, bw * 1.05, tw)
            if lod < 2 and h > 2:
                supports.box(side * rib_x - tw / 2 - .16, side * rib_x + tw / 2 + .16,
                             top - .38, top, z - .43, z + .43)
        supports.box(-rib_x - .25, rib_x + .25, top - .65, top - .12, z - .30, z + .30)
        if h > 5:
            # Cross-members bind the ribs near their top, retaining a readable open arch.
            arches.box(-rib_x, rib_x, bottom - r['archRibDepth'] * .45 - .24,
                       bottom - r['archRibDepth'] * .45 + .24, z - .28, z + .28)
    for side in (-1, 1):
        # Main springing towers establish the hierarchy between slender spandrels and foundations.
        z = side * half; footing_y = design.arch_top(r, z) - design.arch_depth(r, z) - 2.2
        for x in (-rib_x, rib_x):
            supports.tapered_column(x, z, footing_y + 1.25, -r['deckThickness'], 1.8, 1.0, 2.7, 1.25)
            supports.box(x - 1.45, x + 1.45, footing_y, footing_y + 1.25, z - 2.1, z + 2.1)
        supports.box(-rib_x - .5, rib_x + .5, -r['deckThickness'] - 1.2, -r['deckThickness'], z - .9, z + .9)
        approach_mid = side * ((half + length / 2 - r['abutmentLength']) / 2)
        approach_base = -(r['archRise'] * .44 + 4.0)
        for x in (-rib_x, rib_x):
            supports.tapered_column(x, approach_mid, approach_base, -r['deckThickness'], 1.25, .8, 1.65, 1.0)
            supports.box(x - 1.05, x + 1.05, approach_base - .85, approach_base, approach_mid - 1.3, approach_mid + 1.3)
        supports.box(-rib_x - .3, rib_x + .3, -r['deckThickness'] - .9, -r['deckThickness'], approach_mid - .65, approach_mid + .65)
        # Abutment stem and outward-splayed wingwalls finish below the adjoining rock berm.
        end = side * length / 2; inner = side * (length / 2 - r['abutmentLength'])
        supports.box(-width / 2 - .25, width / 2 + .25, -6.2, -r['deckThickness'], min(end, inner), max(end, inner))
        for xside in (-1, 1):
            a = (xside * (width / 2 + .08), -2.3, inner)
            b = (xside * (width / 2 + 2.3), -2.3, end)
            supports.beam(a, b, .5, 4.6)
    # Longitudinal edge beams, cast curbs, open parapets and coping carry the riding view.
    for side in (-1, 1):
        x = side * (width / 2 - .18)
        deck.box(x - .24, x + .24, -r['deckThickness'] - .38, -.02, -length / 2, length / 2)
        rails.box(side * width / 2 - .5 if side > 0 else -width / 2,
                  width / 2 if side > 0 else -width / 2 + .5, -.02, .18, -length / 2, length / 2)
        rails.box(x - .15, x + .15, .18, .36, -length / 2, length / 2)
        rails.box(x - .22, x + .22, r['parapetHeight'] - .14, r['parapetHeight'], -length / 2, length / 2)
        bays = max(4, round(length / 2.5)); spacing = length / bays
        for i in range(bays + 1):
            z = -length / 2 + i * spacing
            post = .32 if i % 4 == 0 else .18
            z0, z1 = max(z - post / 2, -length / 2), min(z + post / 2, length / 2)
            rails.box(x - .16, x + .16, .34, r['parapetHeight'] - .12, z0, z1)
            if lod < 2 and i % 4 == 0:
                rails.box(x - .245, x + .245, r['parapetHeight'], r['parapetHeight'] + .045,
                          max(z - .23, -length / 2), min(z + .23, length / 2))
            if lod == 0 and i % 4 == 2:
                # Scuppers sit in the curb, with a short downpipe outside the deck.
                details.box(x - .12, x + .12, .178, .19, z - .21, z + .21, 'BridgeMetal')
                for k in range(4):
                    details.box(x - .11, x + .11, .19, .207, z - .18 + k * .11, z - .165 + k * .11)
                outer = side * (width / 2 + .04)
                details.beam((outer, -.15, z), (outer, -1.6, z), .085, .085, 'BridgeMetal')
                # Restrained runoff belongs below each scupper on the exterior edge beam.
                stain_x = side * (width / 2 + .063)
                details.box(stain_x - .003, stain_x + .003, -r['deckThickness'] - .32, -.08,
                            z - .085, z + .085, 'BridgeConcreteWeathered')
            if lod < 2 and i % 2 == 1:
                # Small coping joints are geometry visible only in the close riding corridor.
                details.box(x - .218, x + .218, r['parapetHeight'] - .008, r['parapetHeight'] + .003,
                            z - .007, z + .007, 'BridgeMetal')
    if lod < 2:
        for z in (-half, half, -length / 2 + .35, length / 2 - .35):
            details.box(-width / 2 + .5, width / 2 - .5, .001, .008, z - .022, z + .022, 'BridgeMetal')
            if lod == 0:
                for direction in (-1, 1):
                    details.box(-width / 2 + .5, width / 2 - .5, .008, .016,
                                z + direction * .047 - .006, z + direction * .047 + .006, 'BridgeMetal')
    objects = []
    for label, geometry, radius in [('Deck', deck, .022), ('Arches', arches, .035),
                                    ('Supports', supports, .028), ('Parapets', rails, .014), ('Details', details, 0),
                                    ('RoadPaint', markings, 0)]:
        obj = geometry.object('Bridge' + label + '_LOD' + str(lod), collection, radius if lod == 0 else 0)
        if obj:
            objects.append(obj)
    return collection, objects


def triangle_copy(source, reflect_x=False, flat=False):
    # BMesh topology edits change the encoding space of custom split normals.
    # Copy explicit corner vectors and UVs into the final triangle topology instead.
    source.calc_loop_triangles()
    triangles = [tri for tri in source.loop_triangles if tri.area > 1e-10]
    removed = len(source.loop_triangles) - len(triangles)
    order = (2, 1, 0) if reflect_x else (0, 1, 2)
    mesh = bpy.data.meshes.new(source.name + '_triangles')
    mesh.from_pydata([(-v.co.x if reflect_x else v.co.x, v.co.y, v.co.z) for v in source.vertices], [],
                     [tuple(tri.vertices[i] for i in order) for tri in triangles])
    for mat in source.materials:
        mesh.materials.append(mat)
    normals = []
    for face, tri in zip(mesh.polygons, triangles):
        original = source.polygons[tri.polygon_index]
        face.material_index = original.material_index
        flat_surface = flat or source.materials[original.material_index].name.startswith('BridgeRoadPaint')
        face.use_smooth = not flat_surface
        for i in order:
            n = original.normal if flat_surface else source.corner_normals[tri.loops[i]].vector
            normals.append((-n.x if reflect_x else n.x, n.y, n.z))
    for original_uv in source.uv_layers:
        uv = mesh.uv_layers.new(name=original_uv.name)
        for face, tri in zip(mesh.polygons, triangles):
            for destination, i in zip(face.loop_indices, order):
                uv.data[destination].uv = original_uv.data[tri.loops[i]].uv
    mesh.update()
    mesh.normals_split_custom_set(normals)
    return mesh, removed


def evaluated_copy(source, export_collection):
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = bpy.data.meshes.new_from_object(source.evaluated_get(graph), preserve_all_data_layers=True, depsgraph=graph)
    mesh, removed = triangle_copy(evaluated, flat=source.name.startswith('BridgeRoadPaint'))
    bpy.data.meshes.remove(evaluated)
    obj = bpy.data.objects.new(source.name, mesh); export_collection.objects.link(obj)
    return obj, removed


def bounds_and_count(objects):
    points = []; triangles = 0; vertices = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles); vertices += len(obj.data.vertices)
        points.extend(blender_to_unity(obj.matrix_world @ v.co) for v in obj.data.vertices)
    values = np.asarray(points)
    return dict(triangles=triangles, vertices=vertices,
                boundsMin=dict(zip(('x', 'y', 'z'), map(float, values.min(axis=0)))),
                boundsMax=dict(zip(('x', 'y', 'z'), map(float, values.max(axis=0)))))


def export_fbx(objects, filename):
    # Existing Bwork importer maps FBX points to (-x,z,-y); reflect the export copy only.
    for obj in objects:
        previous = obj.data
        obj.data = triangle_copy(previous, reflect_x=True, flat=obj.name.startswith('BridgeRoadPaint'))[0]
        bpy.data.meshes.remove(previous)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    result = bpy.ops.export_scene.fbx(filepath=str(WORK / filename), use_selection=True, object_types={'MESH'},
        global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', use_space_transform=True,
        bake_space_transform=True, axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
        mesh_smooth_type='OFF', use_tspace=False, use_custom_props=False, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP', embed_textures=False, use_metadata=False)
    if result != {'FINISHED'}:
        raise RuntimeError('FBX export did not finish: ' + repr(result))
    return sha(WORK / filename)


def review_camera(name, position, target, ortho=None):
    data = bpy.data.cameras.new(name); obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = unity_to_blender(position)
    obj.rotation_euler = (Vector(unity_to_blender(target)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    data.lens = 44; data.clip_end = 1200
    if ortho:
        data.type = 'ORTHO'; data.ortho_scale = ortho
    bpy.context.scene.camera = obj
    return obj


def review_scene():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'
    scene.cycles.samples = ARGS.render_samples; scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600; scene.render.resolution_y = 1000; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'; scene.view_settings.view_transform = 'AgX'
    scene.world = bpy.data.worlds.new('Bridge review soft blue daylight'); scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.54, .65, .8, 1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
    light = bpy.data.lights.new('Side light', 'SUN'); light.energy = 3; light.angle = .08
    sun = bpy.data.objects.new(light.name, light); scene.collection.objects.link(sun)
    sun.rotation_euler = (.63, -.45, -.68)
    length = RECIPE['totalLength']; rise = RECIPE['archRise']
    review_camera('ThreeQuarter', (length * .67, rise * .35, -length * .63), (0, -rise * .42, 0), length * 1.10)
    return scene


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene; scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
    tile = prepare_textures()
    material('BridgeConcrete', 'concrete', 'concrete', tile)
    material('BridgeAsphalt', 'asphalt', 'asphalt', 2.1)
    material('BridgeMetal', 'metal', color=(.055, .061, .063, 1), metallic=.7, smoothness=.38)
    material('BridgeConcreteWeathered', 'concreteWeathered', 'concrete', tile, color=(.78, .775, .74, 1))
    material('BridgeRoadPaintAmber', 'roadPaintAmber', color=(.9, .48, .055, 1), smoothness=.22)
    material('BridgeRoadPaintWhite', 'roadPaintWhite', color=(.72, .74, .70, 1), smoothness=.18)
    if RECIPE['designType'] != 'coastalArch':
        material('BridgeStone', 'stone', 'stone', 2)
        material('BridgeTimber', 'timber', 'timber', 2)
    exports_collection = bpy.data.collections.new('EXPORT_ONLY'); scene.collection.children.link(exports_collection)
    lods = []; bounds = None; previous = None
    for index, height in enumerate((.35, .12, .015)):
        collection, sources = build_lod(index); bpy.context.view_layer.update()
        outputs = [evaluated_copy(obj, exports_collection) for obj in sources]
        objects = [pair[0] for pair in outputs]
        census = bounds_and_count(objects)
        if previous is not None and census['triangles'] >= previous:
            raise ValueError('LOD complexity must descend')
        previous = census['triangles']
        if index == 0:
            bounds = census
        record = dict(level=index, fbxRelativePath='bridge-lod' + str(index) + '.fbx', screenRelativeHeight=height,
                      triangles=census['triangles'], vertices=census['vertices'], boundsMin=census['boundsMin'], boundsMax=census['boundsMax'],
                      removedDegenerates=sum(pair[1] for pair in outputs),
                      parts=[dict(name=obj.name, triangles=len(obj.data.loop_triangles),
                                  materialSlots=[mat.name for mat in obj.data.materials]) for obj in objects])
        record['sha256'] = export_fbx(objects, record['fbxRelativePath'])
        lods.append(record)
        for obj in objects:
            mesh = obj.data; bpy.data.objects.remove(obj, do_unlink=True); bpy.data.meshes.remove(mesh)
        collection.hide_render = index != 0; collection.hide_viewport = index != 0
        print('BWORK_COASTAL_BRIDGE_LOD ' + json.dumps(record), flush=True)
    collider = Geometry()
    collider.box(-RECIPE['deckWidth'] / 2 + .5, RECIPE['deckWidth'] / 2 - .5, -.2, 0,
                 -RECIPE['totalLength'] / 2, RECIPE['totalLength'] / 2)
    proxy = collider.object('BridgeDeckCollider', exports_collection)
    proxies = [evaluated_copy(proxy, exports_collection)[0]]
    collision_hash = export_fbx(proxies, 'bridge-collider.fbx')
    for obj in [proxy] + proxies:
        mesh = obj.data; bpy.data.objects.remove(obj, do_unlink=True); bpy.data.meshes.remove(mesh)
    bpy.data.collections.remove(exports_collection)
    scene = review_scene()
    for image in bpy.data.images:
        if image.source == 'FILE':
            image.pack()
            image.filepath = '//textures/' + Path(image.filepath).name
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'bridge-source.blend'), compress=True)
    source_bytes = Path(__file__).read_bytes() + (HERE / 'design.py').read_bytes() + (HERE / 'structures.py').read_bytes()
    textures = [dict(path=str(path.relative_to(WORK)), sha256=sha(path), bytes=path.stat().st_size)
                for path in sorted((WORK / 'textures').glob('*.png'))]
    source_bytes += json.dumps(textures, sort_keys=True).encode()
    manifest = dict(schemaVersion=2, designType=RECIPE['designType'], id=RECIPE['id'], recipeHash=design.recipe_hash(RECIPE, source_bytes),
        generatorVersion=design.VERSION, units='meters', coordinateSystem='unity-y-up-z-forward', deckDatum=0,
        totalLength=RECIPE['totalLength'], deckWidth=RECIPE['deckWidth'], archSpan=RECIPE['archSpan'], archRise=RECIPE['archRise'],
        boundsMin=bounds['boundsMin'], boundsMax=bounds['boundsMax'], lods=lods, colliderRelativePath='bridge-collider.fbx',
        colliderSha256=collision_hash,
        materialSlots=[dict(slotName=name, materialKey=key) for name, key in [('BridgeConcrete', 'concrete'),
                      ('BridgeAsphalt', 'asphalt'), ('BridgeMetal', 'metal'), ('BridgeConcreteWeathered', 'concreteWeathered'),
                      ('BridgeRoadPaintAmber', 'roadPaintAmber'), ('BridgeRoadPaintWhite', 'roadPaintWhite'),
                      ('BridgeStone', 'stone'), ('BridgeTimber', 'timber')] if name in MATERIALS],
        materials=MAT_RECORDS, sources=SOURCES, textures=textures,
        sockets=[dict(name='start', position=dict(x=0, y=0, z=-RECIPE['totalLength'] / 2)),
                 dict(name='end', position=dict(x=0, y=0, z=RECIPE['totalLength'] / 2))],
        clearCarriageway=RECIPE['deckWidth'] - 1, sourceBlendRelativePath='bridge-source.blend',
        sourceBlendSha256=sha(WORK / 'bridge-source.blend'), blenderVersion=bpy.app.version_string,
        design=RECIPE, reviewStatus='Blender asset candidate; actual Unity import and scene review are required')
    if not ARGS.skip_renders:
        scene.render.filepath = str(WORK / 'review/three-quarter.png'); bpy.ops.render.render(write_still=True)
        review_camera('Side elevation', (RECIPE['totalLength'] * .8, -RECIPE['archRise'] * .25, 0),
                      (0, -RECIPE['archRise'] * .35, 0), RECIPE['totalLength'] * 1.1)
        scene.render.filepath = str(WORK / 'review/side-elevation.png'); bpy.ops.render.render(write_still=True)
        review_camera('Riding approach', (0, 1.65, -RECIPE['totalLength'] / 2 - 4), (0, 1.25, 0))
        scene.render.filepath = str(WORK / 'review/riding.png'); bpy.ops.render.render(write_still=True)
    (WORK / 'asset-report.json').write_text(json.dumps(manifest, indent=2, allow_nan=False) + '\n')
    contract_fields = ('schemaVersion', 'designType', 'id', 'recipeHash', 'generatorVersion', 'units', 'coordinateSystem',
                       'deckDatum', 'totalLength', 'deckWidth', 'clearCarriageway', 'archSpan', 'archRise', 'boundsMin', 'boundsMax',
                       'colliderRelativePath', 'materialSlots', 'materials', 'sources')
    published = {key: manifest[key] for key in contract_fields}
    published['lods'] = [{key: lod[key] for key in ('level', 'fbxRelativePath', 'screenRelativeHeight', 'triangles')}
                         for lod in manifest['lods']]
    (WORK / 'manifest.json').write_text(json.dumps(published, indent=2, allow_nan=False) + '\n')
    if OUT.exists():
        backup = OUT.parent / ('.' + OUT.name + '-previous-' + manifest['recipeHash'][:8])
        if backup.exists():
            shutil.rmtree(backup)
        OUT.rename(backup)
    WORK.rename(OUT)
    print('BWORK_COASTAL_BRIDGE_FINISHED ' + json.dumps(dict(id=RECIPE['id'], output=str(OUT),
          recipeHash=manifest['recipeHash'], triangles=[lod['triangles'] for lod in lods])), flush=True)


if __name__ == '__main__':
    main()
