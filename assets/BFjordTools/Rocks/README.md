# BFjord original rock library

Six original geometry families: granite boulder, rounded river stone, stratified
outcrop, fractured cliff slab, scree cluster and upright crag. Each has three FBX
LODs plus a simplified convex collision mesh. The pivot is at bottom center;
dimensions and manifest bounds use Unity meters and +Y up.

Models and newly derived map channels are dedicated under [CC0-1.0](LICENSE.md). Color and
OpenGL tangent-normal maps are resized ambientCG **Rock030** by Lennart Demes,
also CC0-1.0: <https://ambientcg.com/view?id=Rock030> and
<https://docs.ambientcg.com/license/>. This source is procedural, not a photoscan.
The 2048-pixel source maps were resized to 1024 pixels and encoded as JPEG quality
90 for this small shared library. Source and delivered hashes are recorded in
`provenance.json`. Original roughness is an
approximation centered at 0.84, not a measured material property. The packed map
contains metallic 0 in RGB and smoothness in alpha. Textures are shared across
the library; Unity material profiles may tint the neutral gray source.

NatureManufacture's Advanced Rock Pack is a visual/workflow reference only:
<https://naturemanufacture.com/advanced-rock-pack-1/>. Its advertised reusable
rock silhouettes, optimized detail levels and terrain-cover controls informed
the authoring goals. No NatureManufacture files, meshes, textures, or source code
are copied. Poly Haven CC0 stone maps were also considered; the existing verified
ambientCG source avoided a new download dependency.

`Review/library-contact.png` shows the editable high-detail authored shapes.
`Review/verification.json` records actual FBX re-import verification of every
LOD and collider. Final Unity material, import and device costs require their
own checks. Scree collision is one conservative convex hull and intentionally
does not model walkable gaps between individual stones.

The editable `original.blend` is a private authoring file; release packaging
must exclude it and review FBX metadata. The reusable generator is in
`scripts/rocks/build_rocks.py` and uses the MIT license.

Three fidelity families add eroded coastal outcrop, weathered river ledge and fractured boulder cluster: each has 6200/2100/650 triangle LODs, a 160-triangle collider and 1024px baked color/normal maps. The generator writes editable sources into `Sources/`; binary authoring files are not part of the public snapshot, which supplies the generator, FBXs and maps. `provenance.json` records generated file hashes and ambientCG derivatives. `Review/fidelity-lods-pbr.png` and `Review/fidelity-lods-clay.png` show actual exported meshes at all LODs. These are Blender review renders, not Unity or device acceptance.
