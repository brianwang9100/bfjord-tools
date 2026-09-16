# Foliage authoring

`bwork_foliage` plans and applies deterministic prefab batches from JSON recipes. It uses live Terrain height and normal queries, weighted shared LOD prefabs, per-species scale and footprint radius, and generated road, water, and structure exclusions.

## Use named batches

Pass `batchId` when forest, scrub, and rock groups must coexist. It accepts 1–32 lowercase letters, digits, hyphens, or underscores and must start with a letter or digit.

```text
bwork_foliage action=prepare batchId=forest recipePath=Packages/com.bwork.world-authoring/Samples/forest.json
bwork_foliage action=apply batchId=forest recipePath=Packages/com.bwork.world-authoring/Samples/forest.json
bwork_foliage action=apply batchId=scrub recipePath=Packages/com.bwork.world-authoring/Samples/scrub.json
bwork_foliage action=apply batchId=rock recipePath=Packages/com.bwork.world-authoring/Samples/rock.json
bwork_foliage action=status batchId=forest
bwork_foliage action=remove batchId=forest
```

Empty `batchId` preserves the original ownership contract:

- Root: `Bwork Foliage [owned:bwork_foliage:v1]`
- Outputs: `bwork-foliage-recipe.json` and `bwork-foliage-result.json`

For `batchId=forest`, ownership is independent:

- Root: `Bwork Foliage [owned:bwork_foliage:v1:forest]`
- Outputs: `bwork-foliage-forest-recipe.json` and `bwork-foliage-forest-result.json`

Prepare and status are read-only. Apply fully plans and instantiates a pending group, then replaces only the exact selected root after the new scene state saves. Remove deletes only the selected root. Other named foliage batches remain in place.

## Recipe fields

- `area`: world X/Z rectangle serialized as `{ "x", "y", "width", "height" }`; `y` is world Z.
- `seed`, `densityPerHectare`, `maximumCount`: deterministic candidate target, bounded to at most 2,000 placements.
- `minimumSpacing`: minimum center separation within this batch.
- Height and slope bounds: reject unsupported Terrain points before placement.
- `species`: weighted prefab key, footprint radius, and scale range. Each prefab must contain one LOD group and at least one renderer.
- `exclusions`: up to 256 unique `circle`, `capsule`, or axis-aligned `box` footprints.

Spacing is enforced within the selected batch. Batches with the same nonempty `spacingGroup` also reserve their existing scaled footprints across batches; recipes without a shared group remain independent. Use `woody` for canopy cohorts and explicit exclusion primitives where other groups must remain clear.

Canopy placement in 0.6 checks the low root collar across all LODs against the Terrain footprint. It lowers upright trees within a bounded allowance or rejects unsupported cliffs and holes; it never changes Terrain heights. Existing batches acquire this correction when reapplied. Crown spacing and root support use separate bounds.

Package recipe paths resolve through Unity's package registry, including external UPM cache locations. Project-relative paths must remain inside the project.

## Generated-geometry exclusions

`SpatialExclusions.Create(ToolSandbox.Root, recipe.exclusions)` indexes readable meshes beneath exact tool-owned road, water, and structure roots. It buckets transformed triangles by XZ bounds, then tests actual triangle or edge distance expanded by the scaled species radius. A curved road's broad AABB does not discard empty land around the curve. Water triangles protect the authored surface and channel footprint; this is a top-down planting-clearance contract.

## Confirmed lifecycle evidence

The combined Unity 6000.6.0f1 milestone applied **612 forest**, **238 scrub**, and **133 rock** placements in three disjoint recipe areas. Forest and scrub reached their calculated targets; rock placed 133 of 166 targeted candidates after slope, footprint, and spacing rejection. Every retained instance has one LOD group.

`bwork_verify` confirmed read-only prepare, exact forest reapply, independent named roots, and forest removal without changing scrub or rock. The full assembled sample lifecycle took **12.3 seconds** in one observed Editor run. That is authoring-command elapsed time, not FPS or runtime performance.

```text
bwork_verify
```

The command is restricted to the isolated authoring probe and does not prove production-island placement, final NatureManufacture-level visual density, iPad performance, or device acceptance. Command receipts are the source for considered, placed, and rejected counts; a screenshot alone is not.

Unity documents the mesh vertex/index and local-to-world inputs used by the footprint index:
<https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-vertices.html>,
<https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-triangles.html>, and
<https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Transform-localToWorldMatrix.html>.
