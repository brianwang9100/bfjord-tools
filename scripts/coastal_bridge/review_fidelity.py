# SPDX-License-Identifier: GPL-3.0-or-later
"""Render the source bridge's silhouette and construction detail without editing it.

Blender --background --threads 4 --python review_fidelity.py -- --package PACKAGE --output OUTPUT
The saved source already contains packed textures and the review lighting.
"""
import argparse
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
record = json.loads((args.package / 'asset-report.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str((args.package / 'bridge-source.blend').resolve()))
scene = bpy.context.scene
scene.cycles.samples = 24
scene.render.threads_mode = 'FIXED'
scene.render.threads = 4
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
args.output.mkdir(parents=True, exist_ok=True)

def camera(name, position, target, ortho=None):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    convert = lambda p: Vector((p[0], -p[2], p[1]))
    obj.location = convert(position)
    obj.rotation_euler = (convert(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    data.lens = 48
    data.clip_end = 1200
    if ortho:
        data.type = 'ORTHO'; data.ortho_scale = ortho
    scene.camera = obj
    scene.render.filepath = str((args.output / (name + '.png')).resolve())
    bpy.ops.render.render(write_still=True)

# Check the corrected UV direction on editable long timber faces before rendering.
# V must track the physical long edge, rather than world up. End faces under 1 m are excluded.
grain_faces = 0
if record['designType'] == 'timberTrestle':
    for obj in scene.objects:
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        adjacency = {v.index: [] for v in mesh.vertices}
        for edge in mesh.edges:
            a, b = edge.vertices
            adjacency[a].append(b); adjacency[b].append(a)
        component_length = {}
        for start in adjacency:
            if start in component_length:
                continue
            pending = [start]; connected = {start}; member_length = 0
            while pending:
                a = pending.pop()
                for b in adjacency[a]:
                    member_length = max(member_length, (mesh.vertices[a].co - mesh.vertices[b].co).length)
                    if b not in connected:
                        connected.add(b); pending.append(b)
            for vertex in connected:
                component_length[vertex] = member_length
        for face in mesh.polygons:
            if mesh.materials[face.material_index].name != 'BridgeTimber':
                continue
            edges = []
            loops = list(face.loop_indices)
            for index, first in enumerate(loops):
                second = loops[(index + 1) % len(loops)]
                delta = mesh.vertices[mesh.loops[second].vertex_index].co - mesh.vertices[mesh.loops[first].vertex_index].co
                edges.append((delta.length, abs(mesh.uv_layers.active.data[second].uv.y - mesh.uv_layers.active.data[first].uv.y)))
            longest, v_length = max(edges)
            if longest >= 1 and abs(longest - component_length[face.vertices[0]]) < .001:
                assert abs(v_length / longest - 1) < .002, 'Longitudinal timber UV: ' + obj.name
                grain_faces += 1
    assert grain_faces > 0

length = record['totalLength']
width = record['deckWidth']
kind = record['designType']
height = record['design'].get('pierHeight', record['archRise'])
camera('silhouette', (length * .6, height * .45, -length * .55), (0, -height * .25, 0), length * 1.05)
if kind == 'stoneViaduct':
    camera('construction', (width + 9, 3, -length * .26), (width / 2, -3.2, -length * .15))
elif kind == 'steelThroughTruss':
    camera('construction', (width * .45, 2.4, -length * .40), (width * .5, 3.0, -length * .25))
elif kind == 'timberTrestle':
    camera('construction', (width + 5, -1, -length * .18), (0, -height * .48, -length * .09))
else:
    camera('construction', (width + 4, 3, -length * .36), (width * .45, -.2, -length * .3))
camera('riding', (width * .15, 1.65, -length * .27), (width * .23, 1.1, -length * .15))
(args.output / 'review.json').write_text(json.dumps({
    'id': record['id'], 'recipeHash': record['recipeHash'], 'renderer': 'Blender CPU Cycles, 24 samples, four threads',
    'sourceBlendSha256': record['sourceBlendSha256'], 'longitudinalTimberFacesChecked': grain_faces,
    'limits': 'Source geometry/material art review. Unity import, scene contact, LOD transitions and device performance are separate.'
}, indent=2) + '\n')
