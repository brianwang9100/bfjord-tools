#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Fetch only the four pinned CC0 Sand 02 maps and verify exact source bytes."""
from pathlib import Path
import hashlib,json,subprocess

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools'):
            if candidate.is_dir():return candidate
    raise FileNotFoundError('Could not find art/BFjordTools or assets/BFjordTools above '+str(script_file))

ART=find_art_root(__file__)
source=ART/'ShoreDetail/Sources'
manifest=json.loads((source/'sand-source.json').read_text())
for role,entry in manifest['files'].items():
    assert entry['url'].startswith('https://dl.polyhaven.org/file/ph-assets/Textures/png/2k/sand_02/')
    p=source/Path(entry['url']).name
    if not p.exists():
        subprocess.run(['curl','-fLsS','--retry','2','--max-time','90','--max-filesize',str(entry['bytes']),entry['url'],'-o',str(p)],check=True)
    assert p.stat().st_size==entry['bytes'],role
    assert hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256'],role
print('Four exact CC0 sand source maps verified')
