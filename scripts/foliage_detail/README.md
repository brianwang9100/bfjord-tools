# Original botanical foliage

`generate.py` creates four original plants, three modeled LODs each, 512px base/normal atlases, FBX reimport evidence and a CPU Cycles contact sheet. Models/textures/review renders are CC0-1.0; these scripts are MIT. No reference image is opened by the generator.

Use Blender 5.2.1 or compatible Python API. Supply `--output` for an `OriginalFoliage` asset folder and `--review` for a render folder. `prepare_catalog.py --catalog PATH` writes stable Unity metadata using the existing catalog's reviewed importer templates and updates only `Assets/BFjord/OriginalFoliage/` entries. `review_closeups.py` runs against the saved review blend and emits one close view for each actual reimported LOD0.

Geometry is in metres with Blender Z-up; FBX export targets Unity Y-up. One UV atlas and one material slot per model. Live Unity is owned by the coordinating integration task.
