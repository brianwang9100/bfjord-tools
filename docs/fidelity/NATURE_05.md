# Nature detail 0.5.0

The 0.5.0 addition extends the existing authoring tools with mixed mature trees, fallen wood, taller grass, road markings, river-flow presets, beach surf and a seven-surface Terrain bank. The implementation and visual review are complete; the source is published in bfjord-tools. The integration snapshot passed 133/133 Unity Editor tests; the separate CLI suite passed 8/8. Reviewed stills and three motion clips are linked below. The labeled nine-rock comparison passed. The final assembled-road and water-refresh lifecycle checks passed. Version 0.5.0 is available in the public repository.

The [0.4.0 validation record](VALIDATION.md) remains historical evidence for that revision. The current [machine-readable 0.5 state](evidence-05.json) records exact results and the completed review gates.

## Recorded assembly

The current assembled sandbox contains **19 mixed-canopy placements: 6 original oak, 3 original birch and 10 retained mature fir**, plus **17 hollow fallen logs** and **174 mixed-grass placements**. The pass retained all **66,049** sampled Terrain heights. These are actual results after terrain, spacing and exclusion checks, not recipe target counts or a claim about another scene.

The source catalog admits **248 files**, comprising 214 retained entries and 34 new entries. The additions are four woodland FBXs, three woodland textures and their family record/metadata, plus nine Terrain textures and their metadata. Public FBX export sanitizes structured private-path strings while preserving numeric and array geometry properties; 81 FBXs are covered by the current export inventory. Final public-output hashes belong to the exported manifests and provenance record.

## Trees, deadwood and grass

`MatureOak_A` adds a broad, low branching crown; `SilverBirch_A` adds a pale trunk and a taller, narrower silhouette. `FallenHollowLog_A` has a bent shell, open inner wall and broken rims. `TallMeadowGrass_A` adds folded blades and seed heads with stronger visible wind. All four are original Blender work, CC0 artwork with MIT generators, rather than downloaded commercial vegetation. The original four rose/daisy/sorrel/coastal-grass assets remain unchanged.

Each model has three authored LODs and one original woodland PBR atlas material. Blender export/reimport checks passed for all twelve meshes, including finite geometry, nondegenerate triangles, UVs and tangents. Generated library revision 3 retains previous generations so existing placements keep their referenced prefabs. The library has 24 variants in total: sixteen retained variants, four 0.4 botanical plants and four 0.5 woodland assets.

Use the ordinary package commands in the configured sandbox:

```text
bwork_foliage action=build-assets
bwork_foliage action=apply batchId=canopy recipePath=Packages/com.bfjord.tools/Samples/woodland-canopy.json
bwork_foliage action=apply batchId=deadwood recipePath=Packages/com.bfjord.tools/Samples/woodland-deadwood.json
bwork_foliage action=apply batchId=meadow05 recipePath=Packages/com.bfjord.tools/Samples/woodland-grass.json
```

The supplied seeds are 51915, 51916 and 51917 respectively. Requested caps are 44 trees, 18 logs and 260 mixed-grass plants; actual counts depend on the current scene. Woody batches share spacing; logs restrict slope to eight degrees; the grass recipe mixes tall grass, coastal grass and daisies. Road, water and structure exclusions remain authoritative.

Grass has a maximum shader displacement bound of approximately 0.14225 m. All LODs share the same root/height field; forward, shadow, depth and normal passes use the same deformation. Logs remain rigid. A fixed-time review is available with `bwork_foliage action=grass-view`, then `action=wind-frame windSeconds=0` or `1.5`; `windSeconds=-1` restores live engine time. Any offline Blender wind illustration is labeled as such and is not Unity playback evidence.

## Seven Terrain surfaces, four palettes

The bank provides seven physical surfaces while selecting exactly four simultaneous layers per palette:

| Surface | Poly Haven source | Physical tile |
|---|---|---:|
| Meadow | [Leafy Grass](https://polyhaven.com/a/leafy_grass) | 2 m |
| Soil | [Park Dirt](https://polyhaven.com/a/park_dirt) | 3 m |
| Talus | [Gravel Floor 03](https://polyhaven.com/a/gravel_floor_03) | 2.5 m |
| Outcrop | [Rocky Terrain](https://polyhaven.com/a/rocky_terrain) | 90 m |
| ForestLitter | [Forest Floor](https://polyhaven.com/a/forest_floor) | 2.14 m |
| CoastalShingle | [Coast Sand Rocks 02](https://polyhaven.com/a/coast_sand_rocks_02) | 15 m |
| RockFace | [Rock Boulder Dry](https://polyhaven.com/a/rock_boulder_dry) | 1.8 m |

| Palette | Four active surfaces |
|---|---|
| temperate | Meadow, Soil, Talus, Outcrop |
| woodland | Meadow, ForestLitter, Talus, RockFace |
| coast | Meadow, CoastalShingle, Talus, RockFace |
| cliff | Meadow, Soil, CoastalShingle, RockFace |

The three new maps are prepared at 1024×1024 with color, OpenGL normal and a Terrain mask: R metallic=0, G source AO, B source displacement, A inverse source roughness. The retained four masks use the same channel contract at 512×512. Physical texture footprints are preserved; in particular the 90 m Rocky Terrain aerial scan is not compressed into a small repeating masonry-like tile. The four-layer choice retains the native Terrain Lit height-blend setup. Exact source files, supplier checksums and SHA-256 values are recorded in `assets/BFjordTools/TerrainDetail/sources.json` and `surface-bank-sources.json`.

`build_masks.py` and `build_surface_bank.py` under `scripts/terrain_detail` reproduce the prepared masks and surface bank from pinned CC0 inputs. Source author credits and the separate original-art grant are listed in [ASSET_LICENSE.md](../ASSET_LICENSE.md).

## Repeatable seeds

| Randomized workflow | Saved seed and reproduction data |
|---|---|
| Built-in height stamps | `bwork_terrain ... seed=<int>`; exact arrays and additive generator metadata in `terrain-recipe.json` |
| Terrain material patches | `TerrainPaintProfile.seed`; saved paint profile, palette and classification settings |
| Foliage batches | Recipe `seed`, species weights, area, spacing and owned placement receipt |
| Rock batches / river dressing | Recipe `seed` plus distribution, terrain and exclusion inputs |
| Nine-rock showcase | `bwork_rock_showcase seed=<int>` controls scale, yaw and slight spacing variation; seed and exact placements persist in its receipt |
| Water texture pattern | Saved appearance `waterPatternSeed`; shared across low/medium/high comparison presets |

Built-in height seed zero preserves the legacy ridge, basin, mesa, eroded-ridge and coastal-bluff formulas. Nonzero seeds apply a bounded coherent warp without modifying the finite stamp border or consuming global Unity random state. Custom stamp recipes already store their values, so a nonzero CLI seed is rejected for those recipes instead of being silently ignored. Deterministic reproduction also requires the same terrain, neighboring batches, exclusions and source catalog. Road ribbons/markings, bridge systems and structures follow explicit routes, dimensions and design parameters. They do not expose an unused geometry seed or randomly choose an entire design. The seed controls above vary only their stated height, material-pattern or placement inputs; a seed is not a promise to generate a new mesh family.

## Roads and water

Asphalt receives two solid yellow center lines and white outer edges, clipped onto the actual stored road surface. Gravel and dirt remain unmarked. On an existing road-before-water assembly, use `bwork_roads action=refresh-markings`; this updates paint without rebuilding the road or rewriting Terrain. `action=remove-markings` removes only owned paint.

Water has low, medium and high appearance profiles with downstream-aligned flow, progressively stronger foam and shallow turbulence. The three profiles share their texture-pattern seed. Use `bwork_water_fidelity` with `preset=low`, `medium` or `high`; existing connected water must already be installed. Appearance refresh retains the accepted canonical geometry and Terrain cuts; a stronger wave envelope refreshes its owned culling bounds. Ocean shading adds sharper bounded crests and shore-contact wash toward the sample beach. These are visual controls, not measured river velocities or fluid simulation.

## Complete source checks

The combined Unity Editor integration snapshot passed **133/133** tests in **103.54 seconds**, with **0 failed, 0 skipped and 0 inconclusive**. That snapshot includes the Terrain material bank, 17 terrain-seed cases, water-wave changes, road legacy/cleanup behavior and showcase-planner JSON. The separate CLI suite passed **8/8** in **0.537 seconds**. These are separate suites, not a combined Unity test count.

Source catalog integrity passed for all **248 entries**. Public export covers **81 sanitized FBXs**; current exported hashes and transformation details remain in the manifest and export-provenance record. The final assembled-road and water-refresh lifecycle checks passed in 17.206 seconds and are recorded separately from test-suite totals.

## Evidence state and limits

Reviewed actual Unity captures are available for [mixed woodland](../images/fidelity-05-woodland.png), [deadwood](../images/fidelity-05-deadwood.png), [grass](../images/fidelity-05-grass.png), and the same-camera [woodland](../images/fidelity-05-terrain-woodland.png), [coast](../images/fidelity-05-terrain-coast.png) and [cliff](../images/fidelity-05-terrain-cliff.png) Terrain palettes. The [actual Unity grass-wind clip](../clips/fidelity-05-grass.mp4) contains 48 captured frames over four seconds at a 12 fps encoding rate. The capture command completed in 62.778 seconds with no reported water-shader errors; that duration and encoding rate are not rendering-performance results.

The [asphalt markings](../images/fidelity-05-road.png), same-camera [low](../images/fidelity-05-river-low.png), [medium](../images/fidelity-05-river-medium.png) and [high](../images/fidelity-05-river-high.png) river-flow stills, and [larger foamy beach waves](../images/fidelity-05-beach.png) are now reviewed. River comparisons use shader time 2 seconds and a common pattern seed. Final water shader import reports support with no messages.

The stronger beach setup uses a 0.95 m primary-wave bound and 19 m wavelength. Its owned mesh culling bounds were refreshed while canonical vertex/index arrays and all 66,049 Terrain height samples remained unchanged. This is still a surface-wave presentation without an overturning lip or volumetric spray. The final setup completed in 13.832 seconds and staged the nine existing rock variants; the [labeled nine-rock capture](../images/fidelity-05-boulders.png) is reviewed. Its separate 8.511-second command produced identical first/repeated placement data and retained all 66,049 Terrain heights. The assembled-road lifecycle passed after its save-time asset-hash repair. The earlier full Editor suite snapshot is recorded above; later scoped results are listed separately. The [river motion clip](../clips/fidelity-05-river.mp4) contains 48 unique captured frames over four seconds; the [beach clip](../clips/fidelity-05-beach.mp4) contains 72 unique frames over six seconds. Both are 1440×960, encoded at 12 fps. The final motion command completed in 10.356 seconds with no shader errors. These figures describe captured output and Editor operation time, not sustained rendering performance. Recorded assembly counts and Blender source checks do not substitute for those results.

The woodland forms and bark are original procedural candidates, not scans or a commercial-reference-parity claim. The seven licensed Terrain surfaces include genuine scan inputs at their stated physical footprints, while the original woodland atlas and some rock material channels remain procedural or documented approximations; all assets are not represented as scans. The nine established rock families remain available; this iteration adds a clearer comparison layout rather than claiming nine additional rock families. Foliage LOD transitions, whole-island composition and sustained iPad costs remain unverified. Water is finite visual geometry without fluid coupling, overturning wave volumes or volumetric spray. Original source/review artwork and licensed scans retain their documented licenses; no private reference photograph or vendor package is distributed.

After that snapshot, source compilation remained clean and scoped Editor regressions passed separately: **RoadMarkingTests 9/9 in 11.36 seconds** and **WaterSurfaceOwnershipTests 5/5 in 5.41 seconds**. Seven test cases were added after the snapshot. The road checks cover deferred URP normalization for yellow/white paint and exact legacy-receipt proof; water checks prevent retirement of shared surface assets. These scoped runs are not added to the 133-test total.

The final assembled lifecycle report, `fidelity-05-final-lifecycle.json`, passed in **17.206 seconds** (inner stopwatch 17.084 seconds). Road remove/refresh/repeat/remove/restore preserved the retained base meshes, complete Terrain height/hole data and receipt Terrain patch; foreign children and edited transforms were rejected. Exact legacy-hash proof migrated the existing paint receipt safely. Stronger-envelope water refresh retained canonical geometry and all **66,049** sampled heights (`canonicalGeometryChanged=false`, `terrainChanged=false`) with the **0.95 m** wave bound. Implementation and review gates are complete; the source is published in bfjord-tools. This is Editor evidence, not device acceptance.
