# Art direction and 0.6.0 review status

BFjord Tools targets a restrained temperate coast built from bounded, editable Unity assets. The 0.4.0 source fidelity pass is complete across terrain, roads, bridges, foliage, rocks, water, and the optional tunnel portal. Unity 6000.6.0f1 imported and compiled the candidate without errors; the Editor suite, world assembly, water lifecycle/motion review, four-design bridge gallery, and isolated road/tunnel lifecycles completed. See the [0.4.0 validation record](fidelity/VALIDATION.md) for measured scope.

## Current 0.6.0 integration review

The current pass reduces obvious Terrain texture grids, seats upright tree collars on slopes, adds oak B and birch B, and refines river strands and ocean foam. The [0.6 detail record](fidelity/NATURE_06.md) provides same-camera terrain comparisons, tree/contact views and water clips. Source is frozen; assembled Unity checks and visual review remain in progress, with no v0.6 test-pass claim yet.

The costs stay explicit: adapted four-layer Terrain can use up to 36 material samples versus 12, with `antiTiling=false` retaining native sampling. Native XZ projection remains; steep rock may still stretch. B tree identities are additive, preserving A assets and existing prefabs. Water adds no samples or geometry and retains its displacement bounds. Neither the source renders nor the Editor captures establish reference parity or iPad performance.

## Previous 0.5.0 addition

The 0.5.0 assembly adds broad oak and slender birch silhouettes alongside mature fir, rigid hollow deadwood and mixed tall-grass patches. These assets are original Blender work with their own leaf/bark PBR atlas. The recorded scene has 19 mixed-canopy placements (6 oak, 3 birch, 10 fir), 17 logs and 174 mixed-grass placements while retaining all 66,049 Terrain height samples. The actual woodland/deadwood/grass and three Terrain-palette captures have been reviewed; road markings, the river-flow still comparison and larger foamy beach waves are also reviewed. Actual [river motion](clips/fidelity-05-river.mp4) and [beach motion](clips/fidelity-05-beach.mp4) are complete. The recorded integration snapshot passed 133/133 in 103.54 seconds with no failures, skips or inconclusive results; the separate CLI suite passed 8/8 in 0.537 seconds. The [labeled nine-rock comparison](images/fidelity-05-boulders.png) passed exact repeat-placement and unchanged-Terrain checks in 8.511 seconds. The final assembled-road and water-refresh lifecycle checks passed; the source is published in bfjord-tools.

After that snapshot, source compilation remained clean and scoped Editor regressions passed separately: **RoadMarkingTests 9/9 in 11.36 seconds** and **WaterSurfaceOwnershipTests 5/5 in 5.41 seconds**. Seven test cases were added after the snapshot. The road checks cover deferred URP normalization for yellow/white paint and exact legacy-receipt proof; water checks prevent retirement of shared surface assets. These scoped runs are not added to the 133-test total.

Seven physical Terrain surfaces form four palettes: temperate, woodland, coast and cliff. Forest litter, coastal shingle and rock face join the four retained scans; each palette keeps four simultaneous layers. Built-in terrain stamps now expose a saved deterministic seed, while zero preserves the old forms. Asphalt center/edge markings follow the retained ribbon. Low/medium/high river appearances share a pattern seed and camera setup; stronger shoreline crests and foam are intended to make the sample beach read clearly in motion.

The [0.5.0 detail record](fidelity/NATURE_05.md) lists exact assets, sources, recipes, observed counts and validation results. Reviewed actual Unity output includes [mixed woodland](images/fidelity-05-woodland.png), [fallen wood](images/fidelity-05-deadwood.png), and [grass wind](clips/fidelity-05-grass.mp4), plus the [woodland](images/fidelity-05-terrain-woodland.png), [coast](images/fidelity-05-terrain-coast.png) and [cliff](images/fidelity-05-terrain-cliff.png) palettes at the same camera. The [asphalt markings](images/fidelity-05-road.png), same-camera [low](images/fidelity-05-river-low.png)/[medium](images/fidelity-05-river-medium.png)/[high](images/fidelity-05-river-high.png) river appearances and [larger beach waves](images/fidelity-05-beach.png) are reviewed as bounded scene output. The water remains a surface without overturning lips or volumetric spray. Procedural timber is simpler than scanned wood, and the palette comparison is still a barren steep-hill fixture rather than finished landscape context. The 0.4.0 source sheets and Unity captures below remain historical evidence, rather than being relabeled as the new scene.

## Historical 0.4.0 visual character

The intended setting is a restrained temperate coast: readable landforms, cool connected water, dark road ribbons, grounded bridge materials and clustered vegetation that follows slopes, clearances and bank fields. Materials should remain legible under ordinary daylight without hiding geometry behind dramatic exposure or post-processing.

The bridge collection supplies the largest authored silhouettes. The 0.4.0 source adds construction-scale details to all four bridge systems without changing their dimensions or driving datum. Nine original rock families now cover boulders, river stones, outcrops, ledges, slabs, scree, crags, and fractured clusters. Four new modeled plants add rose, daisy, sorrel, and coastal-grass forms to the retained scanned canopy and understory.

Terrain keeps native Terrain Lit and four material layers, with real packed masks, height blending, larger-scale bank breakup, and optional eroded ridge/coastal bluff stamps. Roads keep their accepted ribbons and add optional supported wall fragments and delineator posts. Water retains finite authored geometry while refining waterfall lanes, impact foam, river contact, and ocean surface detail. The legacy gray tunnel remains available; an optional original ashlar portal and metric lining now provide the 0.4.0 art candidate.

The source review sheets below are Blender evidence, not Unity captures:

- [Nine rock families](images/rock-fidelity-blender.png)
- [Four original plants and LODs](images/foliage-lods-blender.png)
- [Road wall and delineator](images/road-detail-blender.png)
- [Masonry tunnel portal](images/tunnel-portal-blender.png)

Detailed decisions, provenance, reproduction, and remaining checks are recorded for [foliage](fidelity/FOLIAGE.md), [rocks](fidelity/ROCKS.md), [terrain](fidelity/TERRAIN.md), [water](fidelity/WATER.md), [roads](fidelity/ROADS.md), [bridges](fidelity/BRIDGES.md), and [tunnels](fidelity/TUNNELS.md).

The Unity review includes [terrain](images/fidelity-04-terrain.png), [the new plant patch](images/fidelity-04-foliage-patch.png), [rock placement](images/fidelity-04-rocks.png), fixed-time [river](images/fidelity-04-river-t0.png), [ocean](images/fidelity-04-ocean-t0.png), and [waterfall](images/fidelity-04-waterfall-t0.png) views, plus hero/riding cameras for all four bridge designs. The bridge set also includes the [stone viaduct side construction view](images/fidelity-04-stone-viaduct-110-construction.png) and [independent warm material variant](images/fidelity-04-coastal-arch-180-warm-material.png).

The isolated fixture adds [road dressing](images/fidelity-04-road-dressing.png), [tunnel exterior](images/fidelity-04-tunnel-exterior.png), and [tunnel interior](images/fidelity-04-tunnel-interior.png) views. The road capture shows readable segmented stone with grounded wall contact. The tunnel exterior shows the masonry arch and coping grounded at the entrance; the interior shows continuous curved lining and service curbs.

## Candid limitations

- The bounded Unity review establishes import, composition and fixed-camera evidence. It does not establish moving-camera LOD quality, sustained rendering cost, or iPad suitability.
- The new terrain masks and stamps improve the available vocabulary, but native heightfields cannot create undercut cliffs. Rock ledges and outcrops must supply those silhouettes.
- Road dressing is deliberately intermittent and can reject unsupported placements, so some steep verges will remain open. That 0.4.0 pass did not add lane markings; 0.5.0 adds them on the retained asphalt surface. Drainage, patches and new road surfacing remain outside this addition.
- The new plants use modeled detail and a shared atlas rather than scan-level per-species variation. Meadow density, repetition, and canopy rhythm still require assembled scene review.
- Water remains finite visual geometry. It does not simulate fluids, spray, collider response, refraction, or dynamic wetness.
- The waterfall capture shows a freestanding authoring fixture. It still needs integration into an authored cliff and surrounding landscape before it reads as finished environment art.
- The masonry portal is fixed to the supported bore dimensions. Local terrain and camera angle determine whether the rear seal reads cleanly.
- The current world staging is sparse in places and uses simple neutral review lighting. It remains visibly below the density, lighting and finish of the private commercial references.
- The stone viaduct construction capture uses the maintained side camera; it is useful for the whole form but is not a close masonry-detail view.
- Road and tunnel lifecycle acceptance used a separate fresh 512 m fixture. River cuts in the composed world correctly prevent replacing the earlier road Terrain ownership, so the evidence does not show dressed roads integrated with that river scene.
- The tunnel fixture exposes an oversized rear Terrain-hole seal beyond the masonry portal. Final island integration must bury or fit that return; this is not yet a finished mountain tunnel.

## Historical 0.4.0 assembled Unity acceptance

Completed evidence includes the 81-test Editor suite, deterministic world assembly with unchanged Terrain heights, water ownership/lifecycle checks, three fixed-camera motion clips, four bridge hero/riding pairs, and independent bridge-surface apply/reset. Source sheets remain labeled as Blender reviews.

Remaining art work is intentionally narrow:

1. Fit or bury the tunnel's rear hole seal during island placement and recheck both portal contacts.
2. Integrate dressed roads with a correctly ordered island build rather than replacing Terrain beneath later river edits.
3. Review representative foliage wind and LOD transitions with a moving camera.
4. Integrate the waterfall into authored cliff scenery and refine the wider world's density and lighting before making a finished-environment claim.

Repairs should be judged by contact, scale, silhouette, and material response rather than triangle count or screenshot saturation. Authoring receipts remain authoritative for geometry, grade, ownership, and restoration; screenshots remain art evidence only. No current 0.4.0 claim extends to physical device performance, iPad acceptance, or NatureManufacture parity.
