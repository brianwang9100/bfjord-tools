#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Write only v0.5 woodland Unity metadata and family additions; leave shared manifest to integration."""
import argparse,hashlib,json,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--catalog',required=True);a=p.parse_args();root=Path(a.catalog);folder=root/'Assets/BFjord/OriginalFoliage'
model=(root/'Assets/Bwork/ThirdParty/PolyHaven/Models/fern_02_a.fbx.meta').read_text();texture=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_BaseMap.png.meta').read_text();normal=(root/'Assets/Bwork/ThirdParty/PolyHaven/Textures/fern_02_Normal.png.meta').read_text()
names=['Models/'+n+'.fbx' for n in ('MatureOak_A','SilverBirch_A','FallenHollowLog_A','TallMeadowGrass_A')]+['Textures/'+n+'.png' for n in ('WoodlandAtlas','WoodlandNormal','WoodlandMask')]+['woodland-manifest.json']
for name in names:
 path=folder/name;assert path.is_file(),path;guid=hashlib.sha256(('bfjord-original-foliage-v1/'+name).encode()).hexdigest()[:32]
 if path.suffix=='.fbx':text=re.sub(r'guid: [a-f0-9]+','guid: '+guid,model)
 elif path.suffix=='.png':
  text=re.sub(r'guid: [a-f0-9]+','guid: '+guid,normal if path.stem=='WoodlandNormal' else texture);text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',text);text=text.replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
  if path.stem=='WoodlandMask':text=text.replace('sRGBTexture: 1','sRGBTexture: 0')
 else:text='fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 path.with_suffix(path.suffix+'.meta').write_text(text)
paths=[folder/n for n in names];paths+=[x.with_suffix(x.suffix+'.meta') for x in list(paths)]
entries=[{'path':str(x.relative_to(root)),'bytes':x.stat().st_size,'sha256':hashlib.sha256(x.read_bytes()).hexdigest()} for x in sorted(paths)]
(folder/'woodland-catalog-additions.json').write_text(json.dumps({'revision':'woodland-05','files':entries},indent=2)+'\n');print(json.dumps({'files':len(entries),'bytes':sum(x['bytes'] for x in entries)}))
