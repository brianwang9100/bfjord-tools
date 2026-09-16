#!/usr/bin/env python3
"""Focused original-map regressions; standard library only. SPDX-License-Identifier: MIT."""
import json
import math
import statistics
import unittest
from generate_motion_maps import field


class MotionMapTests(unittest.TestCase):
    def test_each_axis_wraps_and_channels_are_finite(self):
        for kind in ('river', 'ocean', 'waterfall'):
            for i in range(41):
                u, v = i*.137-2, i*.219-3
                current = field(u,v,kind)
                self.assertTrue(all(math.isfinite(x) and 0 <= x <= 1 for x in current))
                for wrapped in (field(u+1,v,kind),field(u,v+1,kind)):
                    self.assertLess(max(abs(a-b) for a,b in zip(current,wrapped)),1e-10)

    def test_river_is_sparse_and_long_in_downstream_metres(self):
        n = 96
        data = [[field((x+.5)/n,(y+.5)/n,'river')[0] for x in range(n)] for y in range(n)]
        across = statistics.mean(abs(data[y][(x+1)%n]-data[y][x]) for y in range(n) for x in range(n))
        along = statistics.mean(abs(data[(y+1)%n][x]-data[y][x]) for y in range(n) for x in range(n))
        # Mapping is 8m across and 24m downstream. Isotropic blobs or a transposed
        # texture fail the directional correlation test even if the map still tiles.
        metric_ratio = across/along*3
        coverage = statistics.mean(x>.2 for row in data for x in row)
        self.assertGreater(metric_ratio,8)
        self.assertGreater(coverage,.05)
        self.assertLess(coverage,.16)
        # Rafts must taper, not span the full tile as uninterrupted painted lanes.
        self.assertTrue(all(min(row[x] for row in data)<.01 for x in range(n)))
        print(json.dumps({'riverMetricGradientRatio':metric_ratio,'riverBrightCoverage':coverage}))

    def test_ocean_has_multiscale_porous_coverage(self):
        n=96
        data=[[field((x+.5)/n,(y+.5)/n,'ocean')[0] for x in range(n)] for y in range(n)]
        values=[v for row in data for v in row]
        blocks=[statistics.mean(data[y+j][x+i] for j in range(12) for i in range(12))
                for y in range(0,n,12) for x in range(0,n,12)]
        coverage=statistics.mean(v>.2 for v in values)
        self.assertGreater(coverage,.25)
        self.assertLess(coverage,.4)
        self.assertGreater(statistics.pstdev(blocks),.04)
        self.assertGreater(statistics.mean(v<.015 for v in values),.3)
        self.assertGreater(statistics.mean(v>.6 for v in values),.015)
        print(json.dumps({'oceanBrightCoverage':coverage,'oceanPatchVariation':statistics.pstdev(blocks)}))


if __name__ == '__main__':
    unittest.main()
