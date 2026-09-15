# Structures: original masonry tunnel

The September 15 pass keeps the existing tunnel/approach/terrain ownership and replaces the grey utility entrance presentation with an optional original asset-backed masonry facade and metric concrete lining. It does not change native routes or workout ownership.

## Reference comparison and reuse

Privately inspected `naturemanufacture/images/structures/meadow-HD_08.jpg` shows a forest log assembly: strong ground contact, visible material grain, irregular joints and layered vegetation. It is not a tunnel reference. Its transferable lessons are scale, contact and restrained irregularity, not an implied commercial tunnel comparison. The baseline `design/native/world-authoring-tools/images/tunnel-detail.png` instead shows an exposed uniformly grey, faceted return with no construction joints, coping or readable material scale. No private image or proprietary model is a generator input or public asset.

The new original portal has individually bevelled ashlar wings/jambs, 25 arch voussoirs, a raised stone hood, coping and buried footings. Blender studio review shows readable joints and restrained mineral variation. The procedural inner shell now uses 32 arch segments and station/cross-section metric UVs, with planar local UVs on caps; rotating or curving a tunnel no longer changes its texture scale. Low service curbs break up the floor/lining seam.

A proportional free-tool check considered [Poly Haven's CC0 assets](https://polyhaven.com/license). A generic rock/structure scan would still need topology adapted to the exact bore and portal seal, so this one bounded original facade was quicker and preserves dimensions. Existing Unity mesh/terrain code is reused. [Unity TerrainData.SetHoles](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TerrainData.SetHoles.html) remains the hole API; [URP Terrain Lit](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shader-terrain-lit.html) requires Terrain Holes support. No package dependency, paid asset, or custom runtime shader was added. Ordinary URP Lit/LODGroup ownership is compatible with the existing Unity 6000.6 editor pipeline. This is an Editor-only tool; no iPad performance claim is made.

## Implementation and ownership

`portalStyle` defaults to `none` for existing recipes. `masonry` requires a tunnel with exactly 8m width and 5.5m crown clearance; the importer rejects other dimensions rather than scaling stones/textures. Both portals are placed at the existing computed front-rim position and rotated into the local endpoint tangent, at scale one. The shell and taper remain responsible for enclosing the Terrain hole raster envelope. The facade reaches 0.35m below the road plane.

The source catalog supplies `Assets/BFjord/TunnelDetail/MasonryPortal.json` and four 512px maps. Apply validates bounds, arrays, normals, positive triangles and reducing LODs. It makes generation-owned Unity meshes/materials/texture copies. The receipt includes those files; source disappearance therefore does not prevent removal. Both ends share their three portal mesh assets. Decoration adds no colliders: the existing closed shell and road deck remain the bounded collision representation. The three facade LODs have 11,556 / 4,708 / 1,284 triangles and 34,668 / 14,124 / 3,852 vertices. Continuous lining remains one authored mesh; this is not a runtime tunnel streaming system.

New receipts fingerprint transforms, names, component types, asset references, LOD membership and child membership through the existing finite package hierarchy validator. Foreign content or edited references reject replace/remove before terrain changes. Legacy receipts accept only their original direct mesh/renderer/collider children and owned references. Existing terrain identity, changed-cell, pre-existing-hole, approach and restored-baseline checks remain active. The saved Unity checkpoint remains the crash recovery boundary.

## Reproduction and actual checks

```sh
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python scripts/tunnel_detail/build_tunnel.py
python3 scripts/tunnel_detail/verify_assets.py assets/BFjordTools/asset-catalog assets/BFjordTools/TunnelDetail/catalog-additions.json
```

Actual Blender 5.2.1 run completed at four CPU threads. Checks passed for finite positions/normals, positive triangle area, face/winding agreement, clear bore, metric UVs and planted contact. An independent stdlib payload check passed catalog bytes/SHA256, metric bounds, positive triangle and UV areas, normal/winding agreement, and decreasing LOD counts. The native `.blend`, studio render, manifest and independent payload evidence are retained. JSON is a direct metric mesh payload; there is no unverified FBX roundtrip claim.

Current-source pure structure smoke passed: bridge 6,252 vertices; tunnel 36,624 vertices; closed outward shells, upward approaches and exact endpoint seams. Analytic terrain approach clearance improved from -0.585932m to +0.035543m with a maximum 0.748033m cut. Existing sparse hole restoration checks passed unrelated-hole, changed-state, duplicate and bounds cases. Additional pure checks passed 90-degree UV invariance, dimension rejection and a finite curved masonry bore. Current full Editor source compiled against the installed Unity 6000.6 SDK; the standalone compiler suppressed its known netstandard2.0/2.1 reference-binding warning. These are software checks, not art or device acceptance.

Four Editor tests in `TunnelDetailTests` cover dimensions, rotated metric UVs, entrance/exit frames and foreign-child fingerprints. They passed as part of the 81-test full Editor suite recorded in the [0.4.0 validation record](VALIDATION.md).

## Integration and camera

1. Merge the separate `assets/BFjordTools/TunnelDetail/catalog-additions.json` entries into the shared catalog. Include the `.meta` files; the additions file intentionally leaves the shared catalog manifest untouched.
2. Install the catalog payload and this package into the isolated review project using the normal exporter/admission workflow.
3. With the saved sandbox and road-established full-detail Terrain, run `bwork_structures action=prepare recipePath=masonry-example`, then `bwork_structures action=apply recipePath=masonry-example`. This built-in recipe samples the restored current Terrain for both endpoint heights and uses only the original hill tunnel. It remains the ordinary Structures command with the same receipt and terrain lifecycle. The saved example JSON under `scripts/tunnel_detail/masonry-hill-tunnel.json` reproduces the earlier 257-grid baseline; use the built-in for the updated Terrain.
4. `bwork_view view=tunnel capture=tunnel-masonry-fidelity` uses camera `(267,43,171)` looking at `(310,38,167)`. Check portal ground contact, the widening return behind the masonry and lining continuity in the assembled scene. The studio render cannot verify Terrain contact. If relocated, retain the fixed bore and use the local endpoint tangent for the camera.
5. For lifecycle review, run the four tests, read-only prepare, deterministic reapply, a foreign-child rejection, source-unavailable remove, exact owned height/hole restoration and final apply; take one exterior and one entrance-to-bore capture.

The accepted fresh-fixture lifecycle completed in 10.048 seconds with exact repeat geometry, poses, heights and holes; cleanup of prior generated assets; foreign-child and edited-transform refusals; source-free removal; exact height/hole restoration; and retention of unrelated subtrees. The final apply produced two masonry LOD groups, 297 changed hole cells and 673 changed height cells. The [exterior capture](../images/fidelity-04-tunnel-exterior.png) shows the masonry arch and coping grounded at the entrance; the [interior capture](../images/fidelity-04-tunnel-interior.png) shows continuous curved lining and curbs.

The fixture exposes an oversized rear Terrain-hole seal beyond the portal. Island integration must bury or fit that return; this is a reusable masonry module and lifecycle demonstration, not a finished mountain tunnel or commercial-pack parity claim.
