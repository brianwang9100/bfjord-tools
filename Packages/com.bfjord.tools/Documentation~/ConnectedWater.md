# Connected water authoring

The connected sample builds two tributaries, a confluence, an upper meander, a lake, a meandering outlet, three delta arms and an ocean as one welded water surface. The same finite wet field carves the Terrain beneath it. It runs in the isolated 512 m authoring sandbox; it does not replace the accepted Fjord Coast scene, route catalog or installed world.

## Commands

Use the registered Unity Pipeline commands while the authoring sandbox is open alone in Edit Mode. `bwork_sandbox` with `action=create` initializes that sandbox, or keeps it when it is already open. If the original water sample is present, remove it with `bwork_water` and `action=remove` first. The two water tools refuse to apply over one another.

| Command | Arguments | Result |
|---|---|---|
| `bwork_water_connected` | `action=prepare` | Validates the packaged recipe, builds a temporary mesh and calculates the Terrain edit without applying it. |
| `bwork_water_connected` | `action=apply` | Builds and saves the owned `Connected Water Sample` group and its Terrain edit. |
| `bwork_water_connected` | `action=status` | Reports the installed group, recipe hash, owned height-cell count and any pending cleanup. |
| `bwork_water_connected` | `action=remove` | Restores the owned Terrain cells and removes the generated water assets; retries pending removal cleanup. |
| `bwork_sandbox` | `action=capture`, `name=connected-water` | Captures the current sandbox camera to the authoring evidence directory. |

`prepare` and `apply` accept an optional `recipePath`. Leave it empty to use [Samples/connected-water.json](../Samples/connected-water.json), or supply an absolute path to an edited copy. Prepare that copy before applying it. Current placement/customization uses the JSON recipe and commands; an interactive RAM-style path editor is not included.

## Recipe schema

Use `schemaVersion: 1` and a stable `id`. Positions and distances are world metres, with +X east and +Z north. Attach the generated mesh under an identity transform.

| Scope | Fields | Meaning and limits |
|---|---|---|
| Recipe | `nodes`, `reaches` | Unique named water bodies/connections; the sample has nine nodes and seven reaches. One finite ocean body is required. |
| Recipe | `cellSize`, `bankFalloff`, `waveBankFade` | Union grid spacing 1–4 m, bank transition 4–40 m, and wave fade near the shoreline 2–12 m. Defaults are 2, 16 and 4 m. |
| Recipe | `riverWaveHeight`, `riverWaveLength`, `riverWaveSpeed` | River visual displacement bound 0–1 m, wavelength 4–80 m and phase-speed control 0–3. Sample values are 0.12, 14 and 0.8. |
| Recipe | `oceanWaveHeight`, `oceanWaveLength`, `oceanWaveSpeed` | Separate ocean wave controls with the same ranges. Sample values are 0.45, 28 and 0.65. |
| Recipe | `flowSpeed`, `normalStrength`, `foamStrength` | Global texture-advection scale 0–4, ripple-normal strength 0–0.3 and foam strength 0–1. |
| Node | `id`, `kind`, `position`, `radius`, `bedDepth` | Kind is `source`, `junction`, `lake`, `mouth` or `ocean`. `position.y` sets its still-water elevation. `radius.x/y` are X/Z ellipse radii; for the ocean they are rectangle half-extents. Bed depth is 0.3–8 m. |
| Reach | `id`, `from`, `to`, `bedDepth`, `flowSpeed`, `knots` | Connects two non-ocean node IDs. Depth is 0.3–8 m and local visual flow scale is 0–4. Each reach supplies its own ordered Bézier knots. |
| Knot | `position`, `handleIn`, `handleOut`, `width`, `foam` | Handles are world positions, not offsets. Width is the full river width and must span at least four union cells. `foam` is inherited from the ribbon format; the connected mesh currently uses global/depth-based foam rather than this per-knot value. |

Use `{ "x": ..., "y": ..., "z": ... }` for positions/handles and `{ "x": ..., "y": ... }` for node radii. Node radii must be at least twice `cellSize` and at most 300 m. The current builder limits the union to a 700 m extent on each horizontal axis and a bounded mesh/lattice budget; it is intended for a finite authored sample.

Reach endpoints must exactly match their named nodes. The graph rejects directed cycles and uphill reaches. Sources have outgoing reaches, mouths have incoming reaches, and intermediate nodes have both. A mouth lies inside the ocean rectangle at its exact elevation. The packaged ocean is centered at `(256, 0.8, 470)` with half-extents `(270, 85)`, covering Z 385–555; it is a bounded patch, not an infinite ocean.

The final centerline elevation eases between flat connection zones at its endpoints. Each zone covers the node body, reach half-width and a grid margin, giving the confluence, lake inlet/outlet and delta a shared level. Interior knot elevations are validated, but node elevations and these zones determine the final water level. Add an intermediate node when a separate elevation control is needed. Reaches too short for their elevation change, unresolved wet connections and crossings with incompatible water levels fail explicitly.

## Geometry, materials and ownership

`ConnectedWaterField` reuses `BworkRiverRibbon` for Bézier sampling and width/fold checks. It clips each regular-grid triangle once and shares lattice corners and edge intersections, producing one connected mesh without stacked coplanar water sheets. This is an approximation at the selected cell size. Shore silhouettes and thin islands remain limited by that grid and the Terrain heightmap resolution.

The wet field also supplies undeformed water heights and lowers Terrain beneath channels, the lake and the ocean, with a smooth bank falloff. It does not raise terrain to support an elevated ribbon. `TerrainPatchChange` records only changed height samples: replacement starts from their restored originals, unrelated edits remain intact, and removal restores only owned samples. A conflicting later edit to an owned sample fails rather than being overwritten.

The water wrappers create a fresh mesh/material generation for each application. The receipt at `Assets/Generated/AuthoringTools/connected-water-edit.json` records the recipe hash, Terrain patch, `assets`, `cleanupAssets` and removal state. Legacy receipts admit only their exact former hash-based asset paths. The prior scene group is retained through the replacement save; failed publication restores the prior receipt and scene/Terrain state, with rollback failures reported explicitly. Superseded assets are cleaned after commit. An interrupted removal retains a cleanup receipt so retry does not restore the Terrain twice.

One material shades the shared surface. UV0 stores world XZ; UV1 stores visual flow direction/scale. Vertex color R pins wave displacement at the bank, B blends river and ocean wave/tint settings, G currently supplies no extra foam, and A is unused. The shared vertices and continuous ocean blend keep mouth displacement coherent. Mesh bounds include the maximum configured wave height. The shader requires the sandbox camera's current URP depth texture.

Flow, ripples, foam and waves are visual effects. They do not simulate fluid volume, discharge, erosion, buoyancy or physical current, and animated displacement never changes the still-water field, Terrain or workout movement. There is no per-frame CPU mesh regeneration or additional reflection camera. Waterfall authoring is not implemented in this tool.

## Research and licensing

All shipped connection/union code and the water shader are original Bwork code. No third-party code or paid water asset was imported for this sample.

| Reference | License and use decision |
|---|---|
| [InstantRiver](https://github.com/bonahona/InstantRiver/tree/77abac1058269d5d5e9013a51a06003842105cf7) | [MIT](https://github.com/bonahona/InstantRiver/blob/77abac1058269d5d5e9013a51a06003842105cf7/LICENSE.txt). Bézier placement/editor reference; its older CG shader is not a verified URP 17.6 replacement. |
| [UWS](https://github.com/anunknowperson/uws/tree/d7aea73b0aea603ed1bd34241190838c8e7caa4f) | [Apache-2.0](https://github.com/anunknowperson/uws/blob/d7aea73b0aea603ed1bd34241190838c8e7caa4f/LICENSE). Useful system reference, but its README labels it incomplete and records water-edge/reflection-camera problems. |
| [Crest public source](https://github.com/wave-harmonic/crest/tree/db0658ff0b2e93e4a9e28cc2867509658b0ecc00) | [MIT](https://github.com/wave-harmonic/crest/blob/db0658ff0b2e93e4a9e28cc2867509658b0ecc00/LICENSE). The public repository targets Built-in; its separately offered URP product is not included. |
| [Clipper2](https://github.com/AngusJohnson/Clipper2/tree/f9c5eb6e14a59f6f5d65fbfb3564519a561cf4fd) | [BSL-1.0](https://github.com/AngusJohnson/Clipper2/blob/f9c5eb6e14a59f6f5d65fbfb3564519a561cf4fd/LICENSE). Polygon-union candidate; the pinned README warns that its newer triangulation has bugs. |
| [LibTessDotNet](https://github.com/speps/LibTessDotNet/tree/bdc0b0f94904dd94c3edfb2c25aa358695276e09) | [SGI Free Software License B 2.0](https://github.com/speps/LibTessDotNet/blob/bdc0b0f94904dd94c3edfb2c25aa358695276e09/LICENSE.txt). Winding-rule tessellation candidate; it does not supply shared water levels/flow or the regular wave-mesh subdivision. |

The bounded regular-grid union avoids adding a polygon package to this example. RAM3/NatureManufacture are workflow and visual references; their proprietary code and paid content are not part of this package.

## Confirmed lifecycle evidence

The connected sample ran in Unity 6000.6.0f1 and rendered **9 nodes, 7 reaches, 29,243 vertices, 55,711 triangles and 9,079 changed Terrain height cells** as one connected wet surface.

`bwork_verify` confirmed read-only prepare, identical Terrain after reapply, exact Terrain restoration on removal, deletion of superseded mesh/material assets, and reciprocal ownership guards between `bwork_water` and `bwork_water_connected`. The full assembled sample lifecycle took **12.3 seconds** in one observed Editor run; this is authoring-command elapsed time, not FPS or runtime water performance.

```text
bwork_verify
```

Two actual same-camera Editor captures at separate times, with no geometry or material edits between them, verify animated river and ocean pixels while the fixed sky remains unchanged. See `design/native/world-authoring-tools/water-motion-verification.json` and its two source captures. The overhead water capture temporarily hides roads, structures, and foliage to expose the union footprint; those groups remain present in the combined sample and other captures.

The ocean is a finite authored patch and flow is shader texture/displacement motion rather than a fluid solver. This sandbox evidence does not establish infinite-ocean behavior, final NatureManufacture-level shoreline art, island integration, physical iPad performance, or final acceptance.
