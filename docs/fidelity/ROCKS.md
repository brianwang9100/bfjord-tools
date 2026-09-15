# Rock fidelity review

The original six families remain available. Three new authored forms improve large fracture planes, nonuniform joints, worn edges and stone-size hierarchy. The highland, river-bank and river-shallow presets now use these additions. Waterfall composition can use the same coastal outcrop, ledge and cluster prefabs.

| ID | Unity dimensions, metres (X/Y/Z) | LOD triangles | Collider |
|---|---|---|---|
| coastal_outcrop | 8 / 4 / 5 | 6200 / 2100 / 650 | 160 |
| river_ledge | 6 / 1.8 / 4 | 6200 / 2100 / 650 | 160 |
| fractured_boulder_cluster | 4.8 / 3.2 / 4 | 6200 / 2100 / 650 | 160 |

The reference review examined the publisher's coastal cliff profiles and exposed boulder/ledge groupings on the [Coast Environment](https://naturemanufacture.com/coast-dunes-environment-dynamic-nature/) and [Advanced Rock Pack](https://naturemanufacture.com/advanced-rock-pack-1/) galleries. The useful differences were varied plane sizes, interrupted erosion channels, subordinate stones at the base and mineral detail that did not depend on excessive geometry. No publisher geometry, textures or promotional imagery were used in these assets or exported documentation.

[Blender Cycles baking](https://docs.blender.org/manual/en/latest/render/cycles/baking.html) was reused as the maintained offline high-to-low transfer implementation. [Poly Haven coast rocks](https://polyhaven.com/a/coast_rocks_01) were checked as a free CC0 alternative; original geometry better preserves editable ledge dimensions and the coordinated waterfall footprint. No add-on is required. The previously bundled [ambientCG Rock030](https://ambientcg.com/view?id=Rock030) color remains the CC0 material basis. New color atlases add original broad mineral variation; tangent normals bake original geometry and procedural grain. This is authored stone, not a geological reconstruction or a new photogrammetry scan.

The Blender generator retains oblique fracture planes, uneven joints and erosion at three scales. It bakes 1024px color and OpenGL tangent normals with 12px margins into unique UVs, then preserves those UVs when reducing LODs. The legacy tiled material remains unchanged. Four source bindings produce twenty shared Unity materials across the five existing profiles; no per-instance material copies are needed. `MaterialForVariant(source, variantId, profile)` selects the baked binding, with an omitted manifest `materialId` defaulting to `rock` for legacy sources.

Validation on Blender 5.2.1 LTS passed all 36 FBX round trips: exact declared triangles, decreasing LOD counts, finite vertices/UVs/normals/tangents, welded closed surfaces, bounded dimensions and base contact. The admitted model/texture bytes total 10,819,936 (10.32 MiB). A representative ray grid across the waterfall feeder's authored rectangle found support in the exported river ledge. This does not prove contact on arbitrary terrain; placement keeps the existing sampled Terrain contact pass and exclusion checks.

Review the exported meshes in `assets/BFjordTools/Rocks/Review/fidelity-lods-pbr.png` and `fidelity-lods-clay.png`. The first review identified overly smooth/dark large faces; the final connected refinement increased multiscale erosion and restrained lighter mineral variation. These images are Blender review evidence. The [0.4.0 Unity record](VALIDATION.md) reports nine imported variants, 20 shared material bindings and bounded placement of 130 highland, 150 riverbank and 87 shallow-water rocks; the [fixed-camera capture](../images/fidelity-04-rocks.png) is Unity evidence. Mobile performance and arbitrary-terrain contact remain outside that result.

```sh
blender --background --threads 4 --python scripts/rocks/build_fidelity.py
blender --background --threads 4 --python scripts/rocks/verify_rocks.py
blender --background --threads 4 --python scripts/rocks/verify_ledge_contact.py
blender --background --threads 4 --python scripts/rocks/render_fidelity.py
```

A full source rebuild runs `build_rocks.py` before `build_fidelity.py`. The generator recreates editable compressed Blender sources in `assets/BFjordTools/Rocks/Sources/`. The public snapshot includes the generator, FBXs and maps; binary authoring files are omitted. Provenance also records the locally generated source hashes. Geometry and baked art are CC0-1.0; scripts are MIT. Mobile performance, scan-level appearance, terrain-adaptive mesh deformation and top-selective moss remain unverified or unimplemented. Convex-envelope colliders fill spaces between cluster pieces, so they are scenery collision approximations.
