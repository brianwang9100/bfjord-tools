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

Sand admission uses [Poly Haven Sand 02](https://polyhaven.com/a/sand_02) by **Charlotte Baglioni**, a **2.1 m** scan. The four matched 2K source PNGs are pinned with exact URLs, byte sizes and SHA-256 in `Sources/sand-source.json`, copied as catalog provenance. The scan uses [CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/); [Poly Haven explicitly permits asset redistribution](https://polyhaven.com/license). Only asset maps are redistributed, not supplier example renders or website content. This source provides close granular detail and small footprints. The original 17.5 cm wavelength, 9 mm amplitude ripple modulation is an optional authored treatment, not a second scan or a coastal simulation.

Use `BeachSand_Color.png`, `BeachSand_NormalGL.png`, `BeachSand_Mask.png` at **2.1 × 2.1 m**. B contains the actual scan displacement; G and A derive from the matched scan AO/roughness. `RippleSand_*` adds a periodic ripple field to that same source normal/height. Dry and wet layers can share a set. Suggested wet shading: base color multiplier `(0.62,0.64,0.65)`, normal strength 0.5, smoothness remapped to 0.42–0.72. Wet sand remains nonmetallic. Root owns shoreline gating, terrain height blending and water coverage.

Reproduce from repository root:

```sh
python3 scripts/shore_detail/fetch_sand.py
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 4 --python scripts/shore_detail/build_shore.py
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 4 --python scripts/shore_detail/review_shore.py
python3 scripts/shore_detail/verify_color_encoding.py
```

Requires Blender 5.x with NumPy and local Python 3 with Pillow for lossless PNG optimization. The saved `Sources/ShoreDetail.blend` uses paths relative to its own directory. No Unity Editor is invoked. FBX/texture bytes are frozen only after these authoring/review commands finish. Unity shader appearance, texture compression, memory costs and physical iPad performance remain integration acceptance work.

Color-only correction is reproducible with the build command followed by `-- --colors-only`; it preserves model, normal and mask files. The color arrays already contain encoded sRGB values and must not be manually decoded before PNG saving. `verify_color_encoding.py` compares the packed sand to the supplier scan, allowing at most one 8-bit quantization step per channel. This guards the double-decoding defect found during the assembled Unity shoreline review.
