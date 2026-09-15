#!/usr/bin/env python3
"""Build native Unity Terrain masks from the admitted CC0 scans. SPDX-License-Identifier: MIT."""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request
import uuid
from PIL import Image, ImageOps

ART = next(candidate for parent in Path(__file__).resolve().parents
           for candidate in (parent / 'assets/BFjordTools/TerrainDetail', parent / 'art/BFjordTools/TerrainDetail')
           if (candidate / 'sources.json').is_file())
CATALOG = ART.parent / 'asset-catalog'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--fetch', action='store_true', help='Fetch only the pinned public CC0 inputs.')
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    sources = json.loads((ART / 'sources.json').read_text())
    target = CATALOG / 'Assets/BFjord/TerrainDetail'
    target.mkdir(parents=True, exist_ok=True)
    entries, measurements = [], []
    for asset in sources['assets']:
        inputs = {}
        for source in asset['files']:
            path = args.cache / source['file']
            if not path.exists() and args.fetch:
                request = urllib.request.Request(source['url'], headers={'User-Agent': 'BFjordTools CC0 terrain preparation'})
                with urllib.request.urlopen(request, timeout=60) as response:
                    path.write_bytes(response.read())
            raw = path.read_bytes()
            if len(raw) != source['size'] or hashlib.md5(raw).hexdigest() != source['md5']:
                raise ValueError('Source does not match pinned provider metadata: ' + str(path))
            source['sha256'] = sha(path)
            inputs[source['role']] = Image.open(path).convert('RGB').resize((512, 512), Image.Resampling.LANCZOS)
        arm = inputs['arm'].split()
        # PH ARM is R=occlusion, G=roughness, B=metallic. Terrain is a different contract.
        height = inputs['Displacement'].getchannel('R')
        output = Image.merge('RGBA', (Image.new('L', height.size, 0), arm[0], height, ImageOps.invert(arm[1])))
        path = target / (asset['layer'] + '_TerrainMask.png')
        output.save(path, optimize=True)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'bfjord-tools:terrain-detail:v1:' + path.name).hex
        meta = '''fileFormatVersion: 2
guid: GUID
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
  isReadable: 0
  textureType: 0
  textureShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    wrapU: 0
    wrapV: 0
    wrapW: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    overridden: 0
  userData: CC0 Poly Haven terrain mask. R=0 G=AO B=height A=smoothness. Linear data.
'''
        path.with_suffix('.png.meta').write_text(meta.replace('GUID', guid))
        for file in [path, path.with_suffix('.png.meta')]:
            entries.append(dict(path=file.relative_to(CATALOG).as_posix(), bytes=file.stat().st_size, sha256=sha(file)))
        measurements.append(dict(layer=asset['layer'], tileMeters=asset['tileMeters'], size=list(output.size),
                                 channels=['metallic', 'occlusion', 'height', 'smoothness'], channelExtrema=output.getextrema(), sha256=sha(path)))
    (ART / 'sources.json').write_text(json.dumps(sources, indent=2) + '\n')
    (ART / 'catalog-additions.json').write_text(json.dumps(dict(schemaVersion=1, files=entries), indent=2) + '\n')
    (ART / 'manifest.json').write_text(json.dumps(dict(schemaVersion=1, license='CC0-1.0',
        generator='Scripts/world_assets/terrain_detail/build_masks.py', generatorLicense='MIT',
        outputBytes=sum(e['bytes'] for e in entries), textures=measurements), indent=2) + '\n')
    print(json.dumps(dict(textures=len(measurements), bytes=sum(e['bytes'] for e in entries))))


if __name__ == '__main__':
    main()
