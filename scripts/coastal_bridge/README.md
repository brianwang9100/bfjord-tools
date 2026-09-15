# Original bridge family assets

The bounded generator builds four original structural designs from dimensions: the accepted open-spandrel coastal arch, a filled-spandrel stone viaduct, a steel Pratt through-truss, and a timber trestle. It produces editable Blender source, three separate triangulated FBXs, a deck collision FBX, metric PBR maps, a strict Unity manifest and a detailed asset report. It regenerates arch ribs, tapered supports, pier foundations, abutments, splayed wingwalls, open parapets, copings, joints, drainage, road paint and sparse reflective studs. This is an art generator for straight, level decks, not civil engineering software.

## Four structural designs and independent materials

Recipe `designType` selects structure and defaults to `coastalArch` for legacy recipes. The other values are `stoneViaduct`, `steelThroughTruss`, and `timberTrestle`. The three new recipes are `stone-viaduct-110.json`, `steel-through-truss-88.json`, and `timber-trestle-80.json`. New outputs use manifest schema 2 with this discriminator; the importer retains schema 1 coastal-arch compatibility. Recipes retain schema 1 and existing metre envelope dimensions, with optional `bayCount`, `pierHeight`, and `trussHeight` for the new families. These are art proportions, not rated engineering capacities.

| Shape | Structural detail | New recipe defaults |
| --- | --- | --- |
| Coastal arch | Twin curved ribs, tapered spandrels, springing towers, open concrete parapets | Existing 180 m / 104 m recipes |
| Stone viaduct | Filled spandrels, full-width barrel arches, projecting radial voussoirs, pier imposts and courses, solid stone parapets | 110 m, five bays, 19 m piers |
| Steel through-truss | Fabricated I sections, sloping end posts, Pratt diagonals, portal knee braces, overhead and underdeck lateral bracing, bearings and gussets | 88 m, eight panels, 8 m truss height |
| Timber trestle | Four-post splayed bents, caps and sills, layered cross bracing, longitudinal bracing/stringers, tie courses and connection plates | 80 m, twelve bays, 16 m pier height |

All designs share the level driving datum and asphalt wearing course. Semantic mesh slots never select a structural design. The new assets add `BridgeStone` → `stone` and `BridgeTimber` → `timber` to the six existing material categories; physical steel members use `BridgeMetal`. Stone and timber default maps are original 1024 px procedural materials, independent of structural recipes and replaceable through the material-profile importer. Coursed-stone coloration is combined with actual near-LOD radial arch-ring blocks. Timber uses sawn-grain maps. No third-party bridge mesh is copied. Lower LODs preserve the main load-path silhouette while reducing joints, section detail, and sampling.

Structural vocabulary was checked against primary public heritage records: [MnDOT's bridge inventory](https://www.dot.state.mn.us/historicbridges/browse.html), the [James J. Hill Stone Arch Bridge](https://www.dot.state.mn.us/historicbridges/27004.html), and [Historic England's timber trestle record](https://historicengland.org.uk/listing/the-list/list-entry/1002126). The existing free Blender mesh and FBX pipeline remains the selected implementation; it produces ordinary Unity meshes/materials without a runtime plugin dependency. These sources are references only, not bundled assets.

## Reproduce

Use the installed Blender **5.2.x**. Run from the repository root. The primary and compact recipes are independent 180 m and 104 m designs; the commands below use the primary. Replace both `coastal-arch-180` occurrences with `coastal-arch-104` for the compact bridge.

```sh
python3 -m unittest discover -s scripts/coastal_bridge -p 'test_*.py'

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --disable-autoexec --python-exit-code 1 \
  --python scripts/coastal_bridge/build_bridge.py -- \
  --recipe scripts/coastal_bridge/recipes/coastal-arch-180.json \
  --output output/bridge-tool/assets/coastal-arch-180 \
  --concrete-source scripts/coastal_bridge/concrete-source.json

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --disable-autoexec --python-exit-code 1 \
  --python scripts/coastal_bridge/verify_export.py -- \
  --manifest output/bridge-tool/assets/coastal-arch-180/manifest.json \
  --report output/bridge-tool/checks/blender-primary-export.json
```

Blender startup in this Mac's restricted process environment crashes during Metal device discovery before Python. The authorized runs used the normal host environment. The generator itself renders with CPU Cycles. It does not install software, access the network or run Unity. `--skip-renders` omits the three review PNGs; use actual Unity images for final visual acceptance.

The input validator rejects unsupported styles, non-finite dimensions, unsafe IDs and impossible proportions before creating output. Publishing writes into a sibling temporary directory and retains the prior package as a hidden sibling backup. These source backups do not belong in runtime or source-control publication. A generation hash covers the canonical design, generator/design source and prepared texture bytes. `asset-report.json` carries file hashes and additional measurements; `manifest.json` contains only the binding import contract.

## Coordinate and rendering contract

The origin is the driving surface midpoint. Unity coordinates are +X right, +Y up and +Z along the road, in metres. FBX imports at unit scale. Blender source uses (X,−road,up); export copies apply the established Bwork handedness conversion, preserving editable source geometry. UV0 values represent metres, and Unity applies `1 / tilingMeters`. UV1 can be generated by the Unity importer for lightmaps. The normal maps are OpenGL tangent-space data; the mask is R metallic/G ambient occlusion/B unused/A smoothness. Colors are sRGB textures; normal and mask textures are data.

`deckWidth` is the full structural slab width, 8 m or 7 m. `clearCarriageway` is 7 m or 6 m; each side reserves 0.5 m for the curb and parapet. The simple collision box covers only the clear driving corridor, from y=−0.2 to 0. Endpoint sockets are at z=±90 m or ±52 m. Footings and wingwalls extend outside the slab width and are included in measured bounds. Placement is translation/yaw only; terrain contact and connecting approaches are scene work.

The coastal arch retains six exact material slots across its meshes: `BridgeConcrete`, `BridgeAsphalt`, `BridgeMetal`, `BridgeConcreteWeathered`, `BridgeRoadPaintAmber`, `BridgeRoadPaintWhite`. Weathered concrete shares the concrete maps and uses a restrained local tint beneath scuppers. Paint is opaque narrow geometry at y=.001–.004 m, breaks around deck joints and persists through the LODs. Raised studs are near-only. Long strips need actual Unity viewing for distant shimmer; source rendering alone cannot establish that result.

The source keeps semantic part groups (Deck, Arches, Supports, Parapets, Details and RoadPaint) and editable bevel/weighted-normal modifiers. FBX contains only evaluated meshes with material slots. Blender may append `.001` to export-copy object names; importer binding uses material names, not those copy suffixes. Near geometry keeps structural bevels. Lower LODs reduce arch sampling, bevels, small caps and drainage while retaining structural proportions and open railing silhouettes. These are independent assembled meshes, not whole-bridge scaled instances.

## Sources and license

- Geometry and placement rules are original Bwork work. The procedural source script uses its stated GPL-3.0-or-later identifier; generated original art is not a copy of a third-party bridge model.
- [ambientCG Concrete034](https://ambientcg.com/view?id=Concrete034), [CC0](https://docs.ambientcg.com/license/), is an approximation material, **not a scan**. The three required source JPEGs and their provenance live under `assets/CoastalBridgeTool/inputs/concrete/`; the packet uses only portable repository-relative paths. The physical source area is 1.1×0.55 m. Resampling to 1024×512 and repeating vertically twice makes a square 1.1 m tile while retaining the source's physical density. AO is white because the source supplies no AO. Smoothness is one minus roughness. Source normal data is preserved.
- [Poly Haven clean_asphalt](https://polyhaven.com/a/clean_asphalt), [CC0](https://polyhaven.com/license), reuses the reviewed RoadQuality maps preserved under `assets/CoastalBridgeTool/inputs/asphalt/`. Tile size is 2.1 m. There is no dependency on the ignored probe or a download during regeneration.
- Paint, metallic accents and the optional fallback concrete are original values/procedural textures. All published assets carry source credits. No NatureManufacture assets, proprietary bridge meshes or reference photography are bundled.

Omitting the concrete packet uses an original procedural fallback. A different licensed local packet can provide `baseColorPath`, `normalPath`, optional `roughnessPath`/`aoPath`, `tilingMeters`, optional `squareTileRepeatY`, and a `source` object containing `name`, `url` and `license: "CC0-1.0"`. Paths resolve relative to that packet. The script expects a tangent-space OpenGL normal and linear roughness, not an engine-packed mask.

## Checks and limits

The pure tests cover recipe bounds, unsupported structural choices, non-finite structural dimensions, and arch/support invariants. The Blender verifier reimports all four FBXs, compares exact triangle counts and bounds to within 1 mm, checks finite UVs/normals, actual up-facing shading corner normals on asphalt and both paint colors at every LOD, positive signed volume, closed geometry, allowed material slots, the up-facing asphalt surface and a flat 12-triangle collider. It verifies published file hashes. These checks complement the review PNGs; they do not prove actual Unity materials, smooth LOD transitions, correct scene contact or physical iPad performance.

The byte-identical final packages and required source inputs are retained in `assets/CoastalBridgeTool/`. Their current recipe hashes include the portable input-path revision and the final explicit corner-normal export correction. Future generator or texture changes receive a new recipe hash. The snapshot normal-correction.json records the original shading bug and corrected per-LOD angle measurements. See the snapshot README and LICENSE.md for ownership, precise CC0 attribution and preservation details.

The finished Unity command, scene and visual review are documented in `docs/BRIDGE_AUTHORING_TOOL.md`. Terrain excavation, curved/graded spans, collision against every parapet/arch surface, engineering certification, device profiling and island installation are outside this tool's current scope.

## Fidelity revision 3

`bridge-families-3` retains all four designs and the same metric deck/collider contract. Near and middle LOD masonry and coastal parapets use separate cap units with 10 mm bedding joints. Stone piers have alternating corner quoins instead of proud horizontal stripes. No foundation footprint changes are required.

Timber side UVs follow each physical member, including diagonal braces and horizontal rails. The original timber finish has restrained silver weathering and longitudinal checking; the separately replaceable metal slot now has original 1 m coated-steel PBR maps with sparse oxidation. Existing concrete and asphalt map scale remains 1.1 m and 2.1 m. Coating roughness and metallic response are read from the packed map in both Blender and Unity.

Use `review_fidelity.py --package PACKAGE --output REVIEW_DIRECTORY` through Blender to capture source silhouette, construction detail and riding views. Run Blender with `--threads 4`. The review script never saves over the source package. See `docs/fidelity/BRIDGES.md` for the comparison and final measured outputs. New original art is covered by the owner's public CC0 grant and `assets/CoastalBridgeTool/FIDELITY-LICENSE.md`.
