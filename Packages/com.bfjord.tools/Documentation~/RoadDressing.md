# Optional road verge dressing

Use `bwork_roads action=apply dressing=true` to add original dry-stone wall fragments and asphalt-side delineator posts. The original RoadDetail asset catalog must be installed. `prepare dressing=true` computes the supported count without changing Terrain; `status` includes `dressingInstances`. The default remains undressed on each apply, including existing recipes.

The wall is approximately 2.4m long, 0.75m deep and 0.94m tall; the post is 1.06m tall. Three ordinary LODs reduce walls from 3,564 to 1,452 to 252 triangles, and posts from 540 to 156 to 60. One 512px original mineral texture pair is shared. Models and textures are CC0-1.0; generators are MIT. No additional physics collider or runtime system is created.

Dressing lives outside the rendered road verge and skips junction mouths, protected spans, other roads, actual water/structure footprints, terrain holes and unsuitable support. A tilted footprint test admits the entire base before applying heights. Short separated wall groups leave open margins. The finite maximum is 384 placements. Terrain, accepted route topology, road collision and material scale remain unchanged.

Meshes and materials are copied into the road's generated asset receipt and shared across instances. Subsequent foliage uses their actual triangle footprints. Reapplying/removing roads deletes only the admitted road generation; new hierarchy seals reject edited or foreign children before deletion. Legacy undressed receipts have a strict migration check. Source FBXs are needed to create dressing, but not to remove it.

`bwork_roads action=view-dressing` positions the sandbox camera by the first supported wall, then `bwork_sandbox action=capture name=road-verge-detail` captures it. The regular `bwork_view view=road` remains available. The local reimport review demonstrates source geometry; final Unity appearance and device performance require the corresponding integration review.
