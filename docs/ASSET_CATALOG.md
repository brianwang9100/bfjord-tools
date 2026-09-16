# Reviewed vegetation, road and Terrain asset catalog

This bounded snapshot admits each selected Unity asset and its import metadata through the exact file list in `manifest.json`. It includes no buildings, facade modules, island scenes or private application state.

- Four ready Unity prefabs: fern02_b, shrub03_a, pine_sapling_small_b and rock_moss_set01_rock01.
- Fifteen prepared model variants: fern02 a–d, shrub03 a–d, six rock variants and pine sapling b. These have reviewed source preparation and model data. The foliage build command creates the shared placement prefabs and LOD representations.
- The requested pine_sapling_small_a is not present in the reviewed prepared input and is not fabricated or substituted.
- Road materials Asphalt, Gravel, Ground, Dirt, Marking, AsphaltEdge, GravelVerge and DirtVerge, their exact texture dependencies, and the original SurfaceBlend shader plus its three local HLSL includes required by the edge/verge materials.

The foliage catalog includes the admitted mature fir, four original 0.4 botanical families (rose thicket, meadow daisy, wood sorrel and coastal grass), and four original 0.5 woodland assets (mature oak, silver birch, hollow fallen log and tall meadow grass). The prepared library contains 24 variants in total. Each original woodland FBX has three LODs and uses a separate original color/normal/metallic-smoothness atlas; the botanical inputs stay unchanged.

The 0.5 source catalog contains 248 admitted files: 214 retained entries and 34 additions, including Unity metadata. Four retained scan-derived masks plus three additional color/normal/mask sets support seven Terrain surfaces in four palettes, always four layers at a time. Meadow / Leafy Grass uses a 2 m tile, Soil / Park Dirt 3 m, Talus / Gravel Floor 03 2.5 m, and Outcrop / Rocky Terrain 90 m. ForestLitter / Forest Floor adds 2.14 m, CoastalShingle / Coast Sand Rocks 02 adds 15 m, and RockFace / Rock Boulder Dry adds 1.8 m. The detail pack's `sources.json` and `surface-bank-sources.json` retain source identities, physical dimensions and hashes. [NATURE_05.md](fidelity/NATURE_05.md) records the exact palette composition and current integration state.

All vegetation and scanned material artwork is CC0. The original SurfaceBlend shader is MIT under the repository's code license. Supplier author credits, exact source URLs/hashes and scoped model/material preparation are preserved in `credits/`. The only unresolved asset GUIDs are the URP package's Lit shader and Editor asset-version component, supplied by the declared Unity dependency.

The `manifest.json` lists relative Unity destination paths, exact byte hashes, ready prefab paths, prepared model paths and the missing requested variant. Public FBX metadata is sanitized using the structured process described in `docs/EXPORT_PROVENANCE.json`; geometry arrays are preserved. Importers must verify hashes and refuse conflicting existing asset bytes/GUIDs before restoring the snapshot. This is a finite art catalog, not an entire copied Assets directory or a runtime asset streaming system.
