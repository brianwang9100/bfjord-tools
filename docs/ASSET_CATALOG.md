# Reviewed vegetation and road asset catalog

This bounded snapshot contains 63 selected Unity assets with their 63 `.meta` files. It includes no buildings, facade modules, island scenes or private application state.

- Four ready Unity prefabs: fern02_b, shrub03_a, pine_sapling_small_b and rock_moss_set01_rock01.
- Fifteen prepared model variants: fern02 a–d, shrub03 a–d, six rock variants and pine sapling b. These have reviewed source preparation and model data; the additional variants do not yet have ready placement prefabs.
- The requested pine_sapling_small_a is not present in the reviewed prepared input and is not fabricated or substituted.
- Road materials Asphalt, Gravel, Ground, Dirt, Marking, AsphaltEdge, GravelVerge and DirtVerge, their exact texture dependencies, and the original SurfaceBlend shader plus its three local HLSL includes required by the edge/verge materials.

All vegetation and scanned material artwork is CC0. The original SurfaceBlend shader is MIT under the repository's code license. Supplier author credits, exact source URLs/hashes and scoped model/material preparation are preserved in `credits/`. The only unresolved asset GUIDs are the URP package's Lit shader and Editor asset-version component, supplied by the declared Unity dependency.

The `manifest.json` lists relative Unity destination paths, exact byte hashes, ready prefab paths, prepared model paths and the missing requested variant. Public FBX metadata is sanitized using the structured process described in `docs/EXPORT_PROVENANCE.json`; geometry arrays are preserved. Importers must verify hashes and refuse conflicting existing asset bytes/GUIDs before restoring the snapshot. This is a finite art catalog, not an entire copied Assets directory or a runtime asset streaming system.
