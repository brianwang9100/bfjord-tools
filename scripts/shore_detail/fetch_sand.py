#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Acquire the pinned 2K CC0 archive; extract only the five matched sand maps."""
from pathlib import Path
import hashlib, json, subprocess, tempfile, zipfile

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools', parent/'assets/BFjordTools'):
            if candidate.is_dir(): return candidate
    raise FileNotFoundError('Could not find BFjordTools art root')

def verified(path, entry, allow_exported=False):
    if not path.is_file(): return False
    size = path.stat().st_size
    candidates = [(entry['bytes'], entry['sha256'])]
    # Public exports strip PNG metadata; only locally retained maps may use
    # that separately pinned representation. Supplier downloads stay strict.
    if allow_exported and 'exportedBytes' in entry and 'exportedSha256' in entry:
        candidates.append((entry['exportedBytes'], entry['exportedSha256']))
    matching_hashes = [digest for count, digest in candidates if count == size]
    return bool(matching_hashes) and hashlib.sha256(path.read_bytes()).hexdigest() in matching_hashes

if __name__ == '__main__':
    source = find_art_root(__file__)/'ShoreDetail/Sources'
    manifest = json.loads((source/'sand-source.json').read_text())
    if not all(verified(source/e['file'], e, allow_exported=True) for e in manifest['files'].values()):
        archive = manifest['archive']
        assert archive['url'] == 'https://ambientcg.com/get?file=Ground052_2K-PNG.zip'
        with tempfile.TemporaryDirectory(prefix='bfjord-sand-') as temporary:
            path = Path(temporary)/'source.zip'
            subprocess.run(['curl', '-fLsS', '--retry', '2', '--max-time', '120', '--max-filesize', str(archive['bytes']), archive['url'], '-o', str(path)], check=True)
            assert verified(path, archive), 'Supplier archive changed; do not silently repin it.'
            with zipfile.ZipFile(path) as bundle:
                for entry in manifest['files'].values():
                    assert Path(entry['file']).name == entry['file']
                    data = bundle.read(entry['archiveMember'])
                    assert len(data) == entry['bytes'] and hashlib.sha256(data).hexdigest() == entry['sha256']
                    (source/entry['file']).write_bytes(data)
    assert all(verified(source/e['file'], e, allow_exported=True) for e in manifest['files'].values())
    print('Five exact CC0 Ground052 source maps verified')
