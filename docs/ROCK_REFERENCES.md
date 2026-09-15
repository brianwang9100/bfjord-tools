# Rock references and acceptance criteria

This note guides the original BFjord rock assets and authoring workflow. The visual goal is believable temperate-fjord cliffs, boulders and stream stones with useful LODs. It is not a NatureManufacture parity claim, and no paid model, shader, texture or vendor image is part of the work.

## Reference package

Unity asset ID `101721` resolves to NatureManufacture's [Advanced Rock Pack 2.0](https://assetstore.unity.com/packages/3d/environments/advanced-rock-pack-2-0-101721). The publisher's older portfolio calls the same listing [Advanced Rock Pack](https://naturemanufacture.artstation.com/projects/v1zYmY), while its site labels the earlier presentation [Advanced Rock Pack 1](https://naturemanufacture.com/advanced-rock-pack-1/). Those publisher pages describe the ideas worth studying: a reusable family of models, scale variation, tileable terrain materials, automatic ground/snow/moss cover, and atlased metallic/AO/smoothness data. They also make mobile-optimization claims, but do not establish current Unity 6, URP or iPad performance. Use the pack only as workflow and presentation inspiration.

For BFjord, the important result is a small coherent library whose pieces share geology and materials while retaining distinct silhouettes. Terrain contact, cover and wetness should be controllable by authored masks or world-space fields. They must not be inseparable effects hidden in a vendor-specific shader.

## Shape language

- **Cliffs:** build broad load-bearing masses first, then a dominant joint direction, secondary fractures, shelves and a few undercuts. Repeat fracture logic across adjacent modules so they read as one formation. Put angular talus below plausible release faces. Avoid evenly distributed noise, repeated teeth and vertical walls made from enlarged boulders.
- **Boulders:** use asymmetric weight, two to four readable fracture planes, softened exposed corners and a broad buried footprint. A boulder should have an obvious resting orientation and remain convincing in a clay render. Rotation and modest non-uniform scale can extend a family, but cannot replace distinct source silhouettes.
- **Stream stones:** favor flattened or subrounded forms, with different degrees of wear. Keep some planar ancestry instead of making eggs or spheres. Half-bury stones, cluster sizes, align some long axes with flow, and reserve darker/smoother response for believable wet or splash zones rather than painting every stone uniformly glossy.

This direction follows observable geology rather than a noise recipe. [NPS rockfall guidance](https://www.nps.gov/yose/learn/nature/rockfall.htm) relates cliffs, slabs and talus to fractures, weathering and freeze-thaw. [USGS work on fluvial gravel](https://pubs.usgs.gov/publication/70031076) shows that roundness varies with lithology and transport history; a stream bed should therefore contain a distribution of angular, subrounded and rounded stones.

## Blender production guidance

Separate three scales of information. Macro form controls silhouette and grounding; meso form supplies planes, ledges and major chips; micro detail belongs mainly in normal, roughness and color maps. Starting from a squashed block or deliberately uneven convex cage makes the first two scales easier to control than repeatedly displacing an icosphere.

Use low-frequency, directional displacement sparingly and mask it by family or face orientation. Blender's [Displace modifier](https://docs.blender.org/manual/en/5.0/modeling/modifiers/deform/displace.html) supports texture-driven normal, axis and vector displacement; its presence is not a reason to apply equal noise at every scale. Voxel remesh can clean intersecting blockout geometry, but Blender documents that it creates uniform topology, so preserve or rebuild important planes after remeshing ([Remeshing](https://docs.blender.org/manual/en/dev/modeling/meshes/retopology.html)).

Create the final game mesh from the approved high form. Protect silhouette, base contact, UV seams and major plane boundaries during reduction. Blender's [Decimate modifier](https://docs.blender.org/manual/en/5.2/modeling/modifiers/generate/decimate.html) can reduce complex sculpted geometry; its planar mode is particularly relevant to rock faces, but every output still needs visual inspection. Bake tangent-space normals and AO from high to low with a checked cage and padded UV islands; Blender's [baking documentation](https://docs.blender.org/manual/en/5.0/render/cycles/baking.html) explains Selected to Active, cages and mip-safe margins.

The open-source [Extra Mesh Objects](https://extensions.blender.org/add-ons/extra-mesh-objects/) extension is a useful generator baseline and its Rock Generator source is [GPL-2.0-or-later](https://github.com/blender/blender-addons/tree/main/add_mesh_extra_objects/add_mesh_rocks). Do not copy GPL source into the BFjord tool. A generated primitive still needs the family-specific art pass above; one-click craggy output alone is not an accepted asset.

## Free reference and source options

All Poly Haven assets are [CC0](https://polyhaven.com/license). The existing public snapshot already includes [Rock Moss Set 01](https://polyhaven.com/a/rock_moss_set_01), a six-shape, eight-meter set with coherent scanned color/normal/roughness data. It is the closest licensed quality baseline for boulders and should remain clearly credited.

[Rock 09](https://polyhaven.com/a/rock_09) is a compact weathered-boulder reference with PBR maps and 23,000 source triangles. [Coast Rocks 01](https://polyhaven.com/a/coast_rocks_01) is a useful large coastal-formation study, but its one-million-triangle source is not an iPad-ready mesh and would require an original low mesh, careful bake and measured import. ambientCG's existing [Rock030](https://ambientcg.com/a/Rock030) is a tileable material source, not a substitute for modeled cliff structure; ambientCG documents its assets as [CC0](https://docs.ambientcg.com/license/).

Prefer the already reviewed Rock Moss set and original BFjord geometry before adding another dependency. Record the exact source URL, author, license, downloaded-file hash and any texture conversion for every accepted third-party byte.

## Acceptance review

An asset family is ready for Unity review when all of the following are visible in evidence:

- Neutral clay turntables show distinct, non-spherical silhouettes from several angles, readable planes, and a plausible flat or buried contact region.
- Neutral PBR renders retain broad value structure. Micro noise does not erase the meso planes, moss respects upward/sheltered areas, and wetness follows a water or splash boundary.
- Each asset has real LOD meshes with strictly decreasing triangle counts. The current prepared Rock Moss precedent is roughly 5,000–11,000 triangles at LOD0, 2,500 at LOD1 and 640 at LOD2 per boulder; treat this as a comparison point rather than a universal budget.
- A wireframe/solid LOD montage shows that the outer contour, dominant planes, pivot and ground footprint remain stable. UVs, tangent normals and material identity survive every LOD.
- A moving road-level Unity capture exercises screen-relative LOD transitions. Popping, lighting flips, floating bases and disappearing fracture landmarks are rejection issues; Unity's [LOD Group](https://docs.unity3d.com/6000.0/Documentation/Manual/class-LODGroup.html) uses screen-relative height, so thresholds must be judged in the actual riding camera.
- A composed scene shows cliffs attached to terrain, talus related to cliff faces, boulders partly embedded, and stream stones distributed by bank/flow context. Random scatter alone is insufficient.
- Repeated assets share materials and support instancing without making repetition obvious. Scale, rotation, cover and wetness variation stay bounded and deterministic from the recipe seed.

Final art review should compare the clay turntable, PBR turntable, LOD montage and one road-level composition under the same neutral daylight and exposure. A flattering hero frame cannot replace those checks.
