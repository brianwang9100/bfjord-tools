#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Catch accidental double sRGB decoding in the published scan color maps."""
from pathlib import Path
from PIL import Image, ImageChops, ImageStat

def find_art_root(script_file):
    for parent in Path(script_file).resolve().parents:
        for candidate in (parent/'art/BFjordTools',parent/'assets/BFjordTools'):
            if candidate.is_dir():return candidate
    raise FileNotFoundError('Could not find art/BFjordTools or assets/BFjordTools above '+str(script_file))

ART=find_art_root(__file__)
family=ART/'ShoreDetail'
source=Image.open(family/'Sources/sand_02_diff_2k.png').convert('RGB')
for name in ['BeachSand_Color.png','RippleSand_Color.png']:
    for folder in [family/'Textures',ART/'asset-catalog/Assets/BFjord/ShoreDetail/Textures']:
        encoded=Image.open(folder/name).convert('RGB')
        assert encoded.size==source.size,(name,'dimensions')
        extrema=ImageChops.difference(source,encoded).getextrema()
        assert all(hi<=1 for lo,hi in extrema),(name,'encoded scan differs beyond 8-bit quantization',extrema)
        print(name,ImageStat.Stat(encoded).mean)
print('Source sRGB scan values preserved in local and catalog sand textures')
