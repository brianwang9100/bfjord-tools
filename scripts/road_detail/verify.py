#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Independent check of shipped road FBXs against their material and geometric contract."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
p=argparse.ArgumentParser();p.add_argument('--source',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(a.source).resolve()
m=json.loads((root/'manifest.json').read_text());checks=[]
for entry in m['assets']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 f=root/entry['file'];assert hashlib.sha256(f.read_bytes()).hexdigest()==entry['sha256']
 bpy.ops.import_scene.fbx(filepath=str(f),use_anim=False)
 objects=[o for o in bpy.context.selected_objects if o.type=='MESH'];assert len(objects)==1
 o=objects[0];mesh=o.data
 assert len(mesh.polygons)==entry['triangles']
 slots=[s.material.name.split('.')[0] for s in o.material_slots];assert slots==entry['materialSlots'],(slots,entry['materialSlots'])
 points=[o.matrix_world@v.co for v in mesh.vertices]
 bounds=[[min(v[k] for v in points),max(v[k] for v in points)] for k in range(3)]
 assert all(abs(bounds[k][j]-entry['boundsBlender'][k][j])<.00001 for k in range(3) for j in range(2))
 assert len(mesh.uv_layers)==1 and all(math.isfinite(k) for l in mesh.uv_layers.active.data for k in l.uv)
 assert all(math.isfinite(c) for v in points for c in v)
 assert all(f.normal.length>.99 and all(math.isfinite(k) for k in f.normal) for f in mesh.polygons)
 for face in mesh.polygons:
  assert len(face.vertices)==3
  v=[mesh.vertices[i].co for i in face.vertices];assert (v[1]-v[0]).cross(v[2]-v[0]).length>1e-9
 checks.append({'asset':entry['file'],'triangles':entry['triangles'],'materials':slots,'boundsMatch':True,'validNormals':True,'validUVs':True})
for asset in set(e['id'] for e in m['assets']):
 ts=[e['triangles'] for e in m['assets'] if e['id']==asset];assert ts[0]>ts[1]>ts[2]
result={'blenderVersion':bpy.app.version_string,'verifiedFBXs':len(checks),'checks':checks}
(root/'Review/contract-verification.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
