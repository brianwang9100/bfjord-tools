# 0.4.0 fidelity validation

This record covers the September 15, 2026 fidelity candidate in Unity 6000.6.0f1 with URP 17.6.0. It extends, but does not rewrite, the separately versioned [0.2.0 and 0.3.0 validation ledger](../VALIDATION.md). Timings below are measured Editor command durations, not frame-rate or device-performance results.

The compact machine-readable results are available in [`evidence.json`](evidence.json).

## Editor suite

The candidate imported and compiled with no Unity compile errors. The full Editor suite completed in 21.43 seconds: **81 passed, 0 failed, 0 skipped, 0 inconclusive**. This includes focused terrain, foliage, rock, road-detail, tunnel-detail, bridge and water contracts.

After that full suite, an FBX root-axis conversion repair added two road regressions. A separate scoped run completed in 0.73 seconds: **9 passed, 0 failed, 0 skipped**. This remains a separate run and is not reported as an 83-test full suite.

## World assembly

The assembled terrain/foliage/rock pass completed in 19.092 seconds. It retained all **66,049** samples of the 257×257 Terrain heightmap while applying the new material presentation and producing:

| Batch | Placed |
|---|---:|
| Highland rocks | 130 |
| Riverbank rocks | 150 |
| Shallow-water rocks | 87 |
| Meadow detail | 177 |
| Rose thickets | 54 |
| Wood sorrel | 247 |

Each new plant recipe was applied twice with the same placed count. Wood sorrel was then removed and reapplied with 247 instances. The rock catalog reported nine variants and 20 shared material bindings. These counts verify the bounded recipes and deterministic replacement exercised here; they are not density targets for another scene.

## Water lifecycle and motion

The water milestone completed in 38.463 seconds. It refreshed connected-water appearance without rebuilding its geometry, prepared and replaced the waterfall, verified that an injected foreign child blocked destructive work, removed and reapplied the owned waterfall, and retained every Terrain height sample. `ShaderUtil` reported no errors for the two reviewed water shaders.

River, ocean and waterfall each captured 24 frames over two seconds at 1440×960. The clips use 12 fps offline encoding:

- [River](../clips/fidelity-04-river.mp4)
- [Ocean](../clips/fidelity-04-ocean.mp4)
- [Waterfall](../clips/fidelity-04-waterfall.mp4)

Representative time-zero stills are [river](../images/fidelity-04-river-t0.png), [ocean](../images/fidelity-04-ocean-t0.png), and [waterfall](../images/fidelity-04-waterfall-t0.png). The waterfall is a finite freestanding fixture; this evidence does not establish finished cliff integration, fluid simulation, spray, refraction, or dynamic wetness.

## Bridge gallery

The bridge collection setup and capture run completed in 144.859 seconds and produced ten 1440×960 stills:

- Hero and riding views for `coastal-arch-180`, `stone-viaduct-110`, `steel-through-truss-88`, and `timber-trestle-80`.
- `fidelity-04-stone-viaduct-110-construction.png`, using the maintained side camera rather than a close-up detail camera.
- `fidelity-04-coastal-arch-180-warm-material.png`, captured after applying the independent `warm-concrete` surface profile and followed by a successful reset to default surfaces.

The gallery checks the four imported design silhouettes and the geometry-independent surface path under one finite Editor presentation. It does not establish structural engineering, arbitrary bridge fitting, runtime performance, or physical-device acceptance.

## Road and tunnel lifecycle fixture

Road and tunnel lifecycle acceptance ran in a separate fresh 512 m fixture. The existing composed world was unsuitable for this operation because later river cuts correctly prevent replacement of the earlier road-owned Terrain samples. This evidence does not claim dressed-road integration with the river world.

The road milestone completed in 8.260 seconds, including removal of the preceding tunnel state and final fixture restoration. It produced 47 dressing instances across three roads and one junction. Repeated apply retained exact geometry, poses, heights and holes while cleaning old generated assets. Foreign-child apply/remove and edited-transform operations refused before mutation. Removal without the source catalog restored exact heights, holes and Terrain LOD state, and the final reapply retained unrelated scene subtrees. The importer fix preserves Unity's FBX root-axis conversion when calculating bounds and baking generated meshes. Two regression tests cover the transformed bounds and mesh bake; material filename comparison remains outside that geometry contract.

The tunnel milestone completed in 10.048 seconds. It repeated with exact geometry, poses, heights and holes; cleaned the prior generated assets; refused foreign-child and edited-transform destruction; removed without its source catalog; restored exact heights and holes; and retained unrelated subtrees. The final applied tunnel has two masonry LOD groups, 297 changed hole cells and 673 changed height cells.

The [road capture](../images/fidelity-04-road-dressing.png) shows readable segmented stone and grounded wall contact. The [tunnel exterior](../images/fidelity-04-tunnel-exterior.png) shows the masonry arch and coping grounded at the entrance, while the [interior](../images/fidelity-04-tunnel-interior.png) shows continuous curved lining and service curbs. The fixture also exposes an oversized rear Terrain-hole seal beyond the portal. Island integration must bury or fit that return before this can be presented as a finished mountain tunnel.

## Capture index and limits

World stills are `fidelity-04-terrain.png`, `fidelity-04-foliage-patch.png`, `fidelity-04-rocks.png`, `fidelity-04-river-t0.png`, `fidelity-04-ocean-t0.png`, and `fidelity-04-waterfall-t0.png`. Bridge filenames use the same `fidelity-04-` prefix and the design/view names listed above. Blender source reviews remain separately labeled and are not counted as Unity evidence.

The current world composition is sparse in places and uses simple review lighting. Moving-camera LOD behavior, sustained performance and iPad suitability remain unverified. No screenshot or timing in this record claims NatureManufacture parity. Lifecycle acceptance does not replace the remaining island-level road ordering, tunnel burial/fit, or finished-environment art work. This record describes a pre-publication candidate; publication remains a separate repository action.
