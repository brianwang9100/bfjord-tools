#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Write stable metadata and a separate catalog addition; never modify the shared catalog."""
import argparse,hashlib,json,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--templates',required=True);a=p.parse_args();root=Path(a.source);templates=Path(a.templates)
model=(templates/'Assets/Bwork/ThirdParty/PolyHaven/Models/fern_02_a.fbx.meta').read_text()
texture=(templates/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_BaseMap.png.meta').read_text()
normal=(templates/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_Normal.png.meta').read_text()
files=[*sorted((root/'Models').glob('*.fbx')),*sorted((root/'Textures').glob('*.png')),root/'manifest.json',root/'LICENSE-CC0.txt']
for path in files:
 guid=hashlib.sha256(('bfjord-road-detail-v1/'+str(path.relative_to(root))).encode()).hexdigest()[:32]
 if path.suffix=='.fbx':text=model.replace('isReadable: 0','isReadable: 1')
 elif path.suffix=='.png':
  text=normal if path.stem.endswith('_Normal') else texture
  text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 512',text)
  text=text.replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
 else:text='fileFormatVersion: 2\nguid: PLACEHOLDER\nTextScriptImporter:\n  externalObjects: {}\n'
 text=re.sub(r'guid: [a-f0-9]+|guid: PLACEHOLDER','guid: '+guid,text)
 path.with_suffix(path.suffix+'.meta').write_text(text)
entries=[]
for path in files:
 for f in [path,path.with_suffix(path.suffix+'.meta')]:entries.append({'path':'Assets/BFjord/RoadDetail/'+str(f.relative_to(root)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
(root/'catalog-additions.json').write_text(json.dumps({'sourceRoot':'art/BFjordTools/RoadDetail','destinationRoot':'Assets/BFjord/RoadDetail','files':entries},indent=2)+'\n')
print(json.dumps({'files':len(entries),'bytes':sum(e['bytes'] for e in entries)}))
