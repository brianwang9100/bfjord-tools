# SPDX-License-Identifier: MIT
"""Actual FBX re-import: finite geometry, triangle budgets, UVs and contact bounds."""
import json
import math
from pathlib import Path

import bpy
import bmesh

OUT = next(candidate for parent in Path(__file__).resolve().parents
           for candidate in (parent / 'assets/BFjordTools/Rocks', parent / 'art/BFjordTools/Rocks')
           if (candidate / 'manifest.json').is_file())
manifest = json.loads((OUT / 'manifest.json').read_text())
report = []
for variant in manifest['variants']:
    previous = float('inf')
    for item in variant['lods'] + [{'path': variant['colliderPath'], 'triangles': variant['colliderTriangles']}]:
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=str(OUT / item['path']), use_custom_normals=True)
        objects = [o for o in bpy.context.selected_objects if o.type == 'MESH']
        assert len(objects) == 1, (item['path'], 'Expected one mesh')
        obj = objects[0]
        mesh = obj.data
        mesh.calc_loop_triangles()
        count = len(mesh.loop_triangles)
        assert count == item['triangles'], (item['path'], count, item['triangles'])
        assert all(math.isfinite(x) for v in mesh.vertices for x in v.co), item['path']
        world = [obj.matrix_world @ v.co for v in mesh.vertices]
        mins = [min(p[i] for p in world) for i in range(3)]
        maxs = [max(p[i] for p in world) for i in range(3)]
        # FBX importer restores Blender Z-up. Unity receives Y-up via FBX axes.
        assert abs(mins[2]) < .065, (item['path'], 'Base lifted', mins)
        assert all(maxs[i] > mins[i] for i in range(3)), item['path']
        actual_size = [maxs[0]-mins[0], maxs[2]-mins[2], maxs[1]-mins[1]]
        expected = variant['boundsSize']
        assert all(abs(a-e) < .22 * e for a,e in zip(actual_size, expected)), (item['path'], actual_size, expected)
        if '_COL' not in item['path']:
            assert count < previous, (item['path'], 'LOD does not simplify')
            previous = count
            assert len(mesh.uv_layers) > 0, item['path']
            assert all(math.isfinite(x) for uv in mesh.uv_layers.active.data for x in uv.uv), item['path']
            assert all(all(math.isfinite(x) for x in v.normal) and v.normal.length > .9 for v in mesh.vertices), item['path']
            mesh.calc_tangents()
            assert all(math.isfinite(x) for loop in mesh.loops for x in loop.tangent), item['path']
        else:
            assert count <= 170, (item['path'], 'Collision exceeds convex budget')
        bm = bmesh.new()
        bm.from_mesh(mesh)
        # FBX may duplicate seam vertices; weld coincident points before topology check.
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00001)
        boundary_edges = sum(1 for e in bm.edges if e.is_boundary)
        bm.free()
        assert boundary_edges == 0, (item['path'], 'Open surface', boundary_edges)
        report.append({'path': item['path'], 'triangles': count, 'boundsSizeUnity': actual_size,
                       'minimumBlenderZ': mins[2], 'closedSurface': True})
(OUT / 'Review/verification.json').write_text(json.dumps({'result': 'passed', 'checks': report}, indent=2)+'\n')
print('ROCK_FBX_VERIFICATION_PASSED ' + str(len(report)))
