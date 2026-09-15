# Original rock authoring

`bwork_rocks` imports the original Blender rock library and places ordinary Unity prefab instances, LODGroups and optional static mesh colliders. Six families cover granite boulders, rounded river stones, stratified outcrops, cliff slabs, scree clusters and upright crags. Blender source and CC0 provenance ship separately under the configured `rockSourceRoot`; the Editor package contains MIT source. No NatureManufacture assets or code are included.

Configure `rockSourceRoot` in `ProjectSettings/BfjordTools.json` and open the configured 512m sandbox. Paths are explicit and project-relative in configuration. Run `build-assets` once, then `prepare` and `apply` with an explicit batch ID. `prepare` admits source, recipe, terrain and exclusions without changing the scene; it does not need a built library. `status`, `remove`, `catalog` and `view` inspect/remove the selected batch, describe available assets or frame its renderers in the current Scene view.

```json
{"schemaVersion":1,"command":"bwork_rocks","arguments":{"action":"apply","batchId":"highland","recipePath":"Packages/com.bfjord.tools/Samples/rocks-highland.json"}}
```

Use the installed package's actual name in recipe paths. Omitting `recipePath` selects `rocks-highland.json`. `rocks-river-bank.json` and `rocks-river-shallow.json` are connected-water presets. The public river scene workflow prepares assets, removes its earlier owned bank/shallow batches, applies water, then applies both rock presets. Each command saves its own transaction; the complete workflow is staged, not atomic. Direct connected-water replacement is guarded while these owned river batches exist so an old bank arrangement cannot silently outlive a river change.

## Controls

- `seed`, `id`, area `{x,z,width,depth}`, weighted species and species scale ranges define placement. `maximumCount` caps at 2,000; density is per hectare. Uniform, `small-biased` and `large-biased` size distributions use bounded scale ranges. Cluster count/radius share the established deterministic scatter planner.
- `materialProfile`: granite, sandstone, basalt, mossy or wet. These are shared PBR texture/tint/roughness profiles; changing one never changes placement random values. Mossy is a color treatment, not a terrain cover or moss-growth shader. Wet uses darker color, reduced normal strength and 0.58 smoothness. Each prefab has three meshes and a native LODGroup; materials are shared and instancing enabled.
- `orientation`: random, strata or flow. Strata has a common yaw plus bounded variation; flow derives the local canonical river direction for a stone's long X axis. `alignment` blends upright and the Terrain normal. `burialFraction` embeds a fraction of rock height. A sampled base-plane contact pass seats the rock; this is an authoring approximation requiring representative visual review on rough terrain.
- `maximumSlopeDegrees`, height range and `maximumGroundRelief` reject unsuitable ground. Terrain holes and unsupported footprint samples are rejected. Full rotation envelopes provide conservative nonoverlap and exclusion spacing. Existing other rock batches also block new placements. A selected batch's previous instances are excluded from these neighbor checks so replacement remains repeatable.
- `exclusionPadding` adds clearance to generated roads, water and structures indexed by `SpatialExclusions`. Recipe `exclusions` adds circle, capsule or axis-aligned box footprints. Arbitrary scene geometry is not inferred as an obstacle: declare explicit primitives for additional protected objects.
- `waterMode=bank` accepts dry river margins within `maximumWaterDistance` and `maximumBankHeight`; `shallow` accepts a canonical wet river footprint only within the measured Terrain-to-water depth range. Both omit ordinary water mesh rejection while preserving road/structure exclusions, and reject lake/ocean areas. Flow is authored water-field direction, not a fluid simulation. A shallow preset's bank-aware depth range allows partially submerged stones, but does not manufacture water interaction or foam around stones.
- `colliders` defaults false. Opting in retains the separate low-poly static collider per rock. No rigidbodies or runtime placement system is installed.

## Ownership and source contract

`manifest.json` schema 1 declares CC0-1.0, up to 32 families, three decreasing LOD triangle counts (at most 20,000 each), screen-relative heights, positive meter dimensions and collider FBX paths. The one shared material defines color, OpenGL normal and Unity packed metallic/smoothness maps. FBX origin is centered X/Z with bottom Y=0. The importer verifies measured triangle counts, bounds, normals and UVs, and bounds colliders to 1,000 triangles. Sources are bounded to 64 MiB; paths reject traversal and symlinks before writes.

Library generations live under the configured sandbox generated root at `RockLibrary/<SHA256>/`; every admitted source byte participates in that hash. Existing batches keep immutable prior generations. Rebuilding never edits a shared asset in place. Incomplete generation folders are not silently reused. Library generations are retained after scene removal so other batches keep valid references.

Scene roots and per-batch receipts have matching ownership IDs. A new inactive root is built before replacing the prior root. The prior root stays in a preview scene until receipt and scene save succeed. Failed publication restores it and the prior metadata; rollback failures remain explicit for recovery. Batch removal uses the same save boundary. The tool never changes native routes, workouts or terrain height samples.

## Reuse and limits

The implementation reuses the suite's deterministic `FjordBulkScatter`, `SpatialExclusions`, validated texture importer and owned-batch publication approach. Unity's maintained [LODGroup](https://docs.unity3d.com/6000.0/Documentation/Manual/class-LODGroup.html) and URP Lit supply rendering. Source reference research is in the public suite's `docs/ROCK_REFERENCES.md`. No paid plugin is required. Three low-poly LODs, shared compressed textures and optional colliders are intended to bound mobile cost; physical iPad performance and final authored-world appearance remain unverified until device and scene review.
