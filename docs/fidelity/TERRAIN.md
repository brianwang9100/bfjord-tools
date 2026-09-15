# Terrain fidelity pass — September 15, 2026

The agent viewed the actual `terrain-finish.png` and `river-motion-t0.png` baseline captures and these private Nature Manufacture references: `coast-1-2` (cliff/inlet), `coast-Ground_11` (ground palette), `forest-forest-31` (woodland ground) and `mountain-Mountain-Environment-Dynamic-Nature-gallery-70` (rocky woodland water edge). They are design references only; private images and commercial assets were not copied into production output.

The baseline has broad smooth hills, uniformly faded surface boundaries and nearly continuous plain banks. The references show exposed vertical faces meeting loose aprons, small stone/soil patches within larger vegetation patches, and irregular ground transitions. New foliage and the rock agent's ledges address objects above that ground. A second terrain mesh or another Blender rock library would duplicate their ownership and bypass the native Terrain brush workflow, so this family adds material assets and bounded original height stamps instead.

## Delivered behavior

- Four actual CC0 scan masks supply metallic/occlusion/height/smoothness to native Terrain Lit. Each mask is 512² and uses the same source/UV orientation as the existing base and normal map. Height comes from the published displacement map, never albedo brightness. Physical repeats remain 2m meadow, 3m soil, 2.5m talus and 90m outcrop.
- Height blending is enabled with an adjustable transition. Terrain remains four layers; no multipass layer expansion or custom shader. `heightBlend=false` keeps the ordinary splat blend while retaining valid AO/roughness masks.
- Domain-warped larger patches and 3.7m breakup vary the bank's width and soil/stone mixture. Both signed water distance and height above the water remain required. These are authored classification signals, not hydrology.
- `eroded-ridge` adds coherent incisions away from a crest; `coastal-bluff` adds a scalloped face, uneven crown and loose apron. They are optional built-in recipes, leaving the original ridge/basin/mesa shapes intact. The native mask/thermal-relaxation/smoothing solver still owns every height edit. They preserve road/water/structure exclusions and stored-grid rollback.
- Terrain material changes now join the paint snapshot, including shader keywords and assigned template. Automatic painting rejects an externally owned material as it already rejects external palettes.

## Sources, artifacts and commands

Primary reuse check: [Unity Terrain Lit](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shader-terrain-lit.html), the installed URP TerrainLit shader/GUI source, and [Poly Haven's CC0 terms](https://polyhaven.com/license). Native Terrain Lit was the suitable existing solution; no additional package or purchase is needed. Terrain height blending supports the current four-layer setup; this pass does not claim device performance or exact scanned physical relief.

Reproducible asset pack: `assets/BFjordTools/TerrainDetail/{sources,manifest,catalog-additions}.json`, `scripts/terrain_detail/build_masks.py`, and `assets/BFjordTools/asset-catalog/Assets/BFjord/TerrainDetail/*`. Four PNGs total roughly 2.22 MiB including their import metadata. Texture content is CC0; packing and original stamp code are MIT. Source PNG packing is deterministic and records file/channel verification. No new mesh, pivot, collider or mesh LOD contract is introduced; ordinary Terrain LOD and texture mipmaps remain responsible for distance detail.

After installing the new catalog, apply appearance to the existing scene with `bwork_terrain action=paint recipePath=<package>/Samples/terrain-paint-woodland-bank.json`. That command does not alter heights. The optional `bwork_terrain action=prepare stampShape=eroded-ridge` and `stampShape=coastal-bluff` preview bounded height changes; apply a chosen shape before rebuilding dependent roads, water and planting. They are examples for the existing 512m sandbox, not a whole-island rebuild.

## Evidence and remaining review

The source masks downloaded with matching pinned provider hashes. Two offline runs of `build_masks.py` produced identical mask bytes; every height channel has actual variation, all metallic channels are zero, and import metadata marks the maps linear with mipmaps. The candidate imported without compile errors and its full Editor suite passed. The assembled paint pass retained all 66,049 Terrain height samples; see the [0.4.0 validation record](VALIDATION.md) and [fixed-camera terrain capture](../images/fidelity-04-terrain.png).

The optional `eroded-ridge` and `coastal-bluff` stamps were not part of that capture, and physical-device performance remains unverified. Native Terrain cannot represent an undercut cliff; use the rock family's coastal outcrops/river ledges for overhanging silhouette/contact where a heightfield cannot supply it. Dense forest litter and roots remain foliage/rock dressing work, rather than fabricated changes to the authoritative water or roads.
