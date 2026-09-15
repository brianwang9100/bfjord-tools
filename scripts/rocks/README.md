# Original BFjord rocks

The Python generator is original MIT-licensed code. Geometry uses deterministic
irregular fracture wedges, rounded exposed seams and restrained surface weathering;
outcrop layers, slab buttresses, crag spurs and scree pieces retain separate masses.
The river stone uses a low-frequency warped ellipsoid.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/rocks/build_rocks.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/rocks/verify_rocks.py
```

Optional generator arguments follow `--`: `--only granite_boulder --seed 123
--dimensions 3 2 2 --no-render`. Dimensions are width/depth/height in meters;
the exported manifest lists Unity width/height/depth. A partial build writes a
partial manifest, so rerun without arguments before distributing the whole library.

The full build writes three FBX LODs and a bounded convex collider per family,
`assets/BFjordTools/Rocks/manifest.json`, shared maps, an editable private
`original.blend`, and a contact-sheet render. FBX is exported with baked Unity
axes, normals, UVs and tangents. UV scale is 2 meters per repeat. Dominant-axis
UV seams can be visible with the standard texture material; triplanar Unity
materials are preferable for freely rotated cliff pieces. LOD reduction preserves
the original UVs but can soften small seams. The screen-height transitions are
starting values, not a claim of sustained iPad performance.

Keep `.blend` private: it contains absolute source texture paths. Audit FBX metadata
before public distribution. Export art is CC0-1.0; do not change the independent
MIT generator to GPL merely because it runs inside Blender. No third-party Blender
generator source is incorporated.

## Fidelity additions

`build_fidelity.py` appends/replaces three original families while preserving the six legacy FBXs. Run it after `build_rocks.py` when rebuilding the entire nine-family library. CPU Cycles uses four threads and 1024px bakes by default:

```sh
blender --background --threads 4 --python scripts/rocks/build_fidelity.py
blender --background --threads 4 --python scripts/rocks/verify_rocks.py
blender --background --threads 4 --python scripts/rocks/verify_ledge_contact.py
blender --background --threads 4 --python scripts/rocks/render_fidelity.py
```

Use `-- --only river_ledge` to regenerate one addition or `-- --resolution 512` for a draft. The default 1024 bake is the checked release art. `Sources/*.blend` retain editable high and low meshes with relative texture paths. `render_fidelity.py` reviews actual exported LODs, not the high meshes. Models/textures are CC0; generator and verification scripts are MIT.
