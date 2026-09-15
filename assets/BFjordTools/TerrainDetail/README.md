# Terrain detail masks

Four 512 × 512 linear RGBA masks supplement the existing Poly Haven ground scans. Output lives at `art/BFjordTools/asset-catalog/Assets/BFjord/TerrainDetail`. `catalog-additions.json` supplies entries for the coordinator to merge into the shared catalog at integration.

The channels are **R metallic (zero), G occlusion, B scanned displacement, A one minus roughness**. ARM input is unpacked as occlusion/roughness/metallic; it is never passed directly to Terrain. These masks retain the corresponding surface UVs and its 2m/3m/2.5m/90m physical repeat. They change material blending and shading; they do not displace terrain geometry. Native TerrainLayer remapping keeps smoothness restrained at 45% of the scan value.

All input/output texture content is [Poly Haven CC0](https://polyhaven.com/license). The existing scan IDs are leafy_grass, park_dirt, gravel_floor_03 and rocky_terrain. `sources.json` pins the public source URLs, provider byte lengths/MD5 and downloaded SHA-256. No reference-gallery pixels enter these outputs. The packing script is MIT.

Rebuild with Python 3 and Pillow:

```sh
python3 Scripts/world_assets/terrain_detail/build_masks.py --cache /tmp/bfjord-terrain-source --fetch
```

Omit `--fetch` for an offline rebuild from the same cache. A source hash mismatch stops generation. The script emits deterministic PNGs/import metadata, channel extrema, file hashes and catalog additions; it never opens Unity or rewrites the shared catalog manifest. Input JPEG maps are reduced to 512 pixels to limit runtime texture memory. The four RGBA masks would occupy about 5.33 MiB with mipmaps if uncompressed; actual platform compression and process costs require profiling.

Unity 6000.6 / URP Terrain Lit supports these four layers in its existing height-blend path. See [Unity's Terrain Lit reference](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shader-terrain-lit.html). This avoids a custom terrain shader or another runtime package. iPad runtime performance remains unmeasured.
