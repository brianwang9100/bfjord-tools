#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Create deterministic Unity import metadata and bounded family catalog additions."""
import hashlib,json,re
from pathlib import Path

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools'):
            if candidate.is_dir():return candidate
    raise FileNotFoundError('Could not find art/BFjordTools or assets/BFjordTools above '+str(script_file))

ART=find_art_root(__file__);root=ART/'asset-catalog';folder=root/'Assets/BFjord/ForestFloorDetail'
model=(root/'Assets/Bwork/ThirdParty/PolyHaven/Models/fern_02_a.fbx.meta').read_text();texture=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_BaseMap.png.meta').read_text();normal=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_Normal.png.meta').read_text()
paths=sorted(p for p in folder.rglob('*') if p.is_file() and p.suffix in ('.png','.fbx','.json') and p.name!='catalog-additions.json')
for path in paths:
 name=path.relative_to(folder).as_posix();guid=hashlib.sha256(('bfjord-forest-floor-07/'+name).encode()).hexdigest()[:32]
 if path.suffix=='.fbx':txt=re.sub(r'guid: [a-f0-9]+','guid: '+guid,model)
 elif path.suffix=='.png':
  txt=re.sub(r'guid: [a-f0-9]+','guid: '+guid,normal if path.stem.endswith('Normal') else texture);txt=re.sub(r'maxTextureSize: \d+','maxTextureSize: 2048',txt);txt=txt.replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
  if path.stem.endswith('Mask'):txt=txt.replace('sRGBTexture: 1','sRGBTexture: 0')
 else:txt='fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 path.with_suffix(path.suffix+'.meta').write_text(txt)
paths += [p.with_suffix(p.suffix+'.meta') for p in list(paths)]
entries=[{'path':p.relative_to(root).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(paths)]
result={'revision':'forest-floor-07','files':entries};(folder/'catalog-additions.json').write_text(json.dumps(result,indent=2)+'\n');(ART/'ForestFloorDetail/catalog-additions.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'files':len(entries),'bytes':sum(e['bytes'] for e in entries)}))
