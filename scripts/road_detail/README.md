# Original road dressing

`generate.py` makes two original CC0 assets with Blender: `VergeWall_A` (staggered stone courses, battered sides and irregular coping) and `Delineator_A` (moulded ivory post, black insets and amber reflector faces). Each has three exported/reimported FBX LODs. The 512px stone base/normal pair is an original procedural mineral field. No external images or models are read. The generator is MIT; art is CC0-1.0.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python Scripts/world_assets/road_detail/generate.py -- --output art/BFjordTools/RoadDetail
python3 Scripts/world_assets/road_detail/prepare_catalog.py --source art/BFjordTools/RoadDetail --templates art/BFjordTools/asset-catalog
```

The second command reads reviewed importer templates, writes metadata beside the new assets, and creates `catalog-additions.json`. It does not change the shared catalog. A coordinator copies its `files` entries into `Assets/BFjord/RoadDetail` and refreshes outer catalog hashes. FBX metadata sanitization changes exported bytes: preserve `assets[].sha256` as `sourceSha256`, then recompute `sha256` for shipped bytes. Source and review folders are not Unity catalog inputs.

Blender 5.2.1 is the checked version. Blender Z-up becomes Unity Y-up; walls extend along Unity local Z, and model bases use a centre pivot in metres. Models have no collision. `bwork_roads action=apply dressing=true` uses ordinary LODGroup/renderers and owns copied meshes/materials with the road receipt. Leave `dressing` omitted to preserve the undressed behavior of existing commands.
