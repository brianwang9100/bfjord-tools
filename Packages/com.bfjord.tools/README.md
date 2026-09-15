# BFjord Tools package

Asset-backed Unity Editor tools for terrain, roads, four bridge designs, foliage and connected water. Outputs are ordinary Unity Terrain, meshes, materials, prefabs and LODGroups. The initial supported development configuration is Unity 6000.6.0f1, URP 17.6.0 and Unity Pipeline 0.7.0-exp.1.

## Start a separate project

The repository includes `Examples~/SandboxProject` and licensed sample assets. From the repository root:

```sh
python3 scripts/bfjord.py prepare-project
```

Open that sample in the pinned Unity Editor. Install this package with Unity Package Manager if using a different project. The sample already references `Packages/com.bfjord.tools` through a relative local dependency.

Every project explicitly opts in through `ProjectSettings/BfjordTools.json`. Its source, generated asset and capture paths resolve against the project root. The sample includes this configuration; custom projects can run `bwork_project action=configure configPath=<JSON>` with their chosen paths. Configuration refuses overlapping ownership roots and protects unrelated scene edits.

## Run commands

Use the resident Unity CLI, the **Tools → BFjord Tools → Run JSON Request** menu, or an ordinary Unity batch invocation. A request is a typed JSON object:

```json
{
  "schemaVersion": 1,
  "command": "bwork_sandbox",
  "arguments": { "action": "create" }
}
```

With the sample open in Unity:

```sh
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/create-sandbox.json
```

The wrapper chooses the resident Editor when the project is locked. For batch execution, close that Editor and provide `--editor` if it is not installed in the default macOS location. Each batch needs a fresh result filename. Graphical captures require a graphics-capable Editor; do not use `-nographics`.

| Tool | Command | Output |
|---|---|---|
| Terrain | `bwork_terrain` | Regional sculpting, protected edits and material painting |
| Roads | `bwork_roads` | Asphalt, gravel and dirt surfaces, junctions and fitted roadbeds |
| Bridges | `bwork_bridge_asset` | Imported arch, masonry viaduct, steel truss and timber trestle assets |
| Bridge surfaces | `bwork_bridge_surface` | Independent material profiles; no geometry rebuild |
| Bridge collection | `bwork_bridge_collection` | Four landscaped sample sites and named capture cameras |
| Foliage | `bwork_foliage` | Deterministic species batches with spacing and exclusion rules |
| Water | `bwork_water_connected` | Connected river/lake/delta/ocean surfaces and animated shading |
| Tunnels / simple structures | `bwork_structures` | Lined tunnel, portal, bridge and approach examples |

The usual lifecycle is `prepare → apply → status → remove`. Preparation does not publish outputs. Applying replaces only the command’s owned content, and removal restores still-owned Terrain edits. Commands reject conflicting external edits rather than overwriting them.

Bridge surfaces use `prepare → apply → status → reset`. Profiles reference semantic material keys and their own textures; the bridge meshes, dimensions, placement and driving collider stay unchanged. Structural replacement imports a new generation and starts with its declared default materials; apply the chosen surface profile to that generation afterward.

## Examples and boundaries

Bundled foliage recipes live at `Packages/com.bfjord.tools/Samples/`. The four bridge generators and licensed prepared art live outside this package in the repository’s `scripts/coastal_bridge` and `assets/CoastalBridgeTool` directories. Blender is required only to regenerate meshes.

`bwork_verify` is a deliberately mutating lifecycle demonstration for the dedicated sandbox. Run it after assembling a milestone, not after every appearance edit. It rebuilds the bundled tool outputs and checks restoration and sibling preservation. See the repository’s evidence and screenshots for the exact tested revision; older timings and counts are not guarantees.

The first release targets bounded authoring sites. Roads and approaches are checked against the full-detail stored Terrain grid. The ocean is finite; water motion is shading, not a fluid simulation. Straight level asset-backed bridges are visual game assets, not engineering designs. Runtime island integration and iPad performance are separate from Editor validation.

Original package code is MIT. Bundled art and generator licenses are listed in the repository notices. Unity and Unity Pipeline retain their own licenses; NatureManufacture products are inspiration and are not included.
