# SPDX-License-Identifier: MIT
"""Verify the authored waterfall feeder footprint against exported ledge geometry."""
import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root = next(candidate for parent in Path(__file__).resolve().parents
           for candidate in (parent / 'assets/BFjordTools/Rocks', parent / 'art/BFjordTools/Rocks')
           if (candidate / 'manifest.json').is_file())
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root/'Models/river_ledge_LOD0.fbx'))
o=next(o for o in bpy.context.selected_objects if o.type=='MESH');m=o.data
v=[o.matrix_world @ p.co for p in m.vertices];polys=[list(p.vertices) for p in m.polygons];tree=BVHTree.FromPolygons(v,polys)
contacts=[]
# Exact intended feeder rectangle expressed in the unscaled Blender XY ground plane.
x=-3.55
while x<=3.551:
 z=-.15
 while z<=1.451:
  local=Vector((x/(12.95/6),-z/(6.8/4),10))
  hit,normal,index,distance=tree.ray_cast(local,Vector((0,0,-1)),20)
  assert hit is not None,(x,z,'unsupported feeder point')
  contacts.append({'fixtureX':x,'fixtureZ':z,'sourceHeight':hit.z})
  z+=.2
 x+=.355
(root/'Review/river-ledge-contact.json').write_text(json.dumps({'result':'passed','scope':'Representative feeder-cap rectangle at width12.95m depth6.8m. Actual Unity projection remains integration evidence.','samples':contacts},indent=2)+'\n')
print('RIVER_LEDGE_FEEDER_CONTACT_PASSED',len(contacts))
