"""Reimport published FBXs and check bridge export facts without touching Unity."""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys
import bpy
import bmesh

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--manifest', type=Path, required=True)
parser.add_argument('--report', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.manifest.resolve().parent
published = json.loads(args.manifest.read_text())
manifest = json.loads((root / 'asset-report.json').read_text())
assert published['recipeHash'] == manifest['recipeHash']


def source(relative, expected=None):
    path = (root / relative).resolve()
    assert path.is_relative_to(root) and path.is_file(), relative
    if expected:
        assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, 'Hash: ' + relative
    return path


def inspect(relative, collider=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(source(relative)), use_anim=False)
    assert result == {'FINISHED'}
    objects = list(bpy.context.scene.objects)
    assert objects and all(obj.type == 'MESH' for obj in objects), 'Meshes only'
    points = []
    count = 0
    parts = []
    top_faces = 0
    surface_normals = {}
    allowed = {record['slotName'] for record in manifest['materialSlots']}
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        count += len(mesh.loop_triangles)
        assert all(abs(value - 1) < 1e-5 for value in obj.scale), 'Unit scale'
        assert mesh.uv_layers and len(mesh.uv_layers.active.data) == len(mesh.loops), 'UV0'
        assert all(math.isfinite(value) for datum in mesh.uv_layers.active.data for value in datum.uv), 'Finite UV0'
        assert all(math.isfinite(value) for normal in mesh.corner_normals for value in normal.vector), 'Finite normals'
        assert all(material.name in allowed for material in mesh.materials), 'Material slots'
        vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
        # FBX roundtrip retains the intentional X reflection used by Unity's handedness conversion.
        points.extend((-point.x, point.z, -point.y) for point in vertices)
        assert all(face.area > 1e-10 for face in mesh.polygons), 'Nondegenerate faces'
        bm = bmesh.new(); bm.from_mesh(mesh)
        volume = bm.calc_volume(signed=True)
        boundary_edges = sum(not edge.is_manifold for edge in bm.edges)
        bm.free()
        assert volume > 0, 'Outward winding: ' + obj.name
        assert boundary_edges == 0, 'Closed export geometry: ' + obj.name
        for face in mesh.polygons:
            material = mesh.materials[face.material_index].name
            if material in ('BridgeAsphalt', 'BridgeRoadPaintAmber', 'BridgeRoadPaintWhite'):
                geometric_normal = obj.matrix_world.to_3x3() @ face.normal
                if geometric_normal.z > .999:
                    values = [(obj.matrix_world.to_3x3() @ mesh.corner_normals[loop].vector).normalized().z
                              for loop in face.loop_indices]
                    surface_normals[material] = min(surface_normals.get(material, 1), min(values))
                    assert min(values) > .9999, 'Up-facing shading corner normals: ' + obj.name + '/' + material
            if collider or material == 'BridgeAsphalt':
                ys = [vertices[index].z for index in face.vertices]
                if max(abs(y) for y in ys) < .001:
                    normal = obj.matrix_world.to_3x3() @ face.normal
                    assert normal.z > .99, 'Up-facing driving surface'
                    top_faces += 1
        parts.append(dict(name=obj.name, triangles=len(mesh.loop_triangles), positiveSignedVolume=volume,
                          nonManifoldEdges=boundary_edges, materialSlots=[m.name for m in mesh.materials]))
    assert top_faces >= 2, 'Continuous top driving surface exists'
    lo = [min(point[i] for point in points) for i in range(3)]
    hi = [max(point[i] for point in points) for i in range(3)]
    return dict(path=relative, triangles=count, boundsMin=lo, boundsMax=hi, topDrivingFaces=top_faces,
                topShadingNormalMinimumUp=surface_normals,
                topShadingNormalMaximumTiltDegrees={key: math.degrees(math.acos(max(-1, min(1, value))))
                                                    for key, value in surface_normals.items()}, parts=parts)


reports = []
previous = float('inf')
for lod in manifest['lods']:
    source(lod['fbxRelativePath'], lod['sha256'])
    report = inspect(lod['fbxRelativePath'])
    assert report['triangles'] == lod['triangles'] < previous, 'Exact descending LOD counts'
    previous = report['triangles']
    for field in ('boundsMin', 'boundsMax'):
        target = [lod[field][axis] for axis in 'xyz']
        assert max(abs(a - b) for a, b in zip(report[field], target)) < .001, 'Millimeter roundtrip bounds'
    reports.append(report)
source(manifest['colliderRelativePath'], manifest['colliderSha256'])
collision = inspect(manifest['colliderRelativePath'], True)
assert collision['triangles'] == 12
assert abs(collision['boundsMax'][1]) < .001 and abs(collision['boundsMin'][1] + .2) < .001
assert abs(collision['boundsMin'][2] + manifest['totalLength'] / 2) < .001
assert abs(collision['boundsMax'][2] - manifest['totalLength'] / 2) < .001
for texture in manifest['textures']:
    source(texture['path'], texture['sha256'])
source(manifest['sourceBlendRelativePath'], manifest['sourceBlendSha256'])
args.report.parent.mkdir(parents=True, exist_ok=True)
args.report.write_text(json.dumps(dict(status='passed', id=manifest['id'], blenderVersion=bpy.app.version_string,
    recipeHash=manifest['recipeHash'], toleranceMeters=.001, lods=reports, collision=collision,
    limits='Blender FBX roundtrip only; Unity import, final materials and presentation are separate evidence.'), indent=2) + '\n')
print('BWORK_COASTAL_BRIDGE_EXPORT_CHECK ' + str(args.report), flush=True)
