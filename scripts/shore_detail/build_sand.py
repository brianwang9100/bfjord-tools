#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Reproducible 2K ivory scan treatment. No shell/atlas/FBX authoring occurs here."""
from pathlib import Path
import hashlib, json, math, shutil
import numpy as np
from PIL import Image
from fetch_sand import find_art_root, verified

ART = find_art_root(__file__)
OUT = ART/'ShoreDetail'
CAT = ART/'asset-catalog/Assets/BFjord/ShoreDetail'
import sys
COLORS_ONLY = '--colors-only' in sys.argv

def decode_srgb(value):
    return np.where(value <= .04045, value/12.92, ((value+.055)/1.055)**2.4)

def encode_srgb(value):
    return np.where(value <= .0031308, value*12.92, 1.055*value**(1/2.4)-.055)

def ivory_color(encoded, treatment):
    linear = decode_srgb(encoded)
    luminance = np.sum(linear*np.array([.2126,.7152,.0722],np.float32),axis=2,keepdims=True)
    relative = np.maximum(luminance,1e-6)/luminance.mean(dtype=np.float64)
    # Mineral color is authored; photogrammetry's fine deposits/variation survive.
    chroma = linear/np.maximum(luminance,1e-6)
    chroma /= chroma.mean(axis=(0,1),keepdims=True,dtype=np.float64)
    color = np.array(treatment['linearBaseColor'],np.float32)*relative**treatment['luminanceContrastExponent']
    color *= 1+(chroma-1)*treatment['sourceChromaRetention']
    knee, ceiling = treatment['linearSoftKnee'], treatment['linearUpperBound']
    color = np.where(color>knee, knee+(ceiling-knee)*(1-np.exp(-np.maximum(color-knee,0)/(ceiling-knee))), color)
    return encode_srgb(np.maximum(color,0))

def source_arrays():
    manifest = json.loads((OUT/'Sources/sand-source.json').read_text())
    arrays = {}
    for role, entry in manifest['files'].items():
        path = OUT/'Sources'/entry['file']
        assert verified(path,entry,allow_exported=True), 'Run fetch_sand.py first: '+role
        im = Image.open(path)
        assert im.size == (2048,2048), (role, im.size)
        # PIL reduces RGB16 to RGB8; monochrome height retains its 16-bit range.
        if role in ('diff','nor_gl'): arrays[role] = np.asarray(im.convert('RGB'),dtype=np.float32)/255
        else:
            values = np.asarray(im,dtype=np.float32)
            if values.ndim == 3: values = values[:,:,0]
            arrays[role] = values/(65535 if im.mode in ('I','I;16') else 255)
    return manifest, arrays

def tangent_normal(encoded):
    # Unity normal imports reconstruct Z from XY. Source downsampling leaves B
    # non-unit, so bake that same positive-hemisphere convention explicitly.
    xy = encoded[:,:,:2]*2-1
    length = np.linalg.norm(xy,axis=2,keepdims=True)
    xy /= np.maximum(length,1)
    z = np.sqrt(np.maximum(1-np.sum(xy*xy,axis=2,keepdims=True),0))
    return np.concatenate([xy,z],axis=2)*.5+.5

def save(name, array):
    if COLORS_ONLY and not name.endswith("_Color.png"): return
    path = OUT/'Textures'/name
    Image.fromarray(np.rint(np.clip(array,0,1)*255).astype(np.uint8)).save(path,optimize=True)
    shutil.copy2(path,CAT/'Textures'/name)

def build():
    manifest, a = source_arrays()
    color = ivory_color(a['diff'],manifest['derivation'])
    height, normal = a['disp'], tangent_normal(a['nor_gl'])
    mask = np.stack([np.zeros_like(height),a['ao'],height,1-a['rough']],axis=2)
    save('BeachSand_Color.png',color)
    save('BeachSand_NormalGL.png',normal)
    save('BeachSand_Mask.png',mask)
    # Optional dunes use 12 broad 16.7 cm ripples at 2.5 mm amplitude. Never
    # bake ripple lighting into the albedo or force ripples over the whole beach.
    n = height.shape[0]
    y,x = np.mgrid[0:n,0:n]/n
    phase = y*math.tau*12+.42*np.sin(x*math.tau*3)+.15*np.sin(x*math.tau*7)
    ripple = np.sin(phase)+.22*np.sin(phase*2)
    dx = .42*np.cos(x*math.tau*3)*math.tau*3+.15*np.cos(x*math.tau*7)*math.tau*7
    gradient = (np.cos(phase)+.44*np.cos(phase*2))*.0025/manifest['tileMeters']
    vector = normal*2-1
    slope = vector[:,:,:2]/np.maximum(vector[:,:,2:3],.1)
    slope[:,:,0] -= gradient*dx
    # PNG rows run downward; GL tangent +Y points upward.
    slope[:,:,1] += gradient*math.tau*12
    vector = np.concatenate([slope,np.ones((n,n,1))],axis=2)
    vector /= np.linalg.norm(vector,axis=2,keepdims=True)
    save('RippleSand_Color.png',color)
    save('RippleSand_NormalGL.png',vector*.5+.5)
    mask[:,:,2] = np.clip(height*.8+.1+ripple*.06,0,1)
    save('RippleSand_Mask.png',mask)
    entries=[]
    for p in sorted((CAT/'Textures').glob('*Sand_*.png')):
        entries.append({'path':p.relative_to(CAT.parents[2]).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    (OUT/'sand08-catalog-additions.json').write_text(json.dumps({'schemaVersion':1,'files':entries},indent=2)+'\n')
    print('SAND_08_BUILD_COMPLETE',json.dumps({'files':len(entries),'catalogBytes':sum(e['bytes'] for e in entries)}))

if __name__ == '__main__': build()
