# Nine-boulder showcase and natural garden

Use the admitted original rock library to compare round, flat, upright, stratified, fractured and eroded silhouettes on the current sandbox terrain:

```text
bwork_rocks action=build-assets
bwork_rock_showcase action=prepare seed=20260916 centerX=80 centerZ=145
bwork_rock_showcase action=setup seed=20260916 centerX=80 centerZ=145
bwork_rock_showcase action=view
bwork_sandbox action=capture name=boulder-comparison
```

This creates one labeled 3×3 comparison with all nine variants, their correct original material maps, mixed scales, seeded yaw and buried bases. The seed and exact placement records persist in a separate receipt and are returned by `action=status`. The default `(80,145)` clearing fits the canonical assembled fixture. Compact two-line labels and a steep orthographic camera keep the silhouettes visible. Small stone LODs stay visible at the comparison camera; shared prefab LOD settings are unchanged. Presentation colliders are disabled. No roads, water, terrain heights or other scenery are changed.

Preparation checks the full footprint against valid terrain, local ground relief and protected road/water/structure footprints. If the patch is unsuitable, choose another clearing with `centerX`/`centerZ`; the command does not flatten land or remove trees to make room. Repeat `setup` to replace only the prior owned comparison. Edits or foreign children reject replacement/removal. `action=remove` removes the owned comparison without deleting the shared library or requiring external source files.

For a natural mixed cluster, first remove the comparison, then apply the ordinary rock recipe:

```text
bwork_rock_showcase action=remove
bwork_rocks action=prepare recipePath=Packages/com.bfjord.tools/Samples/rocks-boulder-garden.json batchId=boulder-garden
bwork_rocks action=apply recipePath=Packages/com.bfjord.tools/Samples/rocks-boulder-garden.json batchId=boulder-garden
```

The garden recipe contains all nine species, seven seeded clusters, mixed scales, 16% burial and a 64-rock ceiling. Normal terrain and spacing exclusions may reduce the accepted count and species coverage; the labeled showcase guarantees one of each. Edit the recipe's `seed` for another natural layout. Remove only the garden with `bwork_rocks action=remove batchId=boulder-garden`.

This reuses existing CC0/original assets and Unity's public terrain/prefab APIs. No new art download, runtime terrain package or physical-performance claim is involved.
