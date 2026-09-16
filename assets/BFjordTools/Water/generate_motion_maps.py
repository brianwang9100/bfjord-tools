#!/usr/bin/env python3
"""Original seamless river, ocean and waterfall data; Python standard library only.

Maps are CC0-1.0; this generator is MIT. No source images or vendor code.
Run with --output path/to/Textures/Water; stdout is a provenance manifest.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
from generate_water_maps import png

TAU = math.tau


def clamp(x):
    return max(0, min(1, x))


def noise(u, v, nx, ny, seed):
    x, y = u*nx, v*ny
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = x-ix, y-iy
    fx = fx*fx*(3-2*fx); fy = fy*fy*(3-2*fy)
    def point(a, b):
        value = ((a % nx)*374761393 + (b % ny)*668265263 + seed*1442695041) & 0xffffffff
        value = ((value ^ (value >> 13))*1274126177) & 0xffffffff
        return (value ^ (value >> 16))/4294967295
    a = point(ix, iy)*(1-fx)+point(ix+1, iy)*fx
    b = point(ix, iy+1)*(1-fx)+point(ix+1, iy+1)*fx
    return a*(1-fy)+b*fy


def field(u, v, kind):
    if kind == 'waterfall':
        # Uneven anisotropic clouds, not a periodic comb of parallel bright strands.
        warp = (noise(u,v,5,4,971)-.5)*.13
        f = (.39*noise(u+warp,v,7,3,917) + .30*noise(u+warp,v,19,7,293)
             + .20*noise(u+warp,v,43,17,677) + .11*noise(u+warp,v,89,37,431))
        fine = noise(u+warp,v,73,23,183)
        # Separate persistent channels from small aerated ropes. Broad coverage is deliberately
        # incomplete: the shader can reveal wet rock between moving whitewater filaments.
        lanes = noise(u+warp*.35,v,13,2,151)
        ropes = clamp((f-.38)*3.5)
        coverage = clamp(.10+lanes*.72+ropes*.22)
        return clamp(ropes*(.40+.80*lanes)), coverage, fine

    if kind == 'river':
        warp = (noise(u,v,4,5,271)-.5)*.1
        fine = noise(u+warp,v,47,23,931)
        f = (.50*noise(u+warp,v,11,7,571) + .32*noise(u+warp,v,27,13,351) + .18*fine)
        coverage = noise(u,v,5,4,713)
        threads = clamp((f-.53)*4.3)*clamp((coverage-.34)*3)
        return threads, coverage, fine
    if kind == 'ocean':
        clouds = (.58*noise(u,v,5,6,419) + .28*noise(u,v,13,17,733) + .14*noise(u,v,37,41,953))
        breakup = noise(u,v,23,29,311)
        # Dense aerated film with round holes, not continuous Voronoi wire outlines.
        # The shader supplies crest/shore placement; this map supplies porous white coverage.
        cells=43;px=u*cells;py=v*cells;ix=math.floor(px);iy=math.floor(py);nearest=2.
        for oy in (-1,0,1):
            for ox in (-1,0,1):
                cx=ix+ox;cy=iy+oy
                jx=noise((cx%cells)/cells,(cy%cells)/cells,cells,cells,211)
                jy=noise((cx%cells)/cells,(cy%cells)/cells,cells,cells,787)
                nearest=min(nearest,math.hypot(px-cx-.15-.7*jx,py-cy-.15-.7*jy))
        bubbles=clamp((nearest-.10)*3.7)
        foam=clamp(.16+bubbles*.76)*(.63+.37*clouds)
        return foam, clouds, breakup
    raise ValueError(kind)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    files = {}
    for kind in ('river', 'ocean', 'waterfall'):
        for u, v in ((.183,.829), (.712,.143), (0,0)):
            a, b = field(u,v,kind), field(u+1,v+1,kind)
            assert max(abs(x-y) for x,y in zip(a,b)) < 1e-11, 'Nonperiodic field'
        data = bytearray()
        size = 512
        for y in range(size):
            for x in range(size):
                data.extend(round(clamp(c)*255) for c in field((x+.5)/size,(y+.5)/size,kind))
        name = kind+'-motion.png'
        png(args.output/name,size,size,data)
        payload = (args.output/name).read_bytes()
        files[name] = dict(size=[size,size], sha256=hashlib.sha256(payload).hexdigest(), bytes=len(payload),
                          encoding='linear RGB: foam streaks / sheet or swell coverage / breakup')
    print(json.dumps(dict(schemaVersion=1,author='BFjord Tools contributors',license='CC0-1.0',
                         generatorLicense='MIT',source='Original periodic mathematical fields, no image inputs',files=files),indent=2))


if __name__ == '__main__':
    main()
