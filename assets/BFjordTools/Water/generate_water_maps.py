#!/usr/bin/env python3
"""Original BFjord seamless water data maps. Standard library only; no source images.

python3 generate_water_maps.py --output PATH/Textures/Water
Normal RGB stores OpenGL-style tangent normals as linear data. Foam packs
filament coverage in R and low-frequency roughness variation in G.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import zlib

TAU = 2 * math.pi

def png(path, width, height, pixels):
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))
    rows = b''.join(b'\0' + pixels[y*width*3:(y+1)*width*3] for y in range(height))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b''))

def modes(seed, count, stretch):
    rng = random.Random(seed)
    result = []
    for _ in range(count):
        x = rng.randint(1, 20)
        y = rng.randint(-12, 12)
        # Integer frequencies make the value and first derivatives periodic.
        amplitude = rng.uniform(.4, 1)/(x*x + y*y/stretch + 4)
        result.append((x, y, amplitude, rng.uniform(0, TAU)))
    return result

def height_gradient(spectrum, u, v):
    h = du = dv = 0.
    for x, y, a, phase in spectrum:
        t = TAU*(x*u+y*v)+phase
        h += a*math.sin(t)
        du += a*TAU*x*math.cos(t)
        dv += a*TAU*y*math.cos(t)
    return h, du, dv

def normal_map(path, seed, stretch):
    size = 512
    spectrum = modes(seed, 36, stretch)
    h = 1e-6
    for u,v in ((0., 0.), (.173, .781), (1., 1.)):
        value, dx, dz = height_gradient(spectrum, u, v)
        assert abs(dx-(height_gradient(spectrum,u+h,v)[0]-height_gradient(spectrum,u-h,v)[0])/(2*h)) < 1e-5
        assert abs(dz-(height_gradient(spectrum,u,v+h)[0]-height_gradient(spectrum,u,v-h)[0])/(2*h)) < 1e-5
        assert max(abs(a-b) for a,b in zip(height_gradient(spectrum,u,v),height_gradient(spectrum,u+1,v+1))) < 1e-9
    # Precompute phase trigonometry so generation needs no numerical image libraries.
    tables=[]
    for x,y,a,phase in spectrum:
        tables.append((a*TAU*x,a*TAU*y,
            [math.cos(TAU*x*(i+.5)/size+phase) for i in range(size)],
            [math.sin(TAU*x*(i+.5)/size+phase) for i in range(size)],
            [math.cos(TAU*y*(i+.5)/size) for i in range(size)],
            [math.sin(TAU*y*(i+.5)/size) for i in range(size)]))
    data=bytearray()
    for row in range(size):
        for column in range(size):
            dx=dz=0.
            for ax,az,cx,sx,cy,sy in tables:
                c=cx[column]*cy[row]-sx[column]*sy[row]
                dx+=ax*c;dz+=az*c
            dx*=.25;dz*=.25
            length=math.sqrt(dx*dx+dz*dz+1)
            data.extend(round((v/length*.5+.5)*255) for v in (-dx,-dz,1))
    png(path,size,size,data)
    return {'size':[size,size], 'seed':seed, 'modes':36, 'encoding':'linear RGB tangent normal, OpenGL +Y'}

def foam_map(path):
    size=256; cells=12; rng=random.Random(7314)
    points={(x,y):(rng.uniform(.1,.9),rng.uniform(.1,.9)) for y in range(cells) for x in range(cells)}
    data=bytearray()
    for y in range(size):
        for x in range(size):
            u=(x+.5)*cells/size;v=(y+.5)*cells/size
            distances=[]
            for iy in range(math.floor(v)-1,math.floor(v)+2):
                for ix in range(math.floor(u)-1,math.floor(u)+2):
                    a,b=points[ix%cells,iy%cells]
                    distances.append(math.hypot(u-ix-a,v-iy-b))
            distances.sort()
            edge=math.exp(-(distances[1]-distances[0])*15)
            variation=.5+.24*math.sin(TAU*x/size+1.2)*math.cos(TAU*2*y/size)+.16*math.cos(TAU*(3*x+y)/size+.7)
            foam=max(0,min(1,edge*(.55+.45*variation)))
            data.extend((round(foam*255),round(variation*255),0))
    png(path,size,size,data)
    return {'size':[size,size], 'seed':7314, 'encoding':'linear R cellular foam filaments / G roughness variation / B unused'}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    files={}
    for name,seed,stretch in [('water-ripple-normal.png',8139,2.),('water-detail-normal.png',317,1.)]:
        files[name]=normal_map(args.output/name,seed,stretch)
    files['water-foam.png']=foam_map(args.output/'water-foam.png')
    for name,entry in files.items():
        entry['sha256']=hashlib.sha256((args.output/name).read_bytes()).hexdigest()
        entry['bytes']=(args.output/name).stat().st_size
    print(json.dumps({'schemaVersion':1,'author':'BFjord Tools contributors','license':'CC0-1.0','generatorLicense':'MIT','source':'Original mathematical texture synthesis; no third-party image inputs','files':files},indent=2))

if __name__=='__main__': main()
