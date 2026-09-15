# Bridge fidelity pass

## Reference review and decision

Reviewed all four baseline Unity images in `artifacts/bfjord-tools-validation/bridge-collection/`, plus private NatureManufacture `forest-forest-32` (footbridge in a leafy river corridor) and `meadow-HD_08`. The latter is mislabeled “Low stone wall” in the private library: the image actually shows a stacked-log fence. It was used only to study silvery wood weathering, visible end/side grain and restrained wear. The footbridge establishes readable slender members and dense, grounded surrounding vegetation; neither image is a model/template or a source of texture pixels. No private images are included in this document's public evidence.

The baseline already had four distinct structural silhouettes. The useful improvement was local material direction and construction scale: timber grain followed world axes instead of each member; steel was a uniform dark value; coping was a continuous extrusion; printed stone mortar crossed the modeled arch-ring blocks. Bridge-scene banks and vegetation remain separate environment composition work.

A proportional reuse search checked [Blender's bevel workflow](https://docs.blender.org/manual/en/4.4/modeling/modifiers/generate/bevel.html) and [ambientCG's available CC0 timber](https://ambientcg.com/view?id=WoodSiding006). The selected solution remains the existing free Blender 5.2 mesh/FBX workflow, the already admitted [ambientCG Concrete034](https://ambientcg.com/view?id=Concrete034) and [Poly Haven clean_asphalt](https://polyhaven.com/a/clean_asphalt). They require no runtime package. The existing procedural wood is a better fit for narrow sawn structural members than a siding-board surface. Ordinary URP Lit maps and existing LODs remain compatible with the tool's current Unity integration; physical iPad performance is untested.

## Implemented

- Coastal arch: separate near/middle coping units with 10 mm joints; existing bevels, drainage, ribs, support and road detail retained. Both 180 m and 104 m recipes rebuilt.
- Stone viaduct: slightly deeper individual caps and alternating corner quoins. Arch-ring blocks, quoins and caps sample single-stone interiors so printed masonry joints do not cut through modeled units. Existing barrel arches and five-bay silhouette retained.
- Timber trestle: member-aligned metric UVs, including sloped bents, diagonal braces, horizontal caps and rails; restrained original silver weathering and longitudinal checking. Existing connection plates, layered braces and trestle spacing retained.
- All designs: independently replaceable coated-steel PBR maps, 1 m tile, fine coating variation and sparse oxidation. Blender now reads metallic from the packed red channel, matching the existing Unity importer. Concrete remains 1.1 m and asphalt 2.1 m.

The four design discriminators, material keys, deck endpoints, foundation footprint, flat driving datum and 12-triangle deck collider are unchanged. Source version is `bridge-families-3`. No runtime resizing or additional Unity subsystem was introduced. Generator/review code remains GPL-3.0-or-later; new original art has the owner's public CC0 grant, recorded in `assets/CoastalBridgeTool/FIDELITY-LICENSE.md`. Third-party maps retain their own CC0 receipts.

## Reproduction and integration

For each recipe ID (`coastal-arch-180`, `coastal-arch-104`, `stone-viaduct-110`, `steel-through-truss-88`, `timber-trestle-80`), run the following from the source repository root, replacing `VARIANT` with the ID:

```sh
python3 -m unittest discover -s scripts/coastal_bridge -p 'test_*.py'
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python scripts/coastal_bridge/build_bridge.py -- --recipe scripts/coastal_bridge/recipes/VARIANT.json --output output/fidelity-bridges/VARIANT --concrete-source scripts/coastal_bridge/concrete-source.json --skip-renders
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python scripts/coastal_bridge/verify_export.py -- --manifest output/fidelity-bridges/VARIANT/manifest.json --report output/fidelity-bridges/checks/VARIANT.json
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python scripts/coastal_bridge/review_fidelity.py -- --package output/fidelity-bridges/VARIANT --output output/fidelity-bridges/review/VARIANT
```

The host Blender run is required on this Mac because sandbox Metal discovery crashes before Python. CPU rendering uses four threads and 24 samples. The review script reads packed textures and leaves source bytes unchanged; it checks UV V against long timber face edges.

Use the existing source packages under `assets/CoastalBridgeTool/` through the normal toolkit extraction, then `bwork_bridge_collection action=setup` to install matching new generations in the collection scene. Do not reuse old imported generations merely because recipe IDs match. Use `bwork_bridge_collection action=capture name=stone-viaduct-110-riding`, `stone-viaduct-110-side`, `timber-trestle-80-riding`, `steel-through-truss-88-riding` and `coastal-arch-180-riding`, plus the existing four `-hero` cameras for the assembled review. For individual application, the unchanged seam is `bwork_bridge_asset action=prepare|apply|status` with the manifest/placement paths and matching batch ID.

## Final evidence

- Four pure design tests passed; modified Python sources compile.
- All five rebuilt packages passed actual Blender FBX reimport: finite UVs/normals, closed positive-volume geometry, exact descending triangle counts, 1 mm bounds agreement, up-facing asphalt/paint normals, matching hashes and the unchanged flat collider.
- Source CPU Cycles silhouette/construction/riding renders were inspected for all four designs. Timber's audit passed on 4,580 long faces across the three editable LODs. The initial stone review caught and corrected texture mortar crossing modeled blocks; the final close view shows clean radial units.
- Final package copies are byte-identical to their checked candidates; `assets/CoastalBridgeTool/snapshot.json` records 106 package/input files.
- The Unity collection setup and gallery completed in 144.859 seconds with ten captures: hero and riding views for all four designs, the stone viaduct construction side view and an independent warm-concrete coastal-arch variant followed by a successful default-surface reset. See the [0.4.0 validation record](VALIDATION.md). This gallery checks finite imported presentation, not arbitrary bridge fitting, LOD transitions under motion or device performance.

| Package | LOD0 / LOD1 / LOD2 triangles |
|---|---|
| coastal-arch-180 | 57276 / 7460 / 3572 |
| coastal-arch-104 | 38316 / 5180 / 2660 |
| stone-viaduct-110 | 43696 / 7872 / 1056 |
| steel-through-truss-88 | 37812 / 4260 / 1812 |
| timber-trestle-80 | 53460 / 5412 / 3612 |

Recreated local artifact evidence is written under `output/fidelity-bridges/checks/` and `output/fidelity-bridges/review/{design-id}/`. Each review directory contains `silhouette.png`, `construction.png`, `riding.png` and a JSON source-hash receipt. The 104 m coastal recipe shares the reviewed coastal design and passed its own export checks; it was not separately rendered.
