#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Verify original tunnel payload without Blender or Unity. Does not modify assets."""
import argparse,hashlib,json,math
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('catalog',type=Path);p.add_argument('additions',type=Path);a=p.parse_args()
for item in json.loads(a.additions.read_text())['files']:
    path=a.catalog/item['path'];data=path.read_bytes()
    assert len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256'],path
model=json.loads((a.catalog/'Assets/BFjord/TunnelDetail/MasonryPortal.json').read_text())
assert model['width']==8 and model['clearance']==5.5 and len(model['lods'])==3
counts=[]
for level in model['lods']:
    vertices=level['vertices'];normals=level['normals'];uv=level['uv'];idx=level['triangles']
    assert len(vertices)==len(normals)==len(uv) and len(idx)%3==0
    assert all(math.isfinite(c) for v in vertices+normals+uv for c in v.values())
    assert min(v['y'] for v in vertices)>=-.351 and max(abs(v['x']) for v in vertices)<8
    for i in range(0,len(idx),3):
        x,y,z=[vertices[idx[j]] for j in range(i,i+3)]
        u=[y[k]-x[k] for k in ('x','y','z')];v=[z[k]-x[k] for k in ('x','y','z')]
        cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
        length=math.sqrt(sum(c*c for c in cross));assert length>1e-9
        n=normals[idx[i]];assert sum(c*n[k] for c,k in zip(cross,('x','y','z')))/length>.99
        ua,ub,uc=[uv[idx[j]] for j in range(i,i+3)]
        assert abs((ub['x']-ua['x'])*(uc['y']-ua['y'])-(ub['y']-ua['y'])*(uc['x']-ua['x']))>1e-10
    counts.append(len(idx)//3)
assert counts[0]>counts[1]>counts[2]
print(json.dumps(dict(result='TUNNEL_PAYLOAD_PASS',lodTriangles=counts,checks=['catalog bytes/hash','metric bounds/contact','finite channels','normal/winding','positive triangle/UV areas','strictly reducing LODs'])))
