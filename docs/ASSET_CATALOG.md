# Reviewed vegetation, road and Terrain asset catalog

This bounded snapshot admits each selected Unity asset and its import metadata through the exact file list in `manifest.json`. It includes no buildings, facade modules, island scenes or private application state.

The **0.7 catalog has 342 files and the prepared foliage library has 42 variants**. Six shore props, seven forest-floor specimens and three tree families add 82 exact dependency files to 0.6. The shoreline palette adds a scanned Sand 02 surface at 2.1 m, shared by dry and wet layers. All sixteen new models have three LOD meshes. [0.7 integration and limitations](fidelity/NATURE_07.md).

- Four ready Unity prefabs: fern02_b, shrub03_a, pine_sapling_small_b and rock_moss_set01_rock01.
- Fifteen prepared model variants: fern02 a–d, shrub03 a–d, six rock variants and pine sapling b. These have reviewed source preparation and model data. The foliage build command creates the shared placement prefabs and LOD representations.
- The requested pine_sapling_small_a is not present in the reviewed prepared input and is not fabricated or substituted.
- Road materials Asphalt, Gravel, Ground, Dirt, Marking, AsphaltEdge, GravelVerge and DirtVerge, their exact texture dependencies, and the original SurfaceBlend shader plus its three local HLSL includes required by the edge/verge materials.

The foliage catalog includes the admitted mature fir, four original 0.4 botanical families (rose thicket, meadow daisy, wood sorrel and coastal grass), and four original 0.5 woodland assets (mature oak, silver birch, hollow fallen log and tall meadow grass). The historical 0.6 prepared library contains **26 variants**: the prior 24 plus `MatureOak_B` and `SilverBirch_B`. Each original woodland FBX has three LODs and uses a separate original color/normal/metallic-smoothness atlas; the botanical inputs stay unchanged.

The historical 0.6 source catalog contains **260 admitted files**: the historical 248 plus 12 additive woodland files, including Unity metadata. Those additions are two B-model FBXs, the three Woodland06 maps and the family manifest, each with its `.meta`. Original A models and their hashes are preserved.

The historical 0.5 source catalog contained exactly **248 files**: 214 retained entries and 34 additions, including Unity metadata. Its prepared library had 24 variants; those prior counts are not v0.6 validation totals. Four retained scan-derived masks plus three additional color/normal/mask sets support seven Terrain surfaces in four palettes, always four layers at a time. Meadow / Leafy Grass uses a 2 m tile, Soil / Park Dirt 3 m, Talus / Gravel Floor 03 2.5 m, and Outcrop / Rocky Terrain 90 m. ForestLitter / Forest Floor adds 2.14 m, CoastalShingle / Coast Sand Rocks 02 adds 15 m, and RockFace / Rock Boulder Dry adds 1.8 m. The detail pack's `sources.json` and `surface-bank-sources.json` retain source identities, physical dimensions and hashes. [NATURE_05.md](fidelity/NATURE_05.md) records the original palette composition and historical 0.5 results; [NATURE_06.md](fidelity/NATURE_06.md) records that integration status.

Terrain appearance defaults to one-repeat stochastic cells (`stochasticCellTiles=1.0`) with `macroVariation=0.28` over `macroScaleMeters=37`. This changes sampling of the existing scans; it adds no texture catalog entries and preserves their physical scales. See [the terrain controls and cost](fidelity/NATURE_06.md) for the native comparison switch and limits.

The B tree meshes and leaves are original CC0 geometry. Their shared 2048² color/normal/metallic-smoothness atlas includes the verified CC0 Poly Haven [Bark Brown 02](https://polyhaven.com/a/bark_brown_02) scan by Rob Tuytel for oak bark. Birch bark and leaf fields are original procedural art. `assets/BFjordTools/FoliageDetail/Woodland06/Sources/sources.json` records the three public 1K source maps, URLs and exact hashes; `Assets/BFjord/OriginalFoliage/woodland06-manifest.json` records the delivered family. The source models have three LODs, and their unbound export material slots avoid absolute texture URLs. Unity assigns the atlas when building a new immutable library generation. Powered by Poly Haven.

## Use the 0.6 woodland sample

Build the library, then apply the new sample after the terrain, roads and water are in place:

```sh
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/foliage-assets.json
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/woodland-canopy-06.json
```

The wrapper request applies `Packages/com.bfjord.tools/Samples/woodland-canopy-06.json` to the named `canopy` batch. Reusing that ID deliberately replaces its prior placements. Choose a new batch ID to retain the old batch as a neighbor; shared woody spacing may then reduce new placements.

The sample uses seed **51915**, a cap of **44** candidates, five 22 m clusters and a 6.5 m minimum spacing over x182–250/z158–228. Species weights are oak B 40%, birch B 38% and retained mature fir 22%; slopes are limited to 30°. Crown radii are 5.3 m, 3.8 m and 3 m before scale. Clearance, terrain, support fitting and neighbors can reduce the accepted count. Recipe caps are not observed placement counts.

All vegetation and scanned material artwork is CC0. The original SurfaceBlend shader is MIT under the repository's code license. Supplier author credits, exact source URLs/hashes and scoped model/material preparation are preserved in `credits/`. The only unresolved asset GUIDs are the URP package's Lit shader and Editor asset-version component, supplied by the declared Unity dependency.

The `manifest.json` lists relative Unity destination paths, exact byte hashes, ready prefab paths, prepared model paths and the missing requested variant. Public FBX metadata is sanitized using the structured process described in `docs/EXPORT_PROVENANCE.json`; geometry arrays are preserved. Importers must verify hashes and refuse conflicting existing asset bytes/GUIDs before restoring the snapshot. This is a finite art catalog, not an entire copied Assets directory or a runtime asset streaming system.
