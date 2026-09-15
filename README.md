# bfjord-tools

Experimental, recipe-driven Unity 6 Editor tools for authoring bounded outdoor scenes. The repository ships terrain and road tools, four distinct bridge systems, independent bridge surfaces, deterministic foliage, six original rock families, connected river/lake/delta/ocean geometry, and a bounded waterfall fixture.

![Coastal arch bridge in the Unity demonstration scene](docs/images/bridge-arch.png)

This is an early public source release. Scene images on this page are actual Unity Editor captures from the standalone authoring fixtures; the two rock LOD sheets are labeled Blender review renders. They do not establish runtime portability, production integration, or iPad performance.

## What is included

| Family | Authored output | Command |
|---|---|---|
| Terrain | Regional height stamps, smoothing, flattening, thermal relaxation, protected edits, and material painting | `bwork_terrain` |
| Roads | Asphalt, gravel, and dirt ribbons, junctions, shoulders, grade fitting, and owned Terrain conformance | `bwork_roads` |
| Bridges | Coastal arch, stone viaduct, steel through truss, and timber trestle geometry generated at explicit dimensions | `bwork_bridge_asset`, `bwork_bridge_collection` |
| Bridge surfaces | Material profiles applied without rebuilding bridge meshes, colliders, placement, or LODs | `bwork_bridge_surface` |
| Foliage | Seeded species batches with slope, spacing, road, water, and structure exclusions | `bwork_foliage` |
| Rocks | Six original rock families, shared PBR maps, three LODs, and an optional collider per family | `bwork_rocks` |
| River scene | Connected water composed with deterministic bank boulders and shallow stream stones | `bwork_river_scene` |
| Water | Lower-level connected tributary/lake/delta/ocean mesh, Terrain carving, bank fade, and shader motion | `bwork_water_connected` |
| Waterfall | Finite falling sheet and plunge foam with an optional original-rock backdrop | `bwork_waterfall` |

Outputs use ordinary Unity Terrain, meshes, materials, prefabs, and LODGroups. The normal lifecycle is `prepare → apply → status → remove`: prepare computes and validates without publishing; apply owns a bounded replacement; remove restores only still-owned edits. Bridge surface profiles use `reset` instead of `remove`.

The package retains `bwork_structures` as a utility fixture for curved structures, Terrain-hole ownership and lifecycle restoration. The gray bridge/tunnel fixture is removed from the curated scene and is not presented as a finished tunnel art example.

## Quick start

The pinned development baseline is Unity **6000.6.0f1**, URP **17.6.0**, Unity Pipeline **0.7.0-exp.1**, and Python 3. The sample project declares its Unity dependencies. Blender **5.2.x** is needed only when regenerating bridge or rock FBXs; prepared assets are included.

From a clean clone:

```sh
git clone https://github.com/brianwang9100/bfjord-tools.git
cd bfjord-tools
python3 scripts/bfjord.py doctor
python3 scripts/bfjord.py prepare-project --dry-run
python3 scripts/bfjord.py prepare-project
```

`doctor` reports repository/sample inventory only; it does not compile Unity or validate an Editor installation. `prepare-project` copies the two hash-verified asset catalogs into `Examples~/SandboxProject`, reuses exact matches, and refuses conflicting destination files.

Open `Examples~/SandboxProject` in Unity 6000.6.0f1 and wait for package import. With that Editor open, create the isolated sample scene and run a non-mutating terrain preparation:

```sh
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/create-sandbox.json
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/terrain.json
```

The wrapper sees Unity's project lock and uses the resident Pipeline CLI. If the Hub CLI is not at its default macOS location, add `--unity-cli /path/to/unity`. Close Unity to use batch mode, then pass `--mode batch`, `--editor /path/to/Unity`, and a new `--result` path. The wrapper refuses to start a second batch Editor against a locked project.

See the [reproducible walkthrough](docs/QUICKSTART.md) for request anatomy, resident and batch examples, scene targeting, and the sample recipe sequence. Package-level command behavior and family-specific references live under [`Packages/com.bfjord.tools`](Packages/com.bfjord.tools).

## Terrain and roads

![Terrain after a regional authoring pass](docs/images/terrain-after.png)

![Road junction and surface conformance](docs/images/roads.png)

Road, water, structure, and foliage tools share explicit spatial exclusions. This lets a recipe retain planting clearance and bridge contact without treating a whole curved road's bounding box as occupied ground.

## Four bridge systems and independent surfaces

The asset-backed generator creates five prepared examples across four geometric systems: 180 m and 104 m coastal arches, a 110 m stone viaduct, an 88 m steel through truss, and an 80 m timber trestle. Each recipe defines dimensions directly; the importer does not stretch a finished bridge to fit.

| Coastal arch | Stone viaduct |
|---|---|
| ![Coastal arch bridge](docs/images/bridge-arch.png) | ![Stone viaduct](docs/images/bridge-stone.png) |
| Steel through truss | Timber trestle |
| ![Steel through truss bridge](docs/images/bridge-truss.png) | ![Timber trestle bridge](docs/images/bridge-timber.png) |

The bridge surface command binds semantic material slots from a separate profile. Changing concrete, masonry, steel, timber, road, or marking surfaces keeps geometry, collider, placement, and LOD identities intact.

![Alternate warm surface profile on unchanged bridge geometry](docs/images/bridge-warm.png)

Generator source, recipes, and the [licensed asset snapshot](assets/CoastalBridgeTool) are included. Historical binary `.blend` files are omitted pending a separately audited regeneration; the Python generator and JSON recipes remain the editable source.

## Original rocks, foliage and connected water

The original rock library contains granite boulder, rounded river stone, stratified outcrop, fractured cliff slab, scree cluster and upright crag families. Each distributed model has three LODs and a separate simplified collider. The editable Blender files stay outside the public snapshot; the MIT generator, CC0 renderer assets and exact public-output hashes are included.

| Blender 5.2 clay LOD review | Blender 5.2 PBR LOD review |
|---|---|
| ![Blender review render of six original rock families at three LODs](docs/images/rock-lods-blender-clay.png) | ![Blender PBR review render of six original rock families at three LODs](docs/images/rock-lods-blender-pbr.png) |

![Forest and undergrowth authoring fixture](docs/images/foliage.png)

![Lake and planted riverbanks](docs/images/water.png)

[View the connected river, lake, delta, and finite ocean layout](docs/images/water-network.png).

Foliage and rock placement are deterministic for fixed recipes and source catalogs. `bwork_river_scene` is the composed water example: it prepares the rock library, applies connected water, then places independently owned bank and shallow-water rock batches. Each stage commits separately and retains its own recovery state. Connected water is authored geometry plus visual shader displacement. It does not simulate fluid volume, hydraulic erosion, buoyancy, or physical currents.

The 0.3.0 source also includes original periodic river-current, ocean-crest and waterfall maps, their standard-library generator, and a bounded coastal waterfall recipe. The waterfall creates an animated sheet and plunge foam and can place an original cliff-and-stone backdrop from the rock library. The assembled 0.3.0 run verified shader compilation, fixed-camera motion, replacement/removal, foreign-child protection and unchanged terrain. The art remains an experimental sample: the waterfall backdrop is a finite rock composition, without spray particles or fluid coupling. See the [motion-map and waterfall notes](assets/BFjordTools/Water/MOTION.md).

### Animated water and original river rocks

![Original river rocks and animated current in Unity](docs/images/river-rocks.png)

![Original rock-backed waterfall in Unity](docs/images/waterfall.png)

Short fixed-camera Unity captures: [river](docs/clips/river-motion.mp4), [ocean](docs/clips/ocean-motion.mp4), [waterfall](docs/clips/waterfall-motion.mp4). Each clip contains 24 rendered frames over two seconds. The 12 fps playback is an offline capture setting, not a device-performance result.

## Evidence and limits

The validation ledger currently records the 0.2.0 public baseline: **51 of 51 Unity Editor tests** with no skipped tests, a **55.73509-second** combined lifecycle validation, **8 Python CLI tests**, exact restoration/reuse of 162 catalog files, and seven representative request replays. For 0.3.0, **63/63 Editor tests passed**, followed by **5/5 affected waterfall tests** after its final visual changes. Rock lifecycle/dependency checks and a 32.651-second combined water lifecycle/capture run also passed. These timings describe authoring checks, not rendering frame rates. See the [validation ledger](docs/VALIDATION.md) for the measured results.

The current scope is one finite 512 m authoring sandbox and isolated bridge sites. Terrain stitching, whole-world streaming, arbitrary bridge curvature/grade, infinite oceans, fluid simulation, runtime island integration, and mobile performance acceptance are outside this release. The bridges are visual game assets, not structural-engineering designs. The water shader supplies an opaque-depth fallback and the URP transparent-surface declaration used by the fixture; it still does not simulate fluid behavior. Unity Pipeline 0.7.0-exp.1 is an experimental pinned command transport whose availability and API may change. The [art-direction record](docs/ART_DIRECTION.md) describes the current visual limitations and next refinement work; the gallery does not claim NatureManufacture visual parity.

## License split

| Files | License |
|---|---|
| Original Unity C# and shaders, public CLI, documentation, export tooling, and rock Blender generator | [MIT](LICENSE) |
| Blender bridge generator and its supporting Python modules | [GPL-3.0-or-later](LICENSES/GPL-3.0-or-later.txt) |
| Original bridge and rock geometry/art, design recipes, generated rock map channels, and supplied review images | [CC0-1.0](LICENSES/CC0-1.0.txt) |
| Included ambientCG and Poly Haven art and documented prepared derivatives | CC0-1.0 with retained source credits; ambientCG Rock030 is procedural, not scanned |
| Unity Editor, URP, Newtonsoft JSON, and Unity Pipeline | Their own licenses; installed separately |

The bridge generator's GPL license and the rock generator's MIT license do not change the CC0 grant for their distributed artwork output. See [NOTICE.md](NOTICE.md) for exact scopes, [asset credits](docs/ASSET_LICENSE.md), [source provenance](docs/SOURCE_PROVENANCE.md), and [export provenance](docs/EXPORT_PROVENANCE.json). No NatureManufacture source, model, shader, or screenshot is included.
