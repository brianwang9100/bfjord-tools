# bfjord-tools

Experimental, recipe-driven Unity 6 Editor tools for authoring bounded outdoor scenes. The repository ships terrain and road tools, four distinct bridge systems, independent bridge surfaces, deterministic foliage, nine original rock families, connected river/lake/delta/ocean geometry, a bounded waterfall fixture, and an optional original masonry tunnel portal.

![0.6 Unity woodland with additive oak B and birch B variants](docs/images/fidelity-06-woodland.png)

The **0.6.0 sandbox iteration is complete**. It adds stochastic Terrain sampling with a native toggle, root-footprint grounding for upright trees, two additive tree variants, broken river streaks and torn ocean foam. Compare actual Unity stills and motion clips in the [0.6 nature-detail record](docs/fidelity/NATURE_06.md). The combined milestone passed **157/157 Editor checks**, followed by six focused terrain checks after the final refinements.

Terrain sampling costs up to **36 material samples versus 12** for four layers. Native Terrain Lit remains selectable with `antiTiling=false`. This pass retains native XZ projection; it adds no triplanar mapping or device-performance claim. Water retains its existing texture-sample count and displacement bounds. B tree variants preserve existing A-model batches and prefab generations.

This is an early public source release. Scene images are labeled by evidence version; images identified as Blender reviews show source assets rather than Unity integration. Neither kind establishes runtime portability, production integration, or iPad performance.

The previous **0.5.0 implementation and visual review are complete; the source is published in bfjord-tools**. It adds original oak, birch, hollow fallen wood and tall grass; asphalt markings; river-flow presets and beach surf; seven Terrain surfaces in four palettes; and explicit seeds for built-in height stamps. The assembled scene records 19 mixed-canopy, 17 deadwood and 174 mixed-grass placements with all 66,049 sampled Terrain heights unchanged. The woodland, deadwood, grass wind and terrain-palette captures have been reviewed. Road markings, the same-camera river comparison and larger foamy beach waves are also reviewed.

The recorded integration snapshot passed **133/133** in **103.54 seconds**, with zero failures, skips or inconclusive results; the separate CLI suite passed **8/8** in **0.537 seconds**. Source integrity checks passed for all 248 catalog entries. River and beach motion clips and the labeled nine-rock comparison are complete. The final assembled-road and water-refresh lifecycle checks passed in 17.206 seconds. Version 0.5.0 is available in the public repository. See the [0.5.0 nature-detail record](docs/fidelity/NATURE_05.md).

After that snapshot, source compilation remained clean and scoped Editor regressions passed separately: **RoadMarkingTests 9/9 in 11.36 seconds** and **WaterSurfaceOwnershipTests 5/5 in 5.41 seconds**. Seven test cases were added after the snapshot. The road checks cover deferred URP normalization for yellow/white paint and exact legacy-receipt proof; water checks prevent retirement of shared surface assets. These scoped runs are not added to the 133-test total.

The published **0.4.0** evidence remains versioned separately: 81/81 Editor tests passed with no skips, followed by the scoped road tests, and its bounded assembly, water, bridge and road/tunnel checks completed. See the [historical 0.4.0 validation record](docs/fidelity/VALIDATION.md) and [0.2.0 / 0.3.0 ledger](docs/VALIDATION.md).

## Previous 0.5.0 Unity review

These are actual Unity captures from the previous 0.5 assembled sample. The new tree silhouettes and ground-cover density are visible; procedural timber and the barren steep-hill palette fixture remain unfinished art context.

| Fallen wood | Grass in the assembled scene |
|---|---|
| ![0.5 Unity capture of original hollow fallen timber](docs/images/fidelity-05-deadwood.png) | ![0.5 Unity capture of tall grass mixed with coastal grass and daisies](docs/images/fidelity-05-grass.png) |

[Watch the actual Unity grass-wind clip](docs/clips/fidelity-05-grass.mp4): 48 captured frames over four seconds, encoded at 12 fps. This is a capture rate, not a device-performance result.

Terrain palette views use the same camera: [woodland](docs/images/fidelity-05-terrain-woodland.png), [coast](docs/images/fidelity-05-terrain-coast.png), and [cliff](docs/images/fidelity-05-terrain-cliff.png). The bank contains seven surfaces and selects four layers per palette. The original temperate palette remains available.

| Surface-following asphalt markings | Larger foamy beach waves |
|---|---|
| ![0.5 Unity asphalt road with double yellow center and white edge lines](docs/images/fidelity-05-road.png) | ![0.5 Unity foamy beach waves within the simple shoreline fixture](docs/images/fidelity-05-beach.png) |

The three river appearance presets use the same camera, shader time (2 seconds) and pattern seed:

| Low flow | Medium flow | High flow |
|---|---|---|
| ![0.5 low-flow river appearance](docs/images/fidelity-05-river-low.png) | ![0.5 medium-flow river appearance](docs/images/fidelity-05-river-medium.png) | ![0.5 high-flow river appearance](docs/images/fidelity-05-river-high.png) |

The larger beach waves remain a bounded surface with crest/contact foam; they do not create overturning lips or volumetric spray. [Watch the actual Unity river clip](docs/clips/fidelity-05-river.mp4) (48 unique frames, 4 seconds) and [beach clip](docs/clips/fidelity-05-beach.mp4) (72 unique frames, 6 seconds). Both are 1440×960 and encoded at 12 fps; these are offline capture settings rather than rendering-performance measurements.

![0.5 Unity labeled comparison of the nine established original rock forms](docs/images/fidelity-05-boulders.png)

The nine-rock comparison reproduced its placement data exactly and retained all 66,049 Terrain height samples in an 8.511-second review command. It displays the existing nine families; it does not add nine new rock families. `bwork_rock_showcase` saves the seed controlling scale, yaw and slight spacing variation.

## What is included

| Family | Authored output | Command |
|---|---|---|
| Terrain | Seeded regional height stamps, smoothing, flattening, thermal relaxation, protected edits, seven surfaces / four palettes, four-layer height blending, and optional stochastic sampling | `bwork_terrain` |
| Roads | Asphalt, gravel, and dirt ribbons, junctions, shoulders, grade fitting, owned Terrain conformance, asphalt center/edge markings, and optional verge walls/posts | `bwork_roads` |
| Bridges | Coastal arch, stone viaduct, steel through truss, and timber trestle geometry generated at explicit dimensions | `bwork_bridge_asset`, `bwork_bridge_collection` |
| Bridge surfaces | Material profiles applied without rebuilding bridge meshes, colliders, placement, or LODs | `bwork_bridge_surface` |
| Foliage | Seeded species batches, ten original botanical/woodland variants, retained scanned variants, authored LODs, root-footprint grounding, and slope/spacing/road/water/structure exclusions | `bwork_foliage` |
| Rocks | Nine original rock families, shared or baked PBR maps, three LODs, and an optional collider per family | `bwork_rocks` |
| River scene | Connected water composed with deterministic bank boulders and shallow stream stones | `bwork_river_scene` |
| Water | Lower-level connected tributary/lake/delta/ocean mesh, Terrain carving, downstream flow presets, bank fade, beach surf, and shader motion | `bwork_water_connected` |
| Waterfall | Finite falling sheet and plunge foam with an optional original-rock backdrop | `bwork_waterfall` |
| Structures | Curved bridge/tunnel fixtures, Terrain-hole ownership, and an optional original masonry portal | `bwork_structures` |

Outputs use ordinary Unity Terrain, meshes, materials, prefabs, and LODGroups. The normal lifecycle is `prepare → apply → status → remove`: prepare computes and validates without publishing; apply owns a bounded replacement; remove restores only still-owned edits. Bridge surface profiles use `reset` instead of `remove`.

The package retains the original gray `bwork_structures` fixture for compatibility and lifecycle work. The 0.4.0 masonry portal is opt-in, preserves the existing tunnel dimensions and ownership seam, and passed the bounded fixture lifecycle and visual review. Its exposed rear hole seal still requires fitting during island integration.

## Quick start

The pinned development baseline is Unity **6000.6.0f1**, URP **17.6.0**, Unity Pipeline **0.7.0-exp.1**, and Python 3. The sample project declares its Unity dependencies. Blender **5.2.x** is needed only when regenerating offline source assets; prepared assets are included.

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

Generator source, recipes, and the [licensed asset snapshot](assets/CoastalBridgeTool) are included. Binary `.blend` files are intentionally omitted; the public Python generators and JSON recipes recreate the editable Blender sources, and the exported FBXs are provided.

## Original rocks, foliage and connected water

The original rock library retains its six established families and adds `coastal_outcrop`, `river_ledge`, and `fractured_boulder_cluster`. Each distributed model has three LODs and a separate simplified collider. The new forms use unique baked color/normal detail and serve the highland, riverbank, shallow-water, and waterfall compositions. The editable Blender files stay outside the public snapshot; the MIT generators, CC0 art, and exact public-output hashes are included. See the [rock fidelity note](docs/fidelity/ROCKS.md).

| Blender 5.2 clay LOD review | Blender 5.2 PBR LOD review |
|---|---|
| ![Blender review render of six original rock families at three LODs](docs/images/rock-lods-blender-clay.png) | ![Blender PBR review render of six original rock families at three LODs](docs/images/rock-lods-blender-pbr.png) |

![Forest and undergrowth authoring fixture](docs/images/foliage.png)

![Lake and planted riverbanks](docs/images/water.png)

[View the connected river, lake, delta, and finite ocean layout](docs/images/water-network.png).

Foliage and rock placement are deterministic for fixed recipes and source catalogs. `bwork_river_scene` is the composed water example: it prepares the rock library, applies connected water, then places independently owned bank and shallow-water rock batches. Each stage commits separately and retains its own recovery state. Connected water is authored geometry plus visual shader displacement. It does not simulate fluid volume, hydraulic erosion, buoyancy, or physical currents.

The 0.4.0 foliage catalog adds four original modeled plants: rose thicket, meadow daisy, wood sorrel, and coastal grass. Each has three authored LODs, shares an original leaf/petal atlas, and uses the existing bounded wind and exclusion system. The terrain pass adds four scan-derived mask maps for native Terrain Lit height blending, more varied bank classification, and optional `eroded-ridge` and `coastal-bluff` stamps. See the [foliage](docs/fidelity/FOLIAGE.md) and [terrain](docs/fidelity/TERRAIN.md) notes.

The 0.3.0 source also includes original periodic river-current, ocean-crest and waterfall maps, their standard-library generator, and a bounded coastal waterfall recipe. The waterfall creates an animated sheet and plunge foam and can place an original cliff-and-stone backdrop from the rock library. The assembled 0.3.0 run verified shader compilation, fixed-camera motion, replacement/removal, foreign-child protection and unchanged terrain. The art remains an experimental sample: the waterfall backdrop is a finite rock composition, without spray particles or fluid coupling. See the [motion-map and waterfall notes](assets/BFjordTools/Water/MOTION.md).

### Animated water and original river rocks

![Original river rocks and animated current in Unity](docs/images/river-rocks.png)

![Original rock-backed waterfall in Unity](docs/images/waterfall.png)

Short fixed-camera Unity captures: [river](docs/clips/river-motion.mp4), [ocean](docs/clips/ocean-motion.mp4), [waterfall](docs/clips/waterfall-motion.mp4). Each clip contains 24 rendered frames over two seconds. The 12 fps playback is an offline capture setting, not a device-performance result.

## 0.4.0 source fidelity preview

These four images review exported source assets in Blender. They are not Unity captures and do not establish assembled-scene contact, shader behavior, LOD transitions, lifecycle behavior, or performance.

| Nine rock families | Four original plants |
|---|---|
| ![Blender review of the nine original rock families](docs/images/rock-fidelity-blender.png) | ![Blender review of four original plant families and their LODs](docs/images/foliage-lods-blender.png) |
| Optional road dressing | Optional masonry tunnel portal |
| ![Blender review of the original verge wall and delineator](docs/images/road-detail-blender.png) | ![Blender review of the original masonry tunnel portal](docs/images/tunnel-portal-blender.png) |

Road dressing adds opt-in, terrain-supported wall fragments and delineator posts while preserving the accepted road ribbon and Terrain transaction. The tunnel pass adds an original ashlar portal, smoother metric-UV lining, and service curbs while retaining the existing Terrain-hole and recovery ownership. See the [roads](docs/fidelity/ROADS.md) and [tunnel](docs/fidelity/TUNNELS.md) notes.

All four bridge systems retain their dimensions and flat driving datum. The 0.4.0 source rebuild adds jointed coastal coping, corrected stone-block material sampling and quoins, member-aligned weathered timber, and independently replaceable coated-steel maps. Water gains narrower waterfall foam ropes, clearer flow lanes, toe-centred receiving foam, river contact detail, and more directional ocean foam/ripples without adding fluid simulation. See the [bridges](docs/fidelity/BRIDGES.md) and [water](docs/fidelity/WATER.md) notes.

## 0.4.0 Unity review

These captures come from the pinned Unity 6000.6.0f1 Editor fixtures after importing the 0.4.0 candidate. They demonstrate bounded authoring output, not runtime or device performance.

| Terrain masks and height blending | Original plant patch |
|---|---|
| ![0.4 Unity terrain fidelity capture](docs/images/fidelity-04-terrain.png) | ![0.4 Unity capture of rose, daisy, sorrel, and coastal grass planting](docs/images/fidelity-04-foliage-patch.png) |
| Original rock placement | Refined river surface and bank contact |
| ![0.4 Unity capture of original rock placement](docs/images/fidelity-04-rocks.png) | ![0.4 Unity river capture at animation time zero](docs/images/fidelity-04-river-t0.png) |
| Finite waterfall fixture | Coastal arch hero view |
| ![0.4 Unity waterfall capture at animation time zero](docs/images/fidelity-04-waterfall-t0.png) | ![0.4 Unity coastal arch fidelity capture](docs/images/fidelity-04-coastal-arch-180-hero.png) |
| Stone viaduct side construction view | Independent warm material profile |
| ![0.4 Unity side view of stone viaduct construction](docs/images/fidelity-04-stone-viaduct-110-construction.png) | ![0.4 Unity coastal arch with independently applied warm material profile](docs/images/fidelity-04-coastal-arch-180-warm-material.png) |

Fixed-camera 0.4.0 clips: [river](docs/clips/fidelity-04-river.mp4), [ocean](docs/clips/fidelity-04-ocean.mp4), and [waterfall](docs/clips/fidelity-04-waterfall.mp4). Each contains 24 captured frames over two seconds at 1440×960; 12 fps is the offline encoding rate, not a performance measurement. Four bridge designs also have hero and riding-view captures in the exported gallery. The stone construction image is the maintained side camera, not a close-up masonry study.

## Evidence and limits

The validation ledger currently records the 0.2.0 public baseline: **51 of 51 Unity Editor tests** with no skipped tests, a **55.73509-second** combined lifecycle validation, **8 Python CLI tests**, exact restoration/reuse of 162 catalog files, and seven representative request replays. For 0.3.0, **63/63 Editor tests passed**, followed by **5/5 affected waterfall tests** after its final visual changes. Rock lifecycle/dependency checks and a 32.651-second combined water lifecycle/capture run also passed. These timings describe authoring checks, not rendering frame rates.

For 0.4.0, **81/81 Editor tests passed** with no skips in 21.43 seconds. The 19.092-second assembly retained all 66,049 Terrain height samples while placing 130 highland, 150 riverbank and 87 shallow-water rocks, plus 177 meadow-detail, 54 rose and 247 sorrel plants. Plant repeat and sorrel remove/reapply checks passed. The 38.463-second water milestone covered replacement, foreign-child refusal, owned removal/reapply, unchanged Terrain heights, three motion sequences and zero `ShaderUtil` errors. The 144.859-second bridge run captured ten views across four designs and applied then reset the independent warm-concrete profile. These are Editor authoring timings, not rendering frame rates. See the [0.4.0 validation record](docs/fidelity/VALIDATION.md) for scope and filenames.

The road importer retained the FBX root-axis conversion fix and passed a separate **9/9 scoped road test run** in 0.73 seconds after the 81-test suite; these are separate runs, not an 83-test full-suite claim. In a fresh 512 m fixture, the 8.260-second road milestone placed 47 dressing instances across three roads and one junction, while the 10.048-second tunnel milestone produced two masonry LOD groups, 297 owned hole cells and a final 673 changed height cells. Both lifecycles verified exact deterministic replacement, old-asset cleanup, hierarchy guards, source-free removal, Terrain restoration and unrelated-subtree retention. Review [road dressing](docs/images/fidelity-04-road-dressing.png), [tunnel exterior](docs/images/fidelity-04-tunnel-exterior.png), and [tunnel interior](docs/images/fidelity-04-tunnel-interior.png) captures.

The fresh fixture was necessary because river cuts in the composed world correctly block replacement of the earlier road Terrain edit. The result therefore does not claim that dressed roads were integrated into the river world. The tunnel portal and lining read clearly in the review fixture, but an oversized rear Terrain-hole seal remains exposed outside the portal; final island placement must bury or fit that return before it can be presented as a finished mountain tunnel.

The current scope is one finite 512 m authoring sandbox and isolated bridge sites. Terrain stitching, whole-world streaming, arbitrary bridge curvature/grade, infinite oceans, fluid simulation, runtime island integration, and mobile performance acceptance are outside this release. The world composition remains sparse in places and uses simple review lighting. The waterfall is a freestanding finite fixture that still needs authored cliff integration. The bridges are visual game assets, not structural-engineering designs. Unity Pipeline 0.7.0-exp.1 is an experimental pinned command transport whose availability and API may change. The [art-direction record](docs/ART_DIRECTION.md) describes the current visual limitations; the gallery does not claim NatureManufacture visual parity.

## License split

| Files | License |
|---|---|
| Original Unity C# and shaders, public CLI, documentation, export tooling, and rock Blender generator | [MIT](LICENSE) |
| Blender bridge generator and its supporting Python modules | [GPL-3.0-or-later](LICENSES/GPL-3.0-or-later.txt) |
| Original bridge, rock, foliage, road-detail, and tunnel geometry/art; design recipes; generated map channels; and supplied review images | [CC0-1.0](LICENSES/CC0-1.0.txt) |
| Included ambientCG and Poly Haven art and documented prepared derivatives | CC0-1.0 with retained source credits; ambientCG Rock030 is procedural, not scanned |
| Unity Editor, URP, Newtonsoft JSON, and Unity Pipeline | Their own licenses; installed separately |

The bridge generator's GPL license and the rock generator's MIT license do not change the CC0 grant for their distributed artwork output. See [NOTICE.md](NOTICE.md) for exact scopes, [asset credits](docs/ASSET_LICENSE.md), [source provenance](docs/SOURCE_PROVENANCE.md), and [export provenance](docs/EXPORT_PROVENANCE.json). No NatureManufacture source, model, shader, or screenshot is included.
