#!/usr/bin/env python3
"""Pack pinned CC0 scan maps for Unity Terrain. SPDX-License-Identifier: MIT."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid
import re
from PIL import Image, ImageOps


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_manifest(sources):
    assets = sources.get('assets', [])
    if sources.get('schemaVersion') != 1 or sources.get('license') != 'CC0-1.0' or not 1 <= len(assets) <= 16:
        raise ValueError('Expected 1–16 explicitly licensed CC0 surfaces')
    ids = set()
    for asset in assets:
        name = asset.get('id', '')
        if not isinstance(name, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]{0,63}', name) or name in ids:
            raise ValueError('Surface IDs must be unique safe names')
        ids.add(name)
        if set(asset.get('files', {})) != {'Diffuse', 'nor_gl', 'arm', 'Displacement'}:
            raise ValueError('Expected four matched scan maps')
        for source in asset['files'].values():
            if type(source.get('size')) is not int or not 1 <= source['size'] <= 16 * 1024 * 1024:
                raise ValueError('Scan source exceeds 16 MiB limit')
            if not re.fullmatch(r'[a-f0-9]{32}', source.get('md5', '')) or not source.get('url', '').startswith('https://dl.polyhaven.org/file/ph-assets/Textures/'):
                raise ValueError('Expected a pinned public scan URL and digest')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-manifest', type=Path, required=True)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--catalog', type=Path, required=True)
    parser.add_argument('--fetch', action='store_true')
    args = parser.parse_args()
    sources = json.loads(args.source_manifest.read_text())
    validate_manifest(sources)
    args.cache.mkdir(parents=True, exist_ok=True)
    target = args.catalog / 'Assets/BFjord/TerrainDetail/Surfaces'
    if not target.resolve().is_relative_to(args.catalog.resolve()):
        raise ValueError('Surface directory escapes the catalog')
    target.mkdir(parents=True, exist_ok=True)
    entries = []
    for asset in sources['assets']:
        maps = {}
        for role, source in asset['files'].items():
            path = args.cache / Path(source['url']).name
            if path.is_symlink():
                raise ValueError('Cache entries must be regular files')
            if not path.exists() and args.fetch:
                if not source['url'].startswith('https://dl.polyhaven.org/file/ph-assets/Textures/'):
                    raise ValueError('Unexpected asset host')
                subprocess.run(['curl', '-fsSL', '--max-time', '60', '--connect-timeout', '15',
                                '--max-filesize', str(source['size']), '-A', 'Mozilla/5.0', source['url'], '-o', str(path)], check=True)
            if path.stat().st_size != source['size']:
                raise ValueError('Source size mismatch: ' + path.name)
            data = path.read_bytes()
            if len(data) != source['size'] or hashlib.md5(data).hexdigest() != source['md5']:
                raise ValueError('Source integrity mismatch: ' + path.name)
            with Image.open(path) as image:
                if not (1 <= image.width <= 4096 and 1 <= image.height <= 4096):
                    raise ValueError('Decoded scan dimensions exceed 4096 pixels')
                maps[role] = image.convert('RGB').resize((1024, 1024), Image.Resampling.LANCZOS)
            source['sha256'] = hashlib.sha256(data).hexdigest()
        arm = maps['arm'].split()
        outputs = {'Color': maps['Diffuse'], 'Normal': maps['nor_gl'],
                   'Mask': Image.merge('RGBA', (Image.new('L', (1024, 1024), 0), arm[0], maps['Displacement'].getchannel('R'), ImageOps.invert(arm[1])))}
        for suffix, data in outputs.items():
            path = target / (asset['id'] + '_' + suffix + '.png')
            for output in [path, path.with_suffix('.png.meta')]:
                if output.is_symlink() or not output.resolve().is_relative_to(args.catalog.resolve()):
                    raise ValueError('Output escapes the catalog')
            data.save(path, optimize=True)
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'bfjord:terrain-bank:1:' + path.name).hex
            meta = f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 1
    sRGBTexture: {1 if suffix == 'Color' else 0}
  isReadable: 0
  textureType: {1 if suffix == 'Normal' else 0}
  textureShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 8
    wrapU: 0
    wrapV: 0
    wrapW: 0
  userData: CC0 Poly Haven scan. Terrain mask is R metal G AO B height A smoothness.
'''
            path.with_suffix('.png.meta').write_text(meta)
            for output in [path, path.with_suffix('.png.meta')]:
                entries.append({'path': output.relative_to(args.catalog).as_posix(), 'bytes': output.stat().st_size, 'sha256': digest(output)})
    args.source_manifest.write_text(json.dumps(sources, indent=2) + '\n')
    (args.source_manifest.parent / 'surface-bank-additions.json').write_text(json.dumps({'schemaVersion': 1, 'files': entries}, indent=2) + '\n')
    print(json.dumps({'textures': len(entries)//2, 'bytes': sum(x['bytes'] for x in entries)}))


if __name__ == '__main__':
    main()
