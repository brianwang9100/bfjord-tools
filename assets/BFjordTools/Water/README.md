# Original connected-water materials

These are original deterministic water data textures, with no third-party image inputs. The three generated PNGs are dedicated under **CC0-1.0**; the generator and water code use the repository's MIT license. `manifest.json` records dimensions, fixed seeds, channel meaning and SHA-256 for each distributed PNG. The canonical PNGs and Unity import metadata live inside the package at `Textures/Water`; no external asset service is required.

Regenerate using Python 3's standard library:

```sh
python3 generate_water_maps.py --output /path/to/Packages/com.bfjord.tools/Textures/Water
```

The generator checks its normal-field analytic derivatives against central differences and checks periodic values and derivatives before writing. Integer Fourier modes produce two seamless, differently seeded normal maps. Periodic jittered cellular edges produce broken foam filaments. These are original authored mathematical data maps, not scanned water or a fluid simulation. Their zipped PNG source payload is approximately 1.38 MiB. Two 512² maps and one 256² map have mipmaps, linear RGB sampling and a 512 texture limit. iPhone import requests ASTC 6×6; source bytes, imported residency and actual device performance are different measurements.

## Shader and recipe controls

The connected-water recipe keeps its version-one graph and geometry controls. New visual fields are optional; `FromJsonOverwrite` preserves explicit defaults for older recipes. `Samples/connected-water.json` supplies the complete example.

| Controls | Meaning |
| --- | --- |
| `riverWaveHeight/Length/Speed`, `lakeWaveHeight/Length/Speed`, `oceanWaveHeight/Length/Speed` | Independent bounded broad-wave profiles. Heights are metres, length is metres, speed is phase radians/second. Lake motion blends inward across 12 m; ocean motion across 24 m. |
| `flowSpeed`, reach `flowSpeed` | Global multiplier and per-reach advection speed. Reach direction follows validated downstream geometry; overlapping flows blend by the existing wet-field weights. |
| `normalStrength`, `rippleTileSize`, `detailTileSize`, `detailStrength` | Original ripple maps, physical repetition lengths and fine-detail weight. Defaults remain restrained; the sample uses normal strength 0.18. |
| `shallowColor`, `deepColor`, `oceanShallowColor`, `oceanDeepColor` | Independent river/lake and ocean colors, each serialized as `r/g/b/a` values in [0,1]. |
| `smoothness`, `oceanSmoothness`, `depthColorDistance` | Specular response and absorption distance. No metallic water approximation. |
| `shallowOpacity`, `deepOpacity`, `shoreFadeDepth` | Transparent shallow bed and depth color. Alpha fades to zero at zero depth, including foam. |
| `foamStrength`, `foamWidth`, `foamTileSize`, `foamCutoff`, `crestFoamStrength` | Broken shore foam, physical pattern size, pattern cutoff, and separate restrained ocean crest foam. Existing knot `foam` now reaches the connected mesh instead of being discarded. |

A current URP camera depth texture is required and validated. The shader uses ordinary alpha blending and does not require an opaque-color copy, refraction, planar reflections, runtime tessellation or compute simulation. It declares URP reflection-probe blending and box projection variants; useful local reflections still require a probe authored by the scene. Ocean remains a finite rectangular body.

Broad wave height and its analytical x/z derivatives use the same three normalized-direction components. River/lake/ocean profiles mix by bounded weights; smooth baked derivatives include the base water profile, squared bank envelope and body mixing. Pixel normals use those derivatives instead of per-triangle face normals. Shader interpolation is a smooth shading approximation to the finite mesh; coarse mesh silhouettes remain finite geometry. Mesh bounds expand vertically by twice the maximum of the three displacement heights. Texture detail fades with distance and ordinary mip filtering.

## Bank and planting seam

`ConnectedWaterCommand.ActiveField()` reconstructs the installed recipe once per authoring operation, returning null if absent, removed or from a legacy receipt without recipe JSON. Call `field.SampleForBank(new Vector2(worldX, worldZ), maximumDistance)` for bank/planting queries (range 0–200 m). This searches neighboring buckets, deduplicates reach segments, and returns positive infinity in `Distance` when no water is within range. `field.Sample(...)` remains the fast local query for wet mesh/carving and is not a wide-bank proximity query. Both return `Distance`, `Height`, `BedDepth`, `Flow`, `Ocean`, `Lake` and `Foam`. `Distance` is negative inside the union; elliptical body distance is boundary-exact but is not a Euclidean SDF. `field.Bounds` and `field.Recipe.bankFalloff` are available.

Wet bank/planting masks should use outside distance and world height relative to `Height`; do not classify a high bridge deck as wet riverbed. These are artistic moisture proxies. Existing water exclusions remain authoritative. Water apply/remove repaint through `TerrainPresentation` and capture terrain layers/alphamaps for rollback before publication; the persisted height restoration receipt retains the existing terrain ownership checks. Shared package maps are not generation-owned assets and are never deleted by remove.

## Reference check and integration review

Free alternatives were checked on September 15, 2026. [Unity's Shader Graph 17.6 production samples](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.6/manual/Shader-Graph-Sample-Production-Ready-Detail.html) include water/flow approaches, but importing their sample graphs and dependencies was unnecessary for the existing welded union. [Unity Boat Attack water's package](https://github.com/Unity-Technologies/boat-attack-water/blob/main/package.json) still declares Unity 2021.3 / URP 12.1.10, with a [Unity Companion License](https://github.com/Unity-Technologies/boat-attack-water/blob/main/LICENSE.md). No code or art was copied from either. The existing original URP shader plus original maps avoids imposing those dependency/license assumptions; exact Unity 6000.6 / URP 17.6 rendering is an assembled milestone check, not established by research or SDK compilation. Physical iPad performance remains unverified.

Use one assembled validation: low bridge-side view toward the sun to expose triangulated highlights; downstream meander view for flow; lake margin view for calm motion and transparent bed; estuary view for smooth river/ocean mixing and finite bounds. Keep the same exposure, sky and camera when comparing. Inspect a few seconds of animation, not only a still. Verify apply/replace/remove restoration and missing-map failure before publishing; no Editor run is performed by this asset-generation script.
