#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Verify one decode/treatment/encode, matched channels and source/catalog identity."""
import json, hashlib
import numpy as np
from PIL import Image
from build_sand import ART, OUT, CAT, source_arrays, ivory_color, decode_srgb, tangent_normal

manifest, source = source_arrays()
expected = np.rint(ivory_color(source['diff'],manifest['derivation'])*255).astype(np.uint8)
report = {'source':manifest['id'], 'checks':[], 'materialStatistics':{}}
for name in ('BeachSand','RippleSand'):
    for folder in (OUT/'Textures',CAT/'Textures'):
        encoded = np.asarray(Image.open(folder/(name+'_Color.png')).convert('RGB'))
        assert encoded.shape == (2048,2048,3)
        assert np.array_equal(expected,encoded), 'Incorrect sRGB treatment or duplicate decoding'
    for role in ('Color','NormalGL','Mask'):
        p=name+'_'+role+'.png'
        assert (OUT/'Textures'/p).read_bytes() == (CAT/'Textures'/p).read_bytes(), p
    normal = np.asarray(Image.open(OUT/'Textures'/(name+'_NormalGL.png')),dtype=np.float32)/255*2-1
    assert np.max(np.abs(np.linalg.norm(normal,axis=2)-1)) < .018, 'Invalid tangent normal'
    mask = np.asarray(Image.open(OUT/'Textures'/(name+'_Mask.png')))
    assert np.all(mask[:,:,0]==0), 'Sand must be nonmetallic'
    assert np.max(np.abs(mask[:,:,1].astype(float)-source['ao']*255)) <= .501
    assert np.max(np.abs(mask[:,:,3].astype(float)-(1-source['rough'])*255)) <= .501
    if name == 'BeachSand':
        assert np.array_equal(np.asarray(Image.open(OUT/'Textures'/(name+'_NormalGL.png'))),np.rint(tangent_normal(source['nor_gl'])*255).astype(np.uint8))
        assert np.max(np.abs(mask[:,:,2].astype(float)-source['disp']*255)) <= .501
    report['checks'].append(name+': exact source treatment, unit normals, nonmetallic matched channels, catalog identity')
linear = decode_srgb(expected.astype(np.float32)/255)
assert not np.any(expected==255), 'Clipped white albedo'
assert np.all((linear.mean(axis=(0,1),dtype=np.float64) > .5)&(linear.mean(axis=(0,1),dtype=np.float64) < .75))
report['materialStatistics']={'meanEncodedRGB':expected.mean(axis=(0,1)).tolist(),'meanLinearRGB':linear.mean(axis=(0,1),dtype=np.float64).tolist(),'maxEncodedRGB':expected.max(axis=(0,1)).tolist(),'clippedChannels':int(np.sum(expected==255)),'tileMeters':manifest['tileMeters'],'resolution':2048,'rawSourceMeanEncodedRGB':(source['diff'].astype(np.float64)*255).mean(axis=(0,1)).tolist()}
report['limits']='Offline map verification only; Unity lighting/compression and device acceptance remain separate.'
(OUT/'Review/sand08-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
