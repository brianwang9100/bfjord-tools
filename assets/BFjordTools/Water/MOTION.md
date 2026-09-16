# River, ocean and waterfall motion

This extension adds three original 512² linear RGB data maps. They are **CC0-1.0**; the generator, shader and Editor code are MIT. `motion-manifest.json` records their exact hashes and channel encodings. No RAM assets, source, textures or proprietary material settings are included. Python is appropriate for these periodic mathematical fields; Blender remains the source of the original cliff and river stones, while Unity performs the live water animation.

## Reference and reuse check — September 15, 2026

- [NatureManufacture R.A.M 3](https://naturemanufacture.com/river-auto-material-3/) describes flow-map direction/speed, vertex-painted slow water, cascades and foam, and waterfall effects. Those visible distinctions are the art reference. This is a paid proprietary product, not a copied dependency or a claim of matching all its features.
- [Unity's production-ready water sample documentation](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Production-Ready-Water.md) documents large/small scrolling lake normals, river flow mapping, foam and waterfall end fades. Unity's maintained Graphics repository is a useful primary implementation reference; importing its graph dependencies is unnecessary for this existing connected HLSL surface. No sample code or assets are redistributed.
- [ChiliMilk URP_Water](https://github.com/ChiliMilk/URP_Water) is MIT and targets Unity 2020.3.1+. Its stated version does not establish current Unity 6000.6 / URP 17.6 or iPad support. Adapting an older separate water renderer would duplicate the admitted connected surface, so the existing original shader remains the chosen implementation.

These are visual shaders, not fluids: no hydraulics, spray-particle simulation, screen refraction, caustic simulation or GPU fluid buffers. Current device performance remains unverified.

## What moves

`river-motion.png` supplies elongated foam threads and crossing ripple variation. The connected shader rotates their coordinates into the mesh's canonical downstream flow, advects in two overlapping phases and fades the effect across lake/ocean masks. The lake keeps its quieter existing profile. `_RiverCurrentStrength` controls the extra current foam independently of painted shore foam.

`ocean-motion.png` supplies broad, broken crest patches. Ocean normals use a larger scale and coherent wind drift; crest foam combines the original analytic wave height with the moving map. `_OceanSurfaceStrength` controls this layer. These material properties default to 0.22 and 0.32 and can be tuned on the generated material; they are not version-one recipe fields. Appearance refresh restores those shader defaults.

`waterfall-motion.png` supplies falling foam filaments, sheet coverage and breakup. The waterfall shader scrolls two different scales down the finite ribbon, preserves clear lanes between whitewater filaments, fades its sides and ends, and animates expanding, broken foam and downstream eddies around the actual toe. A depth fade joins opaque ledges and stones when the configured cameras supply depth. The finite sheet has anchored, sub-metre channel relief and irregular bank width, with no animated CPU geometry. All animation uses shader time. No per-frame CPU mesh upload or Editor component is required. Both shaders preserve URP's transparent-surface definition. Connected water preserves opaque deep water fallback 1.

The waterfall's receiving-wave parameters use the exact same phase, directions and normalized amplitudes as the connected ocean. The coastal fixture has toe elevation 0.83 m over ocean base 0.8 m and matching 0.45 m height / 28 m wavelength / 0.65 speed. Plunge foam and the bottom of the falling ribbon follow that wave field; mesh bounds include the maximum displacement. For an artist's calm receiving pool set `plungeWaveHeight` to zero. Match receiving elevation and wave settings explicitly if the surrounding water changes. This is an authored seam, not automatic fluid coupling.

## Bounded waterfall fixture

`Samples/waterfall.json` defines a 7 m wide, roughly 17 m high coastal fall: lip `(90,18,410)`, toe `(90,0.83,418)`. A 1.6-metre feeder runs backward from the lip. For the rock-backed fixture its vertices project onto the cap’s actual LOD0 triangles before publication; the varying lip height blends into the fall. With `rockBackdrop=true`, the fixture includes the original `coastal_outcrop` mass and `river_ledge` feeder cap, crag shoulders, and wet fractured clusters/river stones. Their variant-aware material bindings retain the new baked detail. Previous rounded/legacy backdrop receipts remain readable and removable. They are prefab instances of the separately admitted immutable rock library, all under `Waterfall Sample`. The two water meshes and two materials are generation-owned assets; shared rock/texture assets are retained on removal. `rockBackdrop=false` permits artist-authored existing cliffs.

The command requires the already configured standalone 512 m sandbox and Edit Mode, validates finite geometry and source maps before scene mutation, and never carves Terrain. Prepare is read-only. `rockBackdrop=true` requires the rock library built first, using the configured `rockSourceRoot`; it does not silently import it while preparing. Apply replaces one owned fixture using staged objects, a receipt and rollback; failed cleanup retains the receipt for retry. No runtime island, catalog, native build or export is involved.

Request format (use the standard `AuthoringBatch` request runner):

```json
{"schemaVersion":1,"command":"bwork_rocks","arguments":{"action":"build-assets"}}
```

```json
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"prepare"}}
```

```json
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"apply"}}
```

```json
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"view","capture":"waterfall-t0","timeSeconds":"0"}}
```

```json
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"capture","capture":"waterfall-t075","timeSeconds":"0.75"}}
```

`view` frames the installed waterfall. `capture` leaves the camera fixed and temporarily sets `_AnimationTime` via renderer property blocks for **all** animated connected-water/waterfall surfaces, restoring those blocks even when capture fails. Time is a string to fit the current batch transport and must parse as a finite invariant-culture number from 0–600 seconds. Empty time uses live shader time. Capture can be used after `bwork_view` river/lake/sea views without an installed waterfall, so it also verifies the existing connected surface.

Run `status` to inspect the fixture; `remove` deletes only its owned scene root and generation assets. Removal does not depend on external source files remaining available. Applying changed maps/shaders requires `bwork_water_connected action=refresh-appearance` and `bwork_waterfall action=apply`; this updates the source receipt instead of mislabelling the old generation.

## Evidence and limitations

The reference-informed fidelity revision updates sheet geometry, two maps and both shaders. Its current evidence is generator periodicity checks, reviewed map output and exact manifest verification. The Unity results below describe **0.3.0 before this revision**; new assembled-scene visual and Editor validation belongs to the coordinator's milestone. See `docs/fidelity/WATER.md` in the working source repository for scope and verification status.


The mathematical generator verifies periodic tile values before writing. All three delivered PNG hashes and dimensions were checked against `motion-manifest.json`; current source payload is 1,934,214 bytes, distinct from imported mip/compression residency. Package importers request linear, mipmapped, repeating data and iPhone ASTC 6×6. No new external image library is required.

The assembled Unity run completed with zero shader errors, waterfall prepare/replace/remove/reapply, foreign-child protection and unchanged Terrain heights. Three fixed cameras each captured 24 animation frames and stills at 0 / 0.75 / 1.5 seconds; measured water regions changed at each comparison. The final waterfall contract suite passed 5/5 after the full extension suite passed 63/63. See the public `docs/VALIDATION.md` extension ledger for exact evidence. This remains visual water without physical currents, particles or device performance acceptance.

Regenerate with:

```sh
python3 art/BFjordTools/Water/generate_motion_maps.py --output Unity/WorldAuthoringTools/Textures/Water
```

## Fidelity 0.5

The v0.5 original generator shortens river patches and gives the ocean dense aerated film with round pores. The waterfall map is unchanged. Low/medium/high connected-water presets share these maps. A deterministic material seed translates their periodic pattern origin; it does not rewrite source maps or use Unity global randomness. See `docs/fidelity/WATER_05.md` for directional mapping, foam controls, capture commands and remaining validation.
