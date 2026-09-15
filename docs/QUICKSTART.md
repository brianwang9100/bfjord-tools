# Reproducible quick start

This walkthrough exercises the public wrapper and the bounded sample without regenerating bridge art. Run every command from the repository root.

## 1. Check the checkout

Requirements:

- Python 3 with no third-party Python packages for the wrapper.
- Unity 6000.6.0f1. The sample manifest pins URP 17.6.0 and Unity Pipeline 0.7.0-exp.1.
- Blender 5.2.x only if you intend to regenerate the bridge FBXs.

Inspect the CLI and public inventory:

```sh
python3 scripts/bfjord.py --help
python3 scripts/bfjord.py doctor
```

`doctor` checks that the sample manifest and the two exported asset catalogs are present. It does not launch Unity, resolve packages, compile Editor code, or inspect a Blender installation.

## 2. Restore the sample assets

Preview the catalog operation, then restore exact files:

```sh
python3 scripts/bfjord.py prepare-project --dry-run
python3 scripts/bfjord.py prepare-project
```

The destination defaults to `Examples~/SandboxProject`. Every catalog entry has a byte count and SHA-256. Setup validates all sources and destinations before copying, reuses exact existing files, and refuses a conflicting file instead of overwriting it. Unrelated project files are left alone.

The sample's `Packages/manifest.json` points to the checkout's local `Packages/com.bfjord.tools` directory. Its `ProjectSettings/BfjordTools.json` is the explicit opt-in and defines the owned scene, generated-output, bridge-source, and capture roots. A custom project must install the package, restore or supply its own compatible assets, and create an equivalent explicit configuration.

## 3. Import the sample

Open `Examples~/SandboxProject` with Unity 6000.6.0f1 and wait for package resolution and script compilation. Keep the project in Edit Mode. Save unrelated scene changes before a command that opens another configured scene.

The first command creates or opens `Assets/Scenes/WorldAuthoringTools.unity`:

```sh
python3 scripts/bfjord.py run \
  --project Examples~/SandboxProject \
  --request recipes/create-sandbox.json
```

When the project has `Temp/UnityLockfile`, the wrapper's default `--mode auto` selects resident execution. It dispatches the original JSON file through `bwork_request --requestPath`, preserving top-level fields such as `targetScene`. If the Hub CLI is elsewhere, add:

```sh
--unity-cli /path/to/unity
```

Resident commands default to a 120-second Pipeline timeout. Pass `--timeout 300` to `bfjord.py run` for a slower request; accepted values are 1–3600 seconds. The wrapper forwards this value to the resident `unity command --timeout` option. This syntax was checked against the locally installed Unity Pipeline 0.7.0-exp.1 help. Batch mode does not use the Pipeline command transport, so `--timeout` does not terminate a batch Editor process.

If Unity is closed but its lock remains after a crash, resolve the stale Unity process/project lock before retrying. Do not force batch mode while an Editor still owns the project.

## 4. Run sample requests

The included requests are intentionally small and reviewable:

```sh
# Validation only: computes the terrain change without publishing it.
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/terrain.json

# Mutating examples: shape the ridge, then fit roads and water to it.
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/terrain-apply.json
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/roads.json
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/water.json

# Opens the separately configured four-bridge collection scene.
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/bridge-collection.json
```

The bridge collection is a separate scene from the 512 m terrain/road/water sandbox. Do not interpret those commands as one composed-scene sequence.

Commands accept a JSON object with a fixed schema:

```json
{
  "schemaVersion": 1,
  "command": "bwork_roads",
  "arguments": {
    "action": "status"
  },
  "targetScene": "Assets/Scenes/WorldAuthoringTools.unity"
}
```

`targetScene` is optional. When supplied, it must name an allowed scene in `ProjectSettings/BfjordTools.json`. The dispatcher opens it only after confirming that current scene edits are saved. Command names and arguments are allowlisted and type checked.

Foliage recipes are installed with the package under `Packages/com.bfjord.tools/Samples/`. Read the package's `Documentation~/Foliage.md` before applying them: the asset catalog must be built first, and named batches have independent ownership.

With the sample Editor open, build the foliage assets and apply all six presentation recipes through their actual Pipeline command:

```sh
UNITY_CLI='/Applications/Unity Hub.app/Contents/Resources/cli/unity'
BFJORD_PROJECT="$PWD/Examples~/SandboxProject"

# Return from the separate bridge scene to the saved terrain sandbox.
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/create-sandbox.json

"$UNITY_CLI" command bwork_foliage --action build-assets --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId canopy --recipePath Packages/com.bfjord.tools/Samples/forest-canopy.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId young --recipePath Packages/com.bfjord.tools/Samples/forest-young.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId floor --recipePath Packages/com.bfjord.tools/Samples/forest-floor.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId herbs --recipePath Packages/com.bfjord.tools/Samples/forest-herbs.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId rocks --recipePath Packages/com.bfjord.tools/Samples/forest-rocks.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId bank --recipePath Packages/com.bfjord.tools/Samples/riverbank-ferns.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
```

The `bank` batch requires the connected-water request above. The other batches retain their own receipts, so applying one does not replace another.

The fuller presentation adds four more independently owned batches with the same command:

```sh
"$UNITY_CLI" command bwork_foliage --action apply --batchId hills --recipePath Packages/com.bfjord.tools/Samples/forest-hills.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId bank-canopy --recipePath Packages/com.bfjord.tools/Samples/riverbank-canopy.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId bank-herbs --recipePath Packages/com.bfjord.tools/Samples/riverbank-herbs.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
"$UNITY_CLI" command bwork_foliage --action apply --batchId bank-rocks --recipePath Packages/com.bfjord.tools/Samples/riverbank-rocks.json --project-path "$BFJORD_PROJECT" --timeout 120 --format json
```

All three `bank-*` batches also require connected water.

Reopen the saved bridge collection, then apply an independent surface profile to the coastal arch without rebuilding its geometry:

```sh
python3 scripts/bfjord.py run --project Examples~/SandboxProject --request recipes/bridge-collection.json

"$UNITY_CLI" command bwork_bridge_surface --action apply \
  --batchId collection-coastal-arch-180 \
  --profilePath "$PWD/assets/CoastalBridgeTool/material-profiles/warm-concrete/profile.json" \
  --project-path "$BFJORD_PROJECT" --timeout 120 --format json

"$UNITY_CLI" command bwork_bridge_surface --action reset \
  --batchId collection-coastal-arch-180 \
  --project-path "$BFJORD_PROJECT" --timeout 120 --format json
```

`bwork_bridge_surface` owns material bindings separately from `bwork_bridge_asset`; `reset` restores that bridge generation's declared default materials.

## 5. Use batch mode

Close the Unity Editor before batch execution. Pass the Editor executable and a result path that does not already exist:

```sh
python3 scripts/bfjord.py run \
  --mode batch \
  --editor /path/to/Unity \
  --project Examples~/SandboxProject \
  --request recipes/create-sandbox.json \
  --result /tmp/bfjord-create-sandbox.json
```

Batch execution writes a sibling `.log` file and exits nonzero when Unity reports failure or does not write the requested result. Each invocation requires a fresh result path so an older success cannot be mistaken for the current run. Captures require a graphics-capable Editor; do not add `-nographics`.

## 6. Run the Python checks

The public wrapper and bridge design math have separate dependency-free test entry points:

```sh
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 scripts/coastal_bridge/test_design.py -v
```

Run the bridge checks through their script path as shown so the adjacent `design.py` module is the one imported.

## 7. Inspect or remove owned output

Change only the request's `arguments.action` to `status` or `remove` for a family that uses the normal lifecycle. `prepare` and `status` are read-only. `remove` restores only cells/assets still matching the command's receipt and refuses conflicting external edits. Bridge material profiles use `reset` to return to the generation's declared defaults.

See [VALIDATION.md](VALIDATION.md) for the completed import, command, contract and screenshot checks. Editor fixture evidence does not establish device performance.
