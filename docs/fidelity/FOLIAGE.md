# Foliage fidelity pass — original botanical detail

September 15, 2026. Four original Blender assets now complement the retained Poly Haven fir, sapling, fern and shrub library. These are modeled plants with authored LODs, an original leaf/petal atlas and the existing bounded GPU wind. This pass does not claim Nature Manufacture parity or device performance.

## Reference review and reuse choice

The three publisher images below were opened from the private reference library at their original saved resolution before modeling. Their pixels are reference-only and are absent from all production and public assets.

| Publisher reference | Observed feature | Response in this pass |
|---|---|---|
| [Coast & Dunes, Rose_01_Tech](https://naturemanufacture.com/coast-dunes-environment-dynamic-nature/) | Rose variants preserve open branch structure, compound leaf clusters and distinct flower color at each maturity | RoseThicket_A uses nine curved canes, lateral twigs, five-leaflet shoots and five-petal flowers; LODs thin complete shoots |
| [Meadow Environment, gallery_02](https://naturemanufacture.com/meadow-environment-dynamic-nature/) | Fine grass, broad leaves and flower heads form different vertical layers rather than an even lawn | MeadowDaisy_A has separate stalks, leaf pairs and domed flower centers; CoastalGrass_A provides folded tapered blades and seedheads |
| [Advanced Foliage Pack, gallery_01](https://naturemanufacture.com/advanced-foliage-pack-1/) | Repeated meadow species form irregular patches with exposed ground between them | Three bounded clustered recipes combine grass/daisy, separate thickets and low WoodSorrel_A groundcover through the existing scatter planner |

A quick free-tool check found [Poly Haven Shrub 01](https://polyhaven.com/a/shrub_01), which is CC0 and remains a suitable scanned shrub candidate. Existing scanned canopy/ferns already cover that role. The owner specifically requested original Blender models, so this pass fills missing botanical forms without adding a downloaded plant dependency. Standard FBX export follows the [Blender FBX documentation](https://docs.blender.org/manual/en/5.2/files/import_export/fbx_legacy.html). Ordinary meshes and the existing URP shader introduce no extra runtime package. Unity import and fixed-camera rendering are recorded in the [0.4.0 validation record](VALIDATION.md); iPad cost remains unmeasured.

## Asset contract

| ID | LOD0 / LOD1 / LOD2 triangles | LOD0 height | Wind amplitude |
|---|---:|---:|---:|
| RoseThicket_A | 7,596 / 2,856 / 1,198 | 1.14 m | 0.035 m |
| MeadowDaisy_A | 3,224 / 1,341 / 444 | 0.69 m | 0.035 m |
| WoodSorrel_A | 1,632 / 921 / 442 | 0.20 m | 0.009 m |
| CoastalGrass_A | 2,740 / 1,226 / 470 | 1.14 m | 0.045 m |

- FBXs contain three `_LOD0/1/2` meshes, metre units, UV0, triangle topology, normals/tangents and one material slot. No collisions, animation, downloaded geometry or alpha-card rectangles.
- Leaves have a folded midrib, tapered/rounded outline and shaped edges. Flower petals and grass blades are actual surfaces. A shared 512×512 sRGB atlas supplies original vein/petal variation in four padded islands, paired with a generated tangent-space vein normal map imported as non-color data. Roughness is 0.79 and metallic is zero. This is modeled and procedural surface detail, not photogrammetry.
- Source roots meet ground within approximately 2.2 mm; recipe burial is 8 mm. Recipe support radii account for horizontal extents, with .82–1.16 scale. The shared shader uses the same root/height across LODs, keeps roots planted and expands mesh bounds by amplitude × 2.04 for the supported minimum .5 scale. The existing forward, shadow, depth and reflection-compatible deformation stays unchanged.
- Names append the existing catalog. Revision 2 reads valid revision-1 receipts, then produces a new generation on rebuild. Existing generated prefabs stay intact for old batch ownership. Placement, road/water exclusions, terrain ownership and removal are unchanged.
- Deterministic import metadata and hashes are in `assets/BFjordTools/asset-catalog`. Original models and atlas together are approximately 2.7 MiB before Unity import; this is source size, not GPU memory usage.

## Actual verification and fidelity gap

Blender 5.2.1 LTS exported and reimported all twelve meshes. The generation run checked triangle-only topology, nonzero triangle area, finite positions, UV presence and finite recalculated tangents. The checked deliverable FBXs were used for the CPU Cycles review render: `assets/BFjordTools/FoliageDetail/Review/exported-lods.png`. Individual `*-detail.png` files are closer views of each reimported LOD0. These are Blender renders, not Unity captures.

Compared with the chosen publisher references, this set adds distinct leaflets, curved blades, visible petal heads and low groundcover. It is still less natural in branch variability, weathering, leaf damage, fine flower anatomy and variation between mature plants. Lower LODs visibly thin botanical elements without crossfading; the single material favors bounded costs over species-specific scanned roughness/normal detail. The assembled Unity fixture placed 177 meadow plants, 54 rose thickets and 247 wood sorrel instances; identical repeat counts and sorrel removal/reapply passed. The [fixed-camera foliage capture](../images/fidelity-04-foliage-patch.png) shows the resulting patch under simple review lighting. Moving-camera LOD/wind behavior and physical-device density remain unverified. The existing catalog of large trees is intentionally retained.

## Reproduce and integrate

From the workspace root:

```sh
blender --background --python scripts/foliage_detail/generate.py -- --output assets/BFjordTools/asset-catalog/Assets/BFjord/OriginalFoliage --review assets/BFjordTools/FoliageDetail/Review
python3 scripts/foliage_detail/prepare_catalog.py --catalog assets/BFjordTools/asset-catalog
blender --background assets/BFjordTools/FoliageDetail/FoliageDetail.blend --python scripts/foliage_detail/review_closeups.py -- --output assets/BFjordTools/FoliageDetail/Review
```

After installing the refreshed asset catalog, run `bwork_foliage action=build-assets`; apply `Samples/meadow-detail.json` as batch `meadow-detail`, `Samples/rose-thickets.json` as `rose-thickets`, and `Samples/wood-sorrel.json` as `wood-sorrel`. All are near the existing forest camera, x196–222 / z174–197. Use `prepare` first if existing scenery is present; these are additional named batches. The recorded run repeated all three applies and removed/reapplied wood sorrel. Inspect height/contact, moving-camera LOD transitions, shaded leaves and road/water clearance when fitting the recipes to another scene.

## Provenance

Models, textures and review renders were authored by the original generator in this change and are dedicated under **CC0-1.0**. Generator and integration source are **MIT**. The editable `.blend` remains a local source artifact; portable scripts plus checked FBX/PNG outputs reproduce it. Private reference images remain copyrighted by Nature Manufacture and are neither asset inputs nor distributable content. There are no new third-party asset bytes in this pass.
