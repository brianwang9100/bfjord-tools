# Asset-backed forest edge

This layer adds a small coherent temperate forest library and repeatable planting recipes. It uses retained CC0 Poly Haven models and the original Bwork foliage deformation shader, adapted to the independent Editor sample. It does not modify the production island or application.

## Prepare the library

Restore `assets/asset-catalog` into the explicitly configured Unity project with its hashes and `.meta` files intact. The catalog needs the original `PolyHaven` closure and the `PolyHavenMatureFir` closure. Source locations follow `natureRoot` and `matureFirRoot` in `ProjectSettings/BfjordTools.json`.

With the authoring sandbox open:

```text
bwork_foliage action=catalog
bwork_foliage action=build-assets
```

`catalog` is read-only. `build-assets` produces ordinary shared meshes, materials, prefabs and LODGroups under the configured sandbox generated root. An unchanged input fingerprint reuses the library. Complete new generations publish only after every prefab succeeds; previous generations remain available to existing batches. This deliberately does not purge assets that saved scenes may still reference.

The library provides `bfjord:MatureFir_A`, `bfjord:pine_sapling_small_b`, `bfjord:fern_02_a` through `d`, `bfjord:shrub_03_a` through `d`, and `bfjord:rock_moss_set_01_rock01` through `06`. The existing four prefab paths and older recipes remain supported.

There are sixteen variants, not sixteen species. The scanned `shrub_03` models are small ground plants, approximately 0.26–0.4 m high. The pine sapling is approximately 1 m high; scaling it into a mature tree is not an authored canopy solution. The retained mature fir is approximately 19 m high. Fern variants provide meaningful shape differences at roughly 0.2–0.43 m height.

## Compose the sample

Apply the connected terrain, roads and water first so their owned exclusion geometry protects roads, banks and structures. Then apply the named batches:

```text
bwork_foliage action=apply batchId=canopy recipePath=Packages/com.bfjord.tools/Samples/forest-canopy.json
bwork_foliage action=apply batchId=young recipePath=Packages/com.bfjord.tools/Samples/forest-young.json
bwork_foliage action=apply batchId=floor recipePath=Packages/com.bfjord.tools/Samples/forest-floor.json
bwork_foliage action=apply batchId=herbs recipePath=Packages/com.bfjord.tools/Samples/forest-herbs.json
bwork_foliage action=apply batchId=rocks recipePath=Packages/com.bfjord.tools/Samples/forest-rocks.json
bwork_foliage action=apply batchId=bank recipePath=Packages/com.bfjord.tools/Samples/riverbank-ferns.json
bwork_foliage action=view
bwork_sandbox action=capture name=forest-edge
```

The bank recipe requires applied connected water. The other five work with ordinary terrain and whatever owned geometry is present. Their designed region is the western forest edge around x=90–255 m, z=120–310 m, with a clearing near (220,195). The dedicated `view` uses a low camera near that clearing. The original `bwork_view view=forest` bookmark still looks at the older eastern planting example.

Use `action=prepare` with the same batch/recipe arguments for read-only placement planning and rejection counts. Preparation checks that required prefabs are available; it does not build the asset library. `status` and selective `remove` retain their existing behavior. Old `forest`, `scrub` or `rock` sample batches can be removed explicitly if a clean presentation is desired; the new commands never erase them automatically.

## Recipe additions

| Field | Meaning |
|---|---|
| `clusterCount` | Zero preserves the existing uniform candidate distribution; 1–128 creates deterministic elliptical patches |
| `clusterRadius` | Patch radius in metres, 1–200 when clustering is enabled; patch orientations vary deterministically |
| `spacingGroup` | A short name enabling spacing against existing named batches in the same group; empty preserves independent legacy batches |
| `maximumWaterDistance` | Zero disables the filter; positive values restrict planting to this distance outside an applied connected-water footprint |
| `maximumBankHeight` | Maximum ground height above the sampled water surface for the bank-distance filter; prevents distant/elevated ground being treated as wet bank |
| Species `groundOffsetMeters` | Bounded source-specific burial/planting offset, scaled with the instance; default zero |

Canopy and young trees share `woody`; their supporting footprints cannot overlap. Fern batches share `ground`. Herbs and rocks have separate groups so intentional vertical layering remains possible. Cross-group overlap is an art choice; the hard road/water/structure exclusions apply regardless of spacing group.

Clusters do not bypass terrain bounds, grade limits, height limits or full-footprint exclusions. `maximumCount` and the existing finite attempt budget remain enforced. Sparse or constrained areas may produce fewer instances than requested; receipts report rejection counts rather than silently exceeding constraints. Ground and bank fields are artistic placement controls, not ecological or hydraulic simulation.

## Wind, LODs and asset ownership

Vegetation materials use `BFjord/Foliage Wind`. Root-relative bending and the matching deformation normal/tangent field are shared by forward, shadow, depth and depth-normal passes. All instances read the same Unity time; there is no per-plant update component or per-frame CPU mesh generation. Sun-backlit transmission is restrained and limited to alpha-clipped vegetation. Rocks retain their rigid materials.

The maximum horizontal deformation is bounded by `amplitude × sqrt(1 + 0.18²)`. Generated vegetation meshes preserve their source vertex/index content and enlarge their stored bounds for a minimum supported instance scale of 0.5. The recipe resolver rejects smaller scales for generated wind prefabs. Every mesh within a model must share the same root basis so all LODs and parts use one rooted field. Existing source meshes/materials are never changed.

Three supplied LOD representations are retained. Some fern source LODs intentionally have equal triangle counts; the library does not invent a lower-detail mesh merely to satisfy a descending-count check. This release uses ordinary LOD switches rather than enabling crossfade shader cost globally. Crossfade quality, moving-camera transitions and actual device costs need visual/platform measurement.

The mature fir remains expensive: the retained source preparation records about 104,302 / 57,411 / 35,847 triangles across its three LODs. Its middle/far foliage preserves alpha cards instead of arbitrary decimation. The canopy recipe therefore targets a modest grove, not a dense mobile forest. The source bundle adds about 13.6 MB on disk; disk bytes are not a GPU-memory measurement. Neither the asset counts nor Editor captures establish iPad performance.

## Sources and verification boundary

- [Poly Haven Pine Sapling Small](https://polyhaven.com/a/pine_sapling_small), [Fir Tree 01](https://polyhaven.com/a/fir_tree_01), [Fern 02](https://polyhaven.com/a/fern_02), [Shrub 03](https://polyhaven.com/a/shrub_03), and [Rock Moss Set 01](https://polyhaven.com/a/rock_moss_set_01): retained prepared assets under [CC0](https://polyhaven.com/license). Exact provider files, author credits, hashes and preparation records live in the catalog's `credits/` directory.
- The original shader derives from `WorldFoliageWind.shader/.hlsl`; it is separately licensed project code, not part of the CC0 image/model grant. No private application code or native-time integration was copied into the package.
- [Unity Shader Graph Production Ready samples](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.6/manual/Shader-Graph-Sample-Production-Ready-Detail.html) were checked as a free existing alternative. They provide foliage wind/transmission and efficient details; this pass retains the already established original common-pass deformation implementation to avoid a larger sample dependency. No Unity sample shader source was copied.

Focused planner tests cover deterministic clustered output, footprint spacing across batches, authoritative terrain/exclusions and invalid controls. Source compilation is separate from actual Unity shader import, scene composition, visual quality and runtime performance. The coordinator performs the assembled Unity review and records its actual images/results.
