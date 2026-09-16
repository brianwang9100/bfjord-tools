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
        # Narrow tapered rafts, stretched in downstream V. No image input or isotropic
        # erosion mask: the wispy ends and splits are part of the moving raft itself.
        foam = 0.
        for lane in range(12):
            phase = lane*2.3999632297
            center = (lane*.61803398875+.13)%1
            center += .035*math.sin(TAU*v+phase)+.014*math.sin(2*TAU*v-phase*.71)+.005*math.sin(7*TAU*v+phase)
            cross = abs((u-center+.5)%1-.5)
            station = (v+.5+.37*math.sin(phase*1.73))%1-.5
            length = .10+.20*(.5+.5*math.sin(phase*2.17))
            taper = clamp(1-abs(station)/length)
            taper = taper*taper*(3-2*taper)
            width = (.016+.025*(.5+.5*math.sin(phase*.87)))*(.10+.90*taper)
            width *= .35+1.05*noise(lane/12,v,12,11,149)
            core = clamp(1-cross/max(width,.0001))
            core = core*core*(3-2*core)
            # A thinner side strand joins the parent at one end and peels downstream.
            split = abs((u-center-width*.85+.5)%1-.5)
            branch = clamp(1-split/max(width*.32,.0001))*.48*taper
            foam = max(foam, (core*(.4+.6*core)*.92+branch)*taper)
        fine = noise(u,v,83,19,931)
        foam *= .46+.54*fine
        coverage = noise(u,v,7,2,713)
        return clamp(foam), coverage, fine
    if kind == 'ocean':
        # Warped multiple-scale film: thin connected rims, torn sheets, and fine
        # cellular porosity have different footprints instead of a uniform dot grid.
        warp_u = u+.053*(noise(u,v,5,7,211)-.5)
        warp_v = v+.045*(noise(u,v,7,5,787)-.5)
        clouds = .60*noise(u,v,5,6,419)+.40*noise(u,v,11,13,733)
        meso = .67*noise(warp_u,warp_v,23,29,311)+.33*noise(warp_u,warp_v,47,53,953)
        fine = .62*noise(warp_u,warp_v,89,97,191)+.38*noise(warp_u,warp_v,173,181,577)
        rims = clamp(1-abs(meso-.50)*12)
        sheet = clamp((meso-.43)*7)*clamp((clouds-.25)*4)
        pores = clamp((fine-.22)*4.8)
        torn = clamp((clouds-.24)*4.0)
        foam = max(rims*.92, sheet)*pores*torn
        return clamp(foam), clouds, fine
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
