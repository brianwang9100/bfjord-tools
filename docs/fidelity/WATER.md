# Water fidelity pass

## Observed reference gaps

Inspected the actual private NatureManufacture reference images `river-Screen_11`, `river-Screen_24` and `river-Screen_3`, then the 0.3.0 river, ocean and waterfall stills at animation time zero. The references show irregular rock-supported flow, clear lanes beside whitewater, and changing foam density as water crosses ledges and reaches the receiving surface. The baseline had a fairly rectangular white sheet over a rounded cap, broad uniform plunge foam, and soft river/ocean detail. These observations guide original work; no reference pixels, meshes, material settings or code are included.

## Reuse decision

[Unity's maintained Shader Graph 17.6 production-ready samples](https://docs.unity.cn/Packages/com.unity.shadergraph@17.6/manual/Shader-Graph-Sample-Production-Ready-Detail.html) describe separate water/stream/waterfall techniques and a forest-stream workflow. The existing original URP HLSL surfaces already provide the required bounded mesh and ownership seams. Reusing them avoids importing an additional sample dependency tree. No Unity sample code or assets were copied. Unity's sample license is not CC0; current package and iPad behavior must still be validated independently.

The rock pass's original Blender `coastal_outcrop`, `river_ledge` and `fractured_boulder_cluster` directly supply the required physical shapes, so a separate water-specific Blender module would duplicate that library. The waterfall uses those prefabs with their variant-aware baked material set; river ledge contact is projected against actual LOD0 triangles at authoring time.

## Implemented

- The falling sheet retains exact centre lip/toe positions, with sub-metre cross-channel relief, bounded lateral variation and irregular width. The feeder is still projected onto its rock cap before publication. No per-frame mesh rebuilding is introduced.
- Two overlapping texture scales create moving clear/whitewater lanes. The original waterfall map now separates coarse coverage from narrow foam ropes instead of filling the entire sheet.
- Receiving foam is centred at the actual toe, with a compact impact core, asymmetric downstream breakup, expanding fronts and small normal-only radial ripples. It continues to use exactly the connected ocean's receiving wave phase and displacement envelope.
- Optional depth contact fades the water against opaque ledges and wet stones. It is enabled only when the configured URP pipeline and sandbox cameras promise current depth. Transparent receiving water is not sampled as opaque depth.
- River current strands and shallow-water turbulence have separate masks. A subdued submerged edge tint and broken contact foam soften water-bank transitions without altering terrain ownership.
- Ocean foam follows high, wind-facing wave portions and crossing texture scales. A fine directional ripple sits beneath the larger swell; no extra vertex displacement is added.
- Existing two-mesh/two-material waterfall output, eight-prefab backdrop, ownership seal, foreign-object protection, source-independent removal and staged replacement remain intact. Both previous backdrop ID sequences are accepted for recovery/removal. The source hash includes a new geometry revision marker.

The three motion maps remain original 512² linear RGB PNGs with existing mip/repeat/iOS importer settings and CC0-1.0 attribution. Total encoded payload is 1,934,214 bytes; imported GPU residency is a separate measurement. The MIT generator takes no image inputs.

## Verification and reproduction

The generator passed its periodicity assertions; exact SHA-256 hashes, PNG dimensions and byte counts match `motion-manifest.json`; the generated waterfall map was visually inspected. The 0.4.0 Unity milestone then completed waterfall replacement, foreign-child refusal, removal/reapply and unchanged-height checks in 38.463 seconds with no `ShaderUtil` errors. River, ocean and waterfall each produced a 24-frame, two-second capture at 12 fps. See the [validation record](VALIDATION.md) for the exact scope and links; these results are separate from the 0.3.0 history.

Regeneration:

```sh
python3 assets/BFjordTools/Water/generate_motion_maps.py --output Unity/WorldAuthoringTools/Textures/Water > assets/BFjordTools/Water/motion-manifest.json
```

After importing the new immutable rock library, run through the existing JSON request runner:

```json
{"schemaVersion":1,"command":"bwork_rocks","arguments":{"action":"build-assets"}}
{"schemaVersion":1,"command":"bwork_water_connected","arguments":{"action":"refresh-appearance"}}
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"prepare"}}
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"apply"}}
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"view","capture":"waterfall-fidelity-t0","timeSeconds":"0"}}
{"schemaVersion":1,"command":"bwork_waterfall","arguments":{"action":"capture","capture":"waterfall-fidelity-t075","timeSeconds":"0.75"}}
```

Inspect the actual feeder contact after the new ledge import, plus clear lanes, foot-rock intersection, receiving-wave alignment and time-separated river/ocean details. Preserve foreign-child and edited-transform rejection, removal without source files, and unchanged terrain heights. Refresh connected-water appearance before captures so its receipt matches the new shader/map bytes.

## Limits

This is finite, authored visual water. It does not bounce off arbitrary colliders, carve channels, simulate fluids/spray, provide screen refraction, implement underwater rendering or establish commercial-pack parity. Above-water rock wetness comes from authored wet material profiles, not dynamic moisture transfer. The receiving elevation/wave parameters remain an explicit authored seam. The fixed review cameras exercised world-space foam and fine normals at grazing angles. Moving-camera behavior and physical-device performance remain unverified.
