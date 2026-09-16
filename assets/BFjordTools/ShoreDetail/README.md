# BFjord shore detail

Six original Blender assets supplement the toolkit's beach surfaces with cockle shells, mussels, fragments, kelp wrack, bladder wrack and bleached driftwood. The geometry and original atlas are CC0-1.0; authoring scripts are MIT. These are authored procedural models, not photogrammetry. The privately viewed commercial reference images supplied art direction only; no pixels, meshes or source assets from those references appear in this family.

The admitted catalog lives under `Assets/BFjord/ShoreDetail/`. `catalog-additions.json` lists every catalog file, byte count and SHA-256 for the root-controlled global manifest merge. `manifest.json` carries bounds, placement and material contracts. `Review/reimport-verification.json` is generated from new imports of the catalog FBXs; the PNGs are Blender reviews, not Unity or iPad acceptance.

| Model file | Mesh children | Typical size / use |
| --- | --- | --- |
| CockleShells_A.fbx | CockleShells_A_LOD0–2 | Two 6.7–8.2 cm cockles, one concave valve |
| MusselShells_A.fbx | MusselShells_A_LOD0–2 | Four 6.5–10 cm dark valves |
| ShellFragments_A.fbx | ShellFragments_A_LOD0–2 | Seven small fragments in a loose patch |
| KelpWrack_A.fbx | KelpWrack_A_LOD0–2 | Ruffled 24–65 cm blades, stipes and smaller torn blades |
| BladderWrack_A.fbx | BladderWrack_A_LOD0–2 | Branched brown wrack with paired attached air bladders |
| BleachedDriftwood_A.fbx | BleachedDriftwood_A_LOD0–2 | Approximately 1.2 m weathered stem and broken branch stubs |

Every FBX holds exactly three meshes, one material slot named `BFjord_ShoreAtlas_Export`, Unity +Y up, metre units and a bottom pivot. Placement ground offset is zero. A small 1–3 mm embed may be used for shells; choose terrain-normal alignment independently. Leave these rigid shoreline debris assets out of live vegetation wind. No colliders, bones or animation are exported. LOD topology reduces deliberately with stable component count and silhouette. The root integration owns LOD transition/culling distances; shells should cull much earlier than large scenery.

Assign `ShoreAtlas_Color.png` as sRGB base color, `ShoreAtlas_NormalGL.png` as a normal map with **no green-channel inversion**, and `ShoreAtlas_Mask.png` as linear data. Mask channels are **R metallic (zero), G AO, B height, A smoothness**. All atlas pixels are opaque and the blades have actual front/back geometry; no alpha clipping is required. UV quadrants use gutters and map shell/wrack/wood details separately. Normal strength 0.7 is the Blender review setting; adjust only after reviewing the actual URP shader. The textures are 2048²; model export uses blank placeholder materials so FBXs contain no machine-local texture path.

Sand revision 0.8 uses [ambientCG Ground052](https://ambientcg.com/view?id=Ground052) by Lennart Demes: a white beach surface captured with photogrammetry at approximately **2 × 2 m**. Its [CC0-1.0 license](https://docs.ambientcg.com/license/) permits modification and redistribution. The exact 2K source archive and five extracted source maps are pinned in `Sources/sand-source.json`; acquisition extracts only color, OpenGL normal, AO, roughness and displacement. The previous Poly Haven Sand02 source remains recorded in `Sources/sand02-legacy-source.json` for history.

The material receives an original **ivory color treatment in linear space** with a soft highlight knee, then one sRGB encode. This is art direction, not a calibrated measurement of white quartz or carbonate. Source grain, small organic deposits, AO, roughness and height stay spatially matched. Normals reconstruct positive Z from source XY, matching Unity's normal import convention. Color is sRGB; normal and R=metallic/G=AO/B=height/A=smoothness maps are linear. No normal green flip. No pure-white clipped channels, metallic sand, baked highlights or extra material layers.

Use `BeachSand_*` at **2 × 2 m**, dry normal strength **0.65**. Dry and wet share these maps; wet color multiplier is **(0.84, 0.82, 0.79)**, normal strength **0.4**, smoothness range **0.22–0.48**. Default wet width/height are **3.5 m / 0.65 m**, requiring both coastal proximity and elevation. The original optional `RippleSand_*` uses a **16.7 cm wavelength / 2.5 mm amplitude** field; it is not painted across the default beach. Root owns shoreline placement and anti-tiling review.

Reproduce sand only with Python 3, NumPy and Pillow:

```sh
python3 scripts/shore_detail/fetch_sand.py
python3 scripts/shore_detail/build_sand.py
python3 scripts/shore_detail/verify_color_encoding.py
```

For a full shell/atlas regeneration, use the original Blender authoring/review scripts. Set `BFJORD_PYTHON` to a Python executable with NumPy/Pillow so full authoring uses the identical sand pipeline. The sand-only command never exports FBX, changes the shell atlas or invokes Unity. `sand08-catalog-additions.json` lists six replacement maps for the root-controlled global catalog merge. Copy the updated family manifest and source receipt to catalog provenance during integration.

`Review/sand08-verification.json` records actual source and output averages and checks exact color encoding, matched source channels, unit normals, nonmetallic output, no clipped white and local/catalog byte identity. `sand08-change-receipt.json` records per-file old/new hashes. See `docs/fidelity/SAND_08.md` for source comparisons and integration limits. Unity shader appearance/compression and physical iPad performance remain separate acceptance work.
