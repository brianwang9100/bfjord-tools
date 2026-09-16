# Forest floor detail 07

Seven original specimens for the BFjord catalogue: exposed root fan, oak/birch leaf litter, pine litter with five cones, four brown boletes, six golden chanterelles, shelf fungi on a short dead branch, and a mossy fallen branch. Geometry is original deterministic authoring. It is intended for close foreground detail, with the lower LODs used for ordinary scatter.

All seven FBX files contain three named mesh children (`{ID}_LOD0` through `LOD2`) with one material slot. Exports use metres and Unity Y up. The atlas is `ForestFloor07Atlas.png` (sRGB), `ForestFloor07Normal.png` (tangent OpenGL normal) and `ForestFloor07Mask.png` (linear RGB metallic=0, alpha smoothness), all 2048². Leaves have actual curled surfaces and backs, cones have individual overlapping scales, roots have fused branching collars, and chanterelles have underside ridges following their concave caps. Bolete undersides are smooth pore surfaces rather than gills.

The seven PNGs in `Review` are Cycles renders of FBX files imported into a fresh Blender scene, with the export atlas reassigned. `manifest.json` records every reimport's finite coordinates, nondegenerate triangles, finite tangents, UV presence, LOD costs and exact bounds. These checks and views cover Blender exports; the integrating task owns Unity import, habitat placement and Unity visual evidence. No iPad performance claim is made.

## Placement

`ExposedRootFan_A` has no artificial ground disc and no stump lid. Add it around a compatible approximately 0.4–0.6 m diameter trunk or use as a hollow decayed root crown. The local origin is the trunk axis at the nominal ground plane. Unity bounds are X −1.344…1.638 m, Y −0.140…0.644 m, Z −1.552…1.904 m. Its branch tips deliberately descend to Y ≈ −0.14 m. Fit the tangent to local terrain, then bury lower tips; do not raise the object so its bounding-box minimum rests on ground. On curved terrain use the placement tool's footprint sampling and lower only as needed to hide exposed underground tips. The nonuniform footprint is about 3.0 × 3.5 m; circular sampling should use the radius recorded in the manifest. Avoid extreme crests or steps that exceed the available 14 cm burial margin.

Leaf litter and pine litter should align with the terrain tangent, with nominal local Y=0 on the terrain. Their tiny gaps/curls are intentional. Fungi stems extend about 2 cm below the plane and retain those buried roots. Keep mushrooms upright on mild slopes or use only a restrained tangent alignment. Deadwood's lower surface reaches about −2.6 cm; align its local tangent and bury that lower surface, preserving the short branch stubs. These details are scenery only; collision is unnecessary.

## Provenance and reuse review

The proportional reuse search checked [Poly Haven forest leaves](https://polyhaven.com/a/forest_leaves_02) and the established [Blender FBX export workflow](https://docs.blender.org/manual/en/5.2/files/import_export/fbx_legacy.html). A tiling ground texture cannot provide individual curled leaf silhouettes or mushroom anatomy, so those specimens are authored geometry with original atlas fields. Ordinary FBX and PBR atlases require no third-party Unity runtime package, custom renderer or shader; the integrating catalogue supplies URP materials.

The bark atlas island reuses the already downloaded and SHA-256 verified [Bark Brown 02](https://polyhaven.com/a/bark_brown_02) scan by Rob Tuytel. Powered by Poly Haven. This scan is [CC0](https://polyhaven.com/license); copied source images, URLs and exact hashes are in `Sources/sources.json`. New geometry and original atlas fields are offered under CC0-1.0, and generator code is MIT. No paid assets or additional downloads were needed.

Private NatureManufacture forest-floor and felled-log reference images informed only the visual goals (uneven litter, natural branching, close material detail); none of those reference pixels or source geometry is included in any generated/exported file.

## Reproduce

From the repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b -t 4 --python scripts/forest_floor_detail/build_forest_floor.py
python3 scripts/forest_floor_detail/prepare_catalog.py
```

The builder uses the verified copies under this folder's `Sources`, exports the seven FBXs and textures to the family's isolated catalogue folder, reimports and checks all LOD meshes, renders seven review images and writes `Sources/ForestFloor07.blend`. That file packs the atlas and retains all editable exported meshes. The Python generator retains the procedural shape rules and seeds. `catalog-additions.json` lists bytes and SHA-256 for every family payload and its deterministic `.meta`; it intentionally excludes itself. Shared catalogue registration and final export are owned by integration.
