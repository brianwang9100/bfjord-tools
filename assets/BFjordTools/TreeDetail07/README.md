# Tree Detail 07

Three additive original tree families: `GiantRedwood_C`, `CoastalPine_C`, and `MatureBeech_C`. Existing foliage remains unchanged. The source scene contains the reimported FBXs with packed textures; ten source renders show full silhouettes, root collars, canopy details and the family comparison.

The redwood has a tall tapering leader, irregular hanging boughs, a broad fused buttress collar and flattened evergreen sprays. Coastal pine has a leaning trunk, missing windward branches, uneven tiers and explicit three-needle fascicles. Beech uses a forked broad crown, pale mottled bark, fine curved twigs and folded leaves with four color/vein islands. Branch counts, lengths, internodes, leaf roll and terminal directions vary deterministically. Large wood is voxel-unioned, relaxed and reduced; fine branches and foliage retain their own authored geometry.

The initial source review found conifer foliage too thin to read at full-tree distance and excessive triangle cost in small woody tubes. The final pass enlarges the geometric spray coverage, reduces twig cross sections, drops subpixel branches in lower LODs, and distributes more redwood boughs around the leader. Leaf anchors remain deterministic across the three LODs. These are canopy-oriented geometric representations, not botanical scans of individual needles.

## License and provenance

Original geometry, beech bark and foliage textures are dedicated to **CC0-1.0**. Generator scripts are **MIT**. Conifer bark uses the free [Bark Brown 02 scan](https://polyhaven.com/a/bark_brown_02) by Rob Tuytel with different authored redwood/pine tints. Its diffuse, OpenGL normal and roughness maps are copied into `Sources/`; `sources.json` preserves the upstream URLs, file sizes and SHA-256 hashes. The generator verifies every source hash before reading pixels. Poly Haven permits redistribution under [CC0](https://polyhaven.com/license). Powered by Poly Haven.

The reuse check considered [Sapling Tree Gen](https://extensions.blender.org/add-ons/sapling-tree-gen/) and retained the existing original offline authoring workflow for deterministic species structure and LOD anchors. Blender's built-in [voxel remesh](https://docs.blender.org/manual/en/4.5/modeling/modifiers/generate/remesh.html) fuses root and scaffold junctions. Unity needs no new package or runtime generation support: these are ordinary meshes and textures for the existing material and LODGroup workflow. Device cost is still an integration measurement.

Private reference images were viewed only to study the balance of wood and foliage, branch irregularity and root transitions. They are not generator inputs, textures or deliverables. No proprietary mesh or material is copied.

## Rebuild

Run from the repository root with Blender 5.2.1 LTS or a compatible Blender build:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python scripts/tree_detail_07/generate_trees07.py -- \
  --output assets/BFjordTools/asset-catalog/Assets/BFjord/TreeDetail07 \
  --review assets/BFjordTools/TreeDetail07/Review
python3 scripts/tree_detail_07/prepare_catalog.py \
  --catalog assets/BFjordTools/asset-catalog
```

`geometry.py` contains the shared tube, folded leaf, UV and mesh helpers. `--skip-review-renders` skips only renders, not export/reimport checks. `--family` supports a bounded single-family diagnostic build; use the full command above for the canonical combined manifest and Blender scene.

## Integration contract

- Source folder: `Assets/BFjord/TreeDetail07/`.
- Each `Models/<ID>.fbx` contains exactly `<ID>_LOD0`, `<ID>_LOD1`, `<ID>_LOD2`, in metres with FBX Y up. Blender source/reimport coordinates are Z up.
- Each family has one exported material slot and its own `Textures/<ID>Atlas.png`, `<ID>Normal.png`, `<ID>Mask.png`, all 2048 × 1024. Atlas color is sRGB, normal is an imported tangent normal, mask is linear metallic/smoothness with smoothness in alpha. Color alpha is opaque; foliage is geometric, with no large transparent canopy cards.
- Bark occupies the left half; four guarded foliage islands occupy the right half. Bark V repeats every 2.4 metres of branch path. Unity import metadata preserves texture repeat.
- FBX image nodes are unbound during export; Unity assigns the family textures. This prevents absolute local texture paths in public FBXs.
- Use the existing scale-aware root support and exclusion placement. Local Y/Z zero in Unity/Blender is the ground plane; negative collar minima are buried support geometry and must not be treated as placement height.
- `tree07-manifest.json` carries reimported triangles, LOD0 bounds, root footprints, the maximum crown radius across all three LODs, conservative spacing radius, source hashes and geometry checks. Spacing includes 0.5 m beyond the maximum exported crown; multiply by instance scale and retain the existing wind-bound expansion.
- `tree07-catalog-additions.json` contains exact bytes/hashes for the 26 new asset and importer files. Merge its entries into the shared catalog only after integration review. The preparation script does not modify that shared catalog.

Export/reimport verifies nine meshes for finite vertices, normals, tangents and UVs, nondegenerate triangular faces, and available UVs. The full source scene and source renders are separate from Unity acceptance. Some UV direction changes at fused junctions remain visible close up; the wider geometric conifer sprays favor silhouette readability over exact needle dimensions. No hardware compatibility, sustained frame rate, or proprietary reference parity is claimed.

## Version 0.7 source measurements (historical)

| Family | LOD0 / LOD1 / LOD2 triangles | Height above ground | Root footprint radius | Minimum spacing radius |
|---|---:|---:|---:|---:|
| GiantRedwood_C | 147,647 / 80,822 / 15,419 | 20.00 m | 2.32 m | 5.68 m |
| CoastalPine_C | 175,983 / 72,107 / 19,458 | 12.38 m | 1.44 m | 6.90 m |
| MatureBeech_C | 169,486 / 95,185 / 23,548 | 9.65 m | 1.30 m | 5.65 m |

The catalog additions total 101,805,389 bytes of source assets and importer metadata. Source FBX/PNG bytes are not Unity runtime residency. Nine FBX reimport geometry checks and all 26 exact catalog hash checks passed. The three public FBXs have no absolute local paths or embedded image bindings.

## Version 0.8 redwood crown refinement

`GiantRedwood_C` now uses two differently rolled feather sprays at every existing fine-shoot anchor. Each spray has a narrow connecting axis and two ranks of tapered geometric needles. Layering fills the space above and below boughs while preserving the irregular large branch gaps, trunk, fused roots and woody scaffold. The existing single atlas and every texture/importer byte remain unchanged. Pine and beech models, textures and importer metadata are byte-identical to 0.7.

The [Oregon State University species account](https://landscapeplants.oregonstate.edu/plants/sequoia-sempervirens) informed the flat, two-ranked needle arrangement. Real needles are much smaller; this asset deliberately enlarges their geometric representation for canopy readability. The private forest and conifer references informed layered crown volume and visible branch gaps only. No private image data enters the generator or deliverables. The free Sapling alternative was rechecked; the existing Blender mesh workflow keeps original geometry, deterministic anchors and ordinary Unity/URP mesh compatibility without an additional runtime dependency. iPad performance is unmeasured.

Final FBX reimport checks pass for all three redwood LODs: **197,459 / 102,170 / 22,535 triangles** (formerly 147,647 / 80,822 / 15,419). The root footprint remains **2.320036 m**, all-LOD crown radius **5.175258 m**, and minimum spacing radius **5.675258 m**. The exact local bounding box changes to accommodate the sprays, so import the replacement mesh rather than retaining stale mesh bounds. Existing placement exclusion radii need no change. The 26-file local catalog receipt totals **110,498,892 bytes**; source bytes are not runtime residency.

`redwood08-changed-hashes.json` is the explicit old/new receipt for the redwood model, local manifest and local catalog additions. The shared catalog is integrated separately by the root agent. Sixteen pine/beech files were verified unchanged; all 12 model/texture hashes and 26 local catalog hashes pass. `before08-hashes.json` records the pre-change local family receipt.

The full and crown comparison renders use the same camera/light setup: `Review/Before08/GiantRedwood_C-{full,crown}.png` and `Review/GiantRedwood_C-{full,crown}.png`. `GiantRedwood_C.blend` preserves the refined source review; `TreeDetail07.blend` and `Review/tree07-family.png` are refreshed from the frozen FBX. Source renders are not Unity or physical-device acceptance. Root operates the final Unity sandbox review.

For a bounded redwood build, add `--family GiantRedwood_C` to the generator command. This writes a single-family manifest for deliberate reconciliation with `tree07-manifest.json`; it does not automatically overwrite the combined manifest or shared catalog. Refresh the combined source review after reconciling the local manifest:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python scripts/tree_detail_07/refresh_family_review.py -- \
  --source assets/BFjordTools/TreeDetail07 \
  --catalog assets/BFjordTools/asset-catalog/Assets/BFjord/TreeDetail07 \
  --family GiantRedwood_C
```
