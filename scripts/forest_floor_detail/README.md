# Forest floor detail authoring

`build_forest_floor.py` generates the original seven-specimen family, 2048 PBR atlas, three LODs, FBX reimport checks, packed editable Blender source and review renders. It uses Blender's bundled numpy and the verified CC0 bark files copied into `art/BFjordTools/ForestFloorDetail/Sources`.

Run Blender with `--python` from the repository root; no add-ons are required. `prepare_catalog.py` writes deterministic Unity import metadata and a family additions receipt without changing the global catalogue. See the art folder README for source credit, bounds, placement and reproduction details.
