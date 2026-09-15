# Roads fidelity pass — implementation checkpoint

The roads pass adds original, asset-backed road margins while retaining the verified road topology, crown, fitted normals, terrain transactions and shared CC0 surface scale. It is explicit opt-in: existing commands and saved road recipes do not acquire props automatically. The bounded Unity lifecycle and art review are recorded in the [0.4.0 validation record](VALIDATION.md).

## Private reference comparison

Reviewed local private images (not copied into the package):

- `mountain-Mountain-Environment-Dynamic-Nature-gallery-06`: the forest path has scattered mineral forms, irregular contact, restrained paths through understory and a clear human scale. The toolkit's stable `artifacts/bfjord-tools/docs/images/road-level.png` shows a broad, smooth verge with little geometry to communicate scale.
- `mountain-Mountain-Environment-Dynamic-Nature-gallery-70`: the mountain path uses intermittent stones and uneven edges beside water, while stable `docs/images/roads.png` has a continuous broad shoulder and visually empty margins. These are path/environment references, not photographs of a road wall or traffic delineator; the new wall/post designs are original extrapolations.

Both private references originate at [Nature Manufacture's Mountain Environment gallery](https://naturemanufacture.com/mountain-environment-dynamic-nature/). No promotional pixels, vendor models or proprietary source were opened by either generator. The private gallery's lighting, mature vegetation, terrain detail and road-adjacent planting still exceed this bounded road-only change; no commercial-pack parity is claimed.

## Reuse decision

The required quick search considered [Poly Haven clean asphalt](https://polyhaven.com/a/clean_asphalt), [worn asphalt](https://polyhaven.com/a/worn_asphalt), [Poly Haven CC0 licensing](https://polyhaven.com/license), and [Unity's Splines mesh extrusion](https://docs.unity.cn/Packages/com.unity.splines%402.7/manual/extrude-mesh.html). The package already has admitted Poly Haven asphalt/gravel/dirt with physical texture scale and a functioning finite road/junction solver. Replacing these would duplicate solved functionality without supplying the missing roadside shape. Keep those sources and the existing URP material path. New meshes use ordinary URP Lit and LODGroup, with no runtime package dependency. Unity/iPad performance is unverified; the budget below is asset census only.

## Concrete changes

`bwork_roads action=prepare dressing=true` reports the supported candidate count; `apply dressing=true` admits the source models and creates short wall groups plus asphalt-side posts. `status` reports the flag and instance count. Omitted `dressing` remains false on each apply. `action=view-dressing` frames an actual supported wall at road level using terrain/road collision to keep the camera above the surface.

`RoadDetailPresentation` places walls beyond the rendered verge (0.85 × road width) with another 0.28m clear strip. Three 2.4m wall modules form separated fragments every 36m; an asphalt-side delineator sits across the road between fragments. Placements avoid visible endpoint mouths, all other route ribbons, and eight metres around bridge/tunnel spans. A 5 × 3 footprint support check follows the tilted base, rejects terrain holes and excessive residuals, and embeds accepted bases by 0.06m. Terrain is sampled from the prepared final height grid before mutation. The finite placement ceiling is 384, independent of recipe size.

The shared `SpatialExclusions.Create` has one backward-compatible optional `excludedGroups` argument restricted to recognized groups. Roads omit only the prior `Roads` generation. Actual water/structure triangles, rather than their union renderer bounds, keep dry ground between water bodies available; other scenery uses conservative bounds. New wall meshes are copied into readable, generated `roads-*` assets, so the existing vegetation triangle index sees their exact footprints without changes to vegetation or catalog APIs.

The road receipt owns the new copied mesh/material assets and all dressing children. Replacement failures retain the existing road/height rollback path, and removal requires no external FBXs. No wall/post collider is added. A new hierarchy seal checks transforms, active child states, mesh/material references, colliders and LOD membership before replacement/removal. A strict legacy migration admits only the original flat mesh children, exact owned mesh references and known road material paths. Foreign children or changed owned hierarchies cause a refusal before destruction.

## Original art and evidence

`assets/BFjordTools/RoadDetail` contains the final source assets, local review and separate catalog addition. The two 512px stone maps are original procedural mineral variation. One mesh per FBX uses one stone material slot or the post's three explicit ivory/black/amber slots; the post uses no emission and makes no retroreflection claim.

| Asset | Approximate dimensions (metres) | LOD0 / LOD1 / LOD2 triangles |
|---|---|---|
| VergeWall_A | 2.4 long × 0.75 deep × 0.94 high | 3,564 / 1,452 / 252 |
| Delineator_A | 0.13 wide × 0.194 deep × 1.06 high | 540 / 156 / 60 |

The original wall uses staggered uneven courses, battered sides, worn arrises and varied coping. Source pivots are at base center; Blender Z-up exports to Unity Y-up with wall length on local Z. The Unity admission checks metre bounds, base pivot, readable geometry and exact material submesh counts before any terrain mutation. Shared render meshes are reused by all placements. LOD transitions are screen fractions 0.13/0.045/0.012, then cull.

Actually completed:

- Blender 5.2.1 generated/exported and reimported all six final FBXs. `Review/contract-verification.json` independently verifies their source hashes, material ordering, finite geometry/UVs/normals, nondegenerate triangles and recorded bounds. LOD counts decrease for both assets.
- Inspected `Review/road-detail-reimport.png`, rendered from the reimported FBXs. The initial regular courses were revised before this final render. This is Blender evidence, not a Unity screenshot.
- `python3 artifacts/bfjord-terrain-roads-001/compile.py` passed the whole Editor source compile with warnings as errors against pinned Unity 6000.6 references, without launching Unity.
- The original seven focused `RoadDetailPresentationTests` are part of the 81-test full Editor pass. Two later regressions verify FBX root-axis conversion in transformed bounds and generated mesh baking. The resulting separate scoped run passed **9/9 with 0 skipped in 0.73 seconds**; it is not an 83-test full-suite result.
- `catalog-additions.json` has 20 checked source entries totalling 856,716 bytes. It points to `Assets/BFjord/RoadDetail`, sourced from `assets/BFjordTools/RoadDetail`; the shared catalog was not edited. Public export sanitization preserves source FBX hashes as `sourceSha256` and recomputes `assets[].sha256` for shipped bytes.

## Integration

Copy only the 20 declared source entries from `assets/BFjordTools/RoadDetail/<suffix>` into `assets/BFjordTools/asset-catalog/Assets/BFjord/RoadDetail/<suffix>`, merge the outer catalog, and let the single assembled import run. The scripts require explicit output/source/template arguments, so both repository and exported directory layouts work. `.blend` is local reproducible authoring state; the public package may omit it and regenerate it from the script.

Use the existing configured sandbox and assembled terrain. Before planting the final foliage composition:

```text
bwork_roads action=prepare dressing=true
bwork_roads action=apply dressing=true
bwork_roads action=status
bwork_roads action=view-dressing
bwork_sandbox action=capture name=road-verge-detail
bwork_view view=road capture=road-level-fidelity
```

The accepted fresh-fixture lifecycle completed in 8.260 seconds. It created 47 dressing instances across three roads and one junction, repeated exact geometry/poses/heights/holes, cleaned obsolete generated assets, refused foreign-child and edited-transform destruction, removed without the source catalog while restoring heights/holes/LOD, and retained unrelated subtrees. Unity's FBX root-axis conversion remains part of imported bounds and generated geometry. The [road capture](../images/fidelity-04-road-dressing.png) shows readable segmented stone and grounded wall contact.

This used a separate fresh 512 m fixture because later river cuts in the composed world correctly block replacement of earlier road-owned Terrain samples. It does not demonstrate dressed roads integrated with that river world. The wall remains a restrained procedural candidate rather than a scanned historic structure. Support rejection can create gaps on steep/uneven verges by design. The continuous shoulder's aggregate/grain remains the existing material, and new drainage, lane markings, road patches, loose-stone carpets, vegetation edge blending and wear-specific surfaces are not part of this batch. No physical device or sustained rendering-cost claim is made.
