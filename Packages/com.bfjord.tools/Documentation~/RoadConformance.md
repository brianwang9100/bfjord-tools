# Sandbox roads and Terrain conformance

`bwork_roads` creates curved roads and explicit junctions in the isolated 512m authoring sandbox. It returns fitted candidate paths, mesh surfaces and owned Terrain edits. It does not modify the full island road network or admit routes to the native workout engine.

## Commands

Open the authoring sandbox alone in Edit Mode. The command accepts:

| Argument | Values |
| --- | --- |
| `action` | `prepare`, `apply`, `remove`, `status` (default) |
| `recipePath` | Optional local JSON file, up to 1MiB; omitted uses the original hill example |
| `fourArms` | `false` by default; `true` selects the four-arm built-in example |

Run `prepare` to inspect counts, grade and Terrain clearance without changing assets or the scene. Run `apply` to build and save the result. Reapplying replaces the owned `Roads` group and its generated assets. `remove` restores owned height cells and prior Terrain LOD settings; unrelated edits are retained. Changed owned cells, target identity or owned LOD settings reject replacement/removal. Remove structures before roads, because structure approaches require the full-detail Terrain setting established by roads.

## Recipes and limits

JSON uses `minX`, `minZ`, `size` (default 512), `sampleSpacing` (default 2m), `maximumGrade` (default `.24`), and a `roads` array. Each road has:

- `id`, `sourceId`, `startNode`, `endNode`: explicit unique road identity, provenance and endpoint connectivity.
- `surface`: `asphalt`, `gravel` or `dirt`; `width`: 3–12m, default 8m.
- `controls`: world-space `{ "x": 100, "y": 0, "z": 180 }` points. X/Z curves are resampled before fitting. Heights follow the original Terrain unless `useControlHeights` is `true`.
- Optional `spans`: `{ "id": "crossing", "kind": "bridge", "startMeters": 60, "endMeters": 90 }`. Kind may be `bridge` or `tunnel`; station intervals omit shoulder/earthwork treatment. They do not create structures or bind to `bwork_structures`.

The compact adapter supports terminal nodes and **three- or four-arm junctions**. The default example has three arms; four arms are optional. Connected endpoints must coincide in X/Z and their roads must share one width. Combine two-arm segments into one road. Crossing lines do not automatically create junctions. Junction approaches need sufficient trim distance; unsupported/folded layouts reject explicitly.

Recipes are bounded to 32 roads, 64 controls per road and 1–4m sample spacing. The default fit is **24% grade**, below the **25% maximum**. Explicit bridge/tunnel spans cannot overlap junction patches. Mixed junctions prefer asphalt when present; detailed material transitions remain authoring work.

## Terrain and material behavior

The fitter preserves road mesh positions and fits Terrain to the actual triangle footprint. It checks intersections with both possible full-resolution Terrain cell diagonals, including narrow triangles that contain no heightmap vertex. Additional lowering is limited to contributing cell corners, at most one grid cell beyond the footprint, and capped at 4m beyond the initial earthwork. A bound failure requires better authored geometry; it never silently raises the cap or shifts the road.

The target mesh/Terrain separation is 3cm. Fitting reserves Terrain quantization slack, then validates the actual stored post-write heights. Metre-space UV0, lateral/station UV1, generated tangents and original `AsphaltEdge`, `GravelVerge` and `DirtVerge` materials blend pavement into shoulders.

Clearance is certified for the **full stored grid**, not arbitrary coarser LODs. On the current 257² Terrain, the tool sets `heightmapMinimumLODSimplification=4`, `heightmapMaximumLOD=0` and `ignoreQualitySettings=true`. The receipt owns/restores these settings. This bounded sandbox configuration is not a large-island Terrain strategy or an iPad performance claim. Scene/asset saving uses the tool host's saved checkpoint; it is not a crash-atomic multi-asset transaction.

## Confirmed evidence

Unity 6000.6 built the default three roads and one junction: **2,756 vertices, 4,818 triangles, maximum grade 24%**. Native stored-grid minimum clearance improved from **−0.788254m** to **+0.0328386m** with the full-detail 257² Terrain. Actual captures include the original interpolation defect and `roads-fixed.png` under `artifacts/world-authoring-tools-001/screenshots/`.

The combined Editor lifecycle verified read-only prepare, identical stored heights after replacement, restoration of Terrain and prior LOD settings on removal, one exact `Roads` root after reapply, and deletion of superseded mesh assets. The road report records 6,014 changed height cells and at most 0.999987m additional cut. Run the bundled lifecycle with:

```text
bwork_verify
```

The complete assembled sample lifecycle took **12.3 seconds** in one observed Editor run. This is authoring-command elapsed time, not FPS, runtime road performance, an island build benchmark, or iPad acceptance.

Focused offline checks cover three/four-arm examples, a triangle wholly inside a Terrain cell, conservative height quantization, unrelated-cell preservation and excessive-cut rejection. Run from the repository root:

```sh
ruby artifacts/fjord-road-tool-001/Conformance/smoke.rb
ruby artifacts/fjord-road-tool-001/Conformance/compile-command.rb
```

The implementation reuses original Bwork road fitting, junction conformance and projected triangle intersection math. Reviewed MIT alternatives [Unity-SplineRoadUtils](https://github.com/elrod/Unity-SplineRoadUtils) and [roadcreator](https://github.com/MarcusElg/roadcreator) did not establish this clearance contract or current iPad compatibility; no external source was copied. Unity's public [Terrain API](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/Terrain/Public/Terrain.bindings.cs) and [Inspector LOD range](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/TerrainEditor/TerrainInspector.cs) informed the setting scope.

The full-detail guarantee applies only to the stored grid of this finite 257² sandbox Terrain. It does not establish arbitrary coarser Terrain LOD clearance, full-island road integration, final surface art, route-catalog acceptance, or device performance.
