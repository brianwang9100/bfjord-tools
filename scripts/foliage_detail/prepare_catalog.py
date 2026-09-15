#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Prepare deterministic Unity metadata and append only OriginalFoliage catalog entries."""
import argparse, hashlib, json, re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--catalog',required=True);a=p.parse_args();root=Path(a.catalog)
folder=root/'Assets/BFjord/OriginalFoliage'
model=(root/'Assets/Bwork/ThirdParty/PolyHaven/Models/fern_02_a.fbx.meta').read_text()
texture=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_BaseMap.png.meta').read_text()
normal=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_Normal.png.meta').read_text()
for path in sorted(folder.rglob('*')):
 if not path.is_file() or path.suffix=='.meta':continue
 guid=hashlib.sha256(('bfjord-original-foliage-v1/'+str(path.relative_to(folder))).encode()).hexdigest()[:32]
 if path.suffix=='.fbx':text=re.sub(r'guid: [a-f0-9]+','guid: '+guid,model)
 elif path.suffix=='.png':
  text=re.sub(r'guid: [a-f0-9]+','guid: '+guid,normal if path.stem=='FoliageNormal' else texture)
  text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 512',text)
  text=text.replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
 else:text='fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 path.with_suffix(path.suffix+'.meta').write_text(text)
manifest=root/'manifest.json';data=json.loads(manifest.read_text())
entries=[x for x in data['files'] if not x['path'].startswith('Assets/BFjord/OriginalFoliage/')]
for path in sorted(folder.rglob('*')):
 if path.is_file():entries.append({'path':str(path.relative_to(root)),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
data['files']=sorted(entries,key=lambda x:x['path']);manifest.write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'originalFoliageFiles':len([x for x in entries if x['path'].startswith('Assets/BFjord/OriginalFoliage/')]),'totalBytes':sum(x['bytes'] for x in entries if x['path'].startswith('Assets/BFjord/OriginalFoliage/'))}))
