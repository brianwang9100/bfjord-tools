# Bounded demonstration dependencies

`assets/CoastalBridgeTool/demo-dependencies` contains only the material, texture, rock and shrub assets used by the isolated bridge fixture, with their exact Unity GUID metadata. It does not contain an entire Unity project, application settings, an island scene or personal Editor state.

The dependency manifest records each relative destination, byte length and SHA-256. The setup path validates the full bundle and destinations before restoration, reuses exact matches, and refuses conflicting files or GUID metadata. The two external GUIDs are supplied by the installed URP package: its Lit shader and Editor asset-version metadata component. The additional tileable bedrock maps are in `assets/CoastalBridgeTool/DemoMaterials`.

The public package resolves asset roots through the project's explicit `ProjectSettings/BfjordTools.json` configuration. A complete public-checkout replay of that layout is still in progress. The bridge replay sequence is demo setup, prepare/apply both bridge manifests and placement recipes, then `bwork_bridge_demo action=refresh-presentation`. That last operation applies camera isolation and bridge-matched approach materials. Re-run it after replacing bridge generations and before final capture.

The prepared bridge materials used by approaches are copied into scene-owned assets, so deleting a bridge generation cannot invalidate the approach textures. Shadows remain enabled. The sample is an Editor art/authoring fixture, not a runtime route catalog or performance acceptance result.
