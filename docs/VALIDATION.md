# Validation evidence

This ledger records the Unity evidence available for the September 15, 2026 source checkpoint. It includes the final functional checks and an independent Unity replay from a clean copy under an unrelated parent. All paths below are relative to the public repository; no author-machine paths are required to inspect or reproduce the source.

## 0.3.0 rock and animated-water extension

The public standalone project compiled the extension without C# errors. The full suite passed **63/63** tests in 19.94 seconds; after the final feeder/material art changes the affected waterfall suite passed **5/5** in 0.75 seconds. The original rock library imported six variants with three LODs and colliders each, plus five shared material profiles. River composition placed **150 bank rocks and 91 shallow stones**, respecting constraints rather than forcing the 220-stone cap.

A 4.242-second scene check verified deterministic highland replacement and remove/reapply, direct water-removal rejection with dependent rocks, and rejection of custom river batches before mutating the existing water or bundled rocks. A 32.651-second combined water check verified appearance refresh, waterfall prepare/replace/remove/reapply, refusal to destroy foreign child objects, unchanged Terrain height samples, and zero shader errors after rendering.

Three fixed cameras each rendered 24 frames at 12 samples/second, plus time 0/0.75/1.5 stills. Water-only image regions changed between times 0 and 1.5 seconds: river 72.9%, ocean 86.1%, waterfall 66.9% above a 3/255 channel threshold. These are animation evidence, not performance measurements. Curated screenshots and the clips were reviewed; no native island or device build was run. The earlier relocated clean-copy replay below belongs to 0.2.0, not this extension.

Machine-readable records: [contract tests](evidence/extension-tests.json), [final waterfall tests](evidence/extension-waterfall-tests.json), [rock lifecycle](evidence/extension-rock-lifecycle.json), [water lifecycle/captures](evidence/extension-water-motion.json), [motion comparison](evidence/extension-motion-comparison.json).

## 0.2.0 tested configuration and scope

- Unity Editor 6000.6.0f1 (`f7f8ed4d1e24`)
- Universal Render Pipeline 17.6.0
- Unity Pipeline 0.7.0-exp.1 for resident Editor command dispatch
- Package `com.bfjord.tools` 0.2.0
- One finite 512 m authoring sandbox plus four isolated bridge presentation sites
- 1440 × 960 Unity Editor captures

The Unity, URP and Pipeline pins are declared in `Examples~/SandboxProject/ProjectSettings/ProjectVersion.txt` and `Examples~/SandboxProject/Packages/manifest.json`. Package 0.2.0 above identifies this measured source checkpoint; a newer package version does not extend these results. `Examples~/SandboxProject/ProjectSettings/BfjordTools.json` is the explicit project opt-in and owns the allowed scenes, generated roots, source roots and capture root.

This evidence covers experimental Editor import, authoring commands, ordinary Unity assets and rendered fixture inspection. It does not establish a complete island, runtime integration, iPad compatibility, sustained frame rate, GPU memory use, physical simulation, structural-engineering fitness or NatureManufacture visual parity.

## Current acceptance state

| Area | Evidence | State |
|---|---|---|
| Standalone import and command dispatch | The sample project imported under the pinned Editor and executed allowlisted JSON commands against its configured scenes. See `Packages/com.bfjord.tools/Editor/AuthoringBatch.cs` and `Packages/com.bfjord.tools/Editor/ProjectContext.cs`. | Passed in the working standalone fixture |
| Relocated clean-copy replay | A separate copy containing only managed public files ran `doctor`, restored 162 catalog files, reused all 162 on the second preparation, imported/compiled the 512 m sample and completed seven Unity request replays with exit code 0 and `success:true`. See `scripts/bfjord.py`, `assets/BFjordTools/asset-catalog/manifest.json` and `assets/CoastalBridgeTool/demo-dependencies/manifest.json`. | Passed |
| Python CLI tests | The dependency-free public wrapper suite completed eight tests covering request/result handling, project-lock behavior and catalog preparation. | 8 passed |
| Editor contract tests | The final standalone run reported 51 total, 51 passed, zero failed and zero skipped. The suite includes project/path contracts, bridge asset and surface contracts, terrain/road presentation and rollback, connected-water bank queries, foliage planning and bounded junction-grade admission. | Passed |
| Functional sandbox lifecycle | The final `bwork_verify` run completed in 55.73509 seconds and reported exact restoration/ownership checks for terrain, roads, connected water, structures and three legacy foliage batches. See `Packages/com.bfjord.tools/Editor/SandboxValidation.cs`. | Passed after the junction repair |
| Road-junction geometry | The corrected real sandbox run reported a 23.999999% maximum centerline grade and 29.872583% maximum pavement-triangle grade. Grade admission happens before mutation. | Passed the bounded 30% pavement contract |
| Four bridge families | Four distinct asset-backed bridges imported with three LODs and one deck collider each, and each rendered in Unity. All four hero views and the steel-truss riding view received visual inspection. | Passed for the isolated Editor presentation sites |
| Independent bridge surface | Applying `assets/CoastalBridgeTool/material-profiles/warm-concrete/profile.json` preserved all four coastal bridge FBX hashes, placement and structural source identity. | Passed; material change only |
| Asset-backed foliage | `build-assets` produced 16 variants. Ten curated habitat recipes then applied successfully in a clean scene and the assembled landscape was rendered. | Passed in the bounded sandbox |

## Relocated clean-copy replay

The relocated sample was assembled from managed public files only; it did not read a Bwork source tree or reuse an existing Unity `Library`. Before Unity launched, `doctor` found the sample and both catalogs, `prepare-project` copied all 162 catalog files, and a second preparation reported all 162 unchanged. Initial Unity import and compilation completed with exit code 0 and created the 512 m sandbox.

Seven representative requests then returned exit code 0 with `success:true`:

| Request | Relocated result |
|---|---|
| Terrain prepare | 10,591 cells prepared; four bounded operations reported |
| Terrain apply | The same 10,591-cell change applied |
| Roads | Three roads and one junction; 2,756 vertices, 3,638 triangles, 23.999999% centerline and 29.872583% pavement grade |
| Connected water | Nine nodes, seven reaches and one welded river/lake/delta/ocean union |
| Foliage asset build | 16 catalog variants prepared |
| Foliage canopy | 40 of 40 instances placed |
| Bridge collection | All four family sites created with four Terrain presentation pads |

## Functional lifecycle receipt

The 55.73509-second `bwork_verify` run exercised reset, prepare, apply, exact reapply, selective removal and restoration in dependency order. Its scope string was “Isolated 512m authoring sandbox; not island or device acceptance.”

| Family | Recorded result |
|---|---|
| Terrain footprints | 4,184 protected cells, 8,145 changed cells, exact restoration passed |
| Roads | Three roads and one junction; 2,756 vertices, 3,638 triangles; 23.999999% maximum centerline grade and 29.872583% maximum pavement-triangle grade; prepare read-only, exact reapply, removal restoration and obsolete-mesh deletion passed |
| Road terrain conformance | 5,900 changed height cells; minimum stored-grid clearance improved from −1.735607 m to +0.032828 m; maximum additional cut was 2.273456 m |
| Connected water | Nine nodes and seven reaches; 29,243 vertices, 55,711 triangles; prepare read-only, exact reapply, removal restoration, obsolete-asset deletion and reciprocal ownership guard passed |
| Structures | One bridge and one tunnel in 23 parts; 27,516 vertices, 9,172 triangles; Terrain holes/heights restored and the sampled bridge underside clearance was 3.999999 m |
| Legacy foliage lifecycle | Forest 612, scrub 238 and rock 141 placed; prepare read-only, exact reapply and separate-batch preservation passed |

The road receipt distinguishes centerline grade from the stricter surface measurement. The repaired junction remained below the 30% pavement-triangle ceiling and completed the same exact reapply/removal lifecycle.

## Bridge collection

The public manifests under `assets/CoastalBridgeTool/` contain the source hashes, material bindings, LOD records and collider path. The v1 coastal manifest remains readable through the adapter; the other three manifests use the family-specific v2 contract.

| Family | Unity capture | LOD triangle counts | Collider |
|---|---|---:|---:|
| Coastal arch, 180 m | `docs/images/bridge-arch.png` | 41,940 / 5,756 / 3,572 | 1 |
| Stone viaduct, 110 m | `docs/images/bridge-stone.png` | 23,032 / 4,680 / 1,056 | 1 |
| Steel through truss, 88 m | `docs/images/bridge-truss.png` | 37,812 / 4,260 / 1,812 | 1 |
| Timber trestle, 80 m | `docs/images/bridge-timber.png` | 53,460 / 5,412 / 3,612 | 1 |

The warm-profile capture is `docs/images/bridge-warm.png`. The observed material operation reported `geometryChanges: false`; a separate before/after comparison found the four FBX files, placement receipt and structural source hash unchanged. The collection is an Editor art fixture with straight, level visual bridge assets. It is not a physics, structural, mobile or sustained-performance test.

## Foliage library and recipes

The built library contains 16 variants: one mature fir, one pine sapling, four ferns, four shrubs and six mossy rocks. Every entry produced a prefab and three declared LOD representations; vegetation entries record bounded wind amplitude and bounds padding, while rocks remain rigid. Public source and preparation receipts are under `assets/BFjordTools/asset-catalog/`, and the implementation contract is in `Packages/com.bfjord.tools/Documentation~/FoliagePresentation.md`.

All ten curated habitat recipes completed successfully in the final clean placement:

| Batch | Public recipe | Target | Placed | LODGroups |
|---|---|---:|---:|---:|
| Canopy | `Packages/com.bfjord.tools/Samples/forest-canopy.json` | 40 | 40 | 40 |
| Young trees | `Packages/com.bfjord.tools/Samples/forest-young.json` | 95 | 95 | 95 |
| Forest floor | `Packages/com.bfjord.tools/Samples/forest-floor.json` | 1,200 | 1,200 | 1,200 |
| Herbs | `Packages/com.bfjord.tools/Samples/forest-herbs.json` | 1,800 | 1,800 | 1,800 |
| Rocks | `Packages/com.bfjord.tools/Samples/forest-rocks.json` | 24 | 24 | 24 |
| Riverbank ferns | `Packages/com.bfjord.tools/Samples/riverbank-ferns.json` | 400 | 400 | 400 |
| Hillside canopy | `Packages/com.bfjord.tools/Samples/forest-hills.json` | 190 | 190 | 190 |
| Riverbank canopy | `Packages/com.bfjord.tools/Samples/riverbank-canopy.json` | 65 | 65 | 65 |
| Riverbank herbs | `Packages/com.bfjord.tools/Samples/riverbank-herbs.json` | 1,800 | 1,800 | 1,800 |
| Riverbank rocks | `Packages/com.bfjord.tools/Samples/riverbank-rocks.json` | 90 | 90 | 90 |

The final placement reached each recipe target while still reporting terrain, height, slope, exclusion and spacing rejections considered along the way. `docs/images/foliage.png` is the assembled Unity capture. It demonstrates the bounded authoring fixture; the 19 m mature fir remains expensive and the image does not establish mobile density or runtime cost.

## Public captures and interpretation

The bridge-family, warm-surface and foliage images listed above are current standalone Unity captures. The four bridge hero views were inspected individually, together with the steel-truss riding view. The final terrain, forest, lake, road and junction captures were also reviewed against the corrected geometry without changing it. Terrain, road and water images under `docs/images/` document the bounded fixtures and do not expand the validation scope beyond Editor authoring.

The final connected-water review removed the visible diagonal surface seam. The shader keeps its default depth fallback at opaque depth 1 and declares the URP transparent surface path; this is a rendering contract, not fluid simulation or underwater-rendering acceptance.

The legacy gray structure/tunnel group was explicitly removed with `bwork_structures action=remove` before the curated landscape captures. Its lifecycle and Terrain-hole restoration remain tested utility behavior, but no tunnel beauty or visual-fidelity claim is made.

## Validation boundary

The clean-copy Unity replay closes the standalone portability check for the representative terrain, road, water, foliage and bridge requests above. Publication is a separate delivery action and does not broaden the evidence. The completed results support a functional standalone Editor fixture with bounded terrain, road, bridge, foliage and water evidence. They do not support mobile, runtime, whole-island, fluid-simulation or NatureManufacture-parity claims.
