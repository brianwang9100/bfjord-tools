# Sandbox bridges and tunnels

`bwork_structures` builds an original curved bridge and a hill tunnel, including ground-connected approaches, in the isolated 512m authoring sandbox. These are separate structure samples: their approaches do not automatically join the road tool's network or the full island/native route catalog.

## Commands

Open the authoring sandbox alone in Edit Mode and apply roads first to establish full-detail Terrain. The command accepts `action=prepare|apply|remove|status` and optional `recipePath` pointing to local JSON up to 1MiB. Omitting the recipe uses the current coastal bridge and ridge-crossing tunnel demonstration.

`prepare` computes geometry, grades and Terrain changes without mutation. `apply` saves one owned `Structures` group, meshes, materials and `structure-edit.json`. Reapply replaces that group. `remove` restores only owned height/hole cells and deletes owned generated assets. It preserves sibling road/water/foliage groups, unrelated Terrain edits and pre-existing holes. Changed owned cells or Terrain identity/grid/transform reject the operation. Remove structures before removing roads; structures require the road tool's full-detail Terrain setting and do not take ownership of it.

## Recipes

JSON has a `structures` array with one to eight items. Each item requires `id`, `sourceId`, `kind` (`bridge` or `tunnel`), `style` (`concrete` or `stone`), and world-space `controls` containing `{ "x": 290, "y": 34.6, "z": 165 }` points. Controls are curve/height guides; fitting may change their heights.

| Dimension | Default | Meaning |
| --- | ---: | --- |
| `width` | 8m | Deck/tunnel width; 3–12m supported |
| `clearance` | 5m | Bridge underside gap, or tunnel crown height above deck |
| `thickness` | .5m | Structural shell thickness before Terrain-seal allowance |
| `parapetHeight` | 1.1m | Bridge barrier height |
| `supportSpacing` | 20m | Bridge pier spacing |
| `portalDepth` | 2m | Portal return's interior length |
| `minimumCover` | .5m | Ground/arch overlap threshold for hole treatment |
| `approachLength` | 40m | Ground tie distance; 12–100m supported |

`hasWaterLevel=true` with `waterLevel` adds an explicit horizontal water reference for bridge fitting. Water meshes are not inferred. The recipe-level `concreteMaterialPath` defaults to the original `Assets/Generated/FjordReview/Tunnel Concrete.mat`; another existing material may be selected. A missing default uses plain original URP concrete, while an explicitly configured missing material rejects the operation.

## Geometry and Terrain behavior

Bridges have curved slabs, parapets, piers and end abutments. Tunnels have decks, closed arched linings and closed portal returns without caps across the opening. The default visible portal rim is .75m thick, tapering behind the lip into wider Terrain-sealing geometry. Both structures have asphalt approaches with exact deck endpoint heights and Terrain-blended shoulders; tunnel approaches retain level aprons beneath the portal returns.

Centerline fitting targets **24% grade**, below the **25% maximum**. Approach grades also must remain at or below 25%; rejected approaches require relocation or a longer recipe length. Bridge underside clearance is checked at fitted stations and five lateral positions. That is a discrete check against ground/explicit water level, not continuous swept collision or water-triangle conformance.

Approach Terrain uses the same triangle/cell conformance as roads, targeting 3cm clearance and validating stored heights after apply. It requires the 257² full-detail setting (`heightmapMinimumLODSimplification=4`, `heightmapMaximumLOD=0`, `ignoreQualitySettings=true`). Tunnel holes use Unity's `[y,x]`, true-surface/false-hole grid. Existing ground above the arch remains; portal/lining intersections are opened. The outer lining and receding portal returns enclose the cut-cell envelope. The portal, lining, bridge supports, and materials remain simple sample artwork rather than final NatureManufacture-level fidelity.

Sparse receipts own changed heights and hole cells. Geometry is bounded to 150,000 vertices per structure and 8,192 changed hole cells per command. There is no runtime generation or structure LOD hierarchy. The saved tool checkpoint is the recovery boundary; scene/asset saving is not a crash-atomic transaction. These samples establish neither native route admission nor physical-device performance.

## Confirmed evidence

The completed native Unity 6000.6 lifecycle built one bridge and one tunnel with **27,516 vertices / 9,172 triangles**, **190 changed hole cells**, **1,159 changed height cells**, and maximum grade **24.000083%** (float tolerance). Stored-grid approach clearance improved from **−0.944217m** to **+0.0330255m**, requiring at most **1.170272m** additional cut. Sampled bridge underside clearance is **3.999999m**.

Reapply retained identical stored heights and hole coverage. Removal restored both arrays, and a final apply rebuilt the exact owned group. Run the complete roads, water, structures, and foliage lifecycle with:

```text
bwork_verify
```

The assembled Editor sample took **12.3 seconds** in one observed run. This is authoring-command elapsed time, not FPS, runtime structure performance, final visual fidelity, island/native route acceptance, or iPad performance.

```sh
ruby artifacts/fjord-road-tool-001/StructuresReview/compile.rb
ruby artifacts/fjord-road-tool-001/StructuresReview/smoke.rb artifacts/world-authoring-tools-001/current-terrain.json
```

Implementation reuses original Bwork road fitting, tunnel frame conventions and sparse hole restoration. The reviewed MIT [Unity procedural-mesh example](https://github.com/sunsided/unity-procedural-meshes) was not copied. The hole contract uses Unity's public [TerrainData.SetHoles API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TerrainData.SetHoles.html); no proprietary import or paid package is required.
