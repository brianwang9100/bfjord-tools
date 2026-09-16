# Shoreline terrain palette

The opt-in `shoreline` paint palette uses exactly four Terrain layers, in order: **ForestLitter, DrySand, WetSand, RockFace**. It retains the existing URP Terrain Lit height blending and stochastic anti-tiling adapter. The `temperate`, `woodland`, `coast` and `cliff` palettes keep their previous classifications and surfaces. Repainting changes surface layers and alphamaps; it does not modify terrain height samples, holes or water geometry.

Install the ShoreDetail catalog before selecting this palette. DrySand and WetSand share the matched `Assets/BFjord/ShoreDetail/Textures/BeachSand_Color.png`, `BeachSand_NormalGL.png` and `BeachSand_Mask.png` scans at their **2.1 m** physical footprint. The mask stores R metallic (zero), G AO, B actual scan displacement, A smoothness. WetSand uses a `(0.62,0.64,0.65)` diffuse multiplier, 0.42–0.72 mask smoothness remapping and 0.5 normal strength. Its darkness and sheen are material properties, not duplicate dark textures. ForestLitter and RockFace reuse the existing admitted terrain bank.

Run the existing terrain paint command with `Samples/shoreline-terrain.json`. The saved paint profile adds four bounded options:

| Field | Default | Meaning |
| --- | --- | --- |
| shorelineWidthMeters | 26 | Outer distance limit from the canonical ocean rectangle edge |
| shorelineHeightMeters | 6 | Maximum absolute height difference from ocean level |
| shorelineWetWidthMeters | 6 | Wet-sand distance limit, within the broader beach |
| shorelineWetHeightMeters | 1 | Wet-sand height limit, within the broader beach |

The mask uses the canonical connected-water recipe's sole `ocean` node: signed distance to its rectangular footprint, including Euclidean corner distances, and world elevation relative to the ocean surface. The absolute edge distance bounds the beach on both land and underwater sides. Sand remains full strength until 58% of both distance and elevation limits, then fades smoothly toward the outer margin. Wet sand has a smaller 30% interior plateau. This avoids multiplying two fades from zero across the entire beach, which let leaf litter dominate before native height blending. Deterministic noise varies the beach width and the existing slope thresholds return cliffs to rock. Rivers and lakes cannot create beach sand far from that ocean edge. Without a connected ocean, low slopes remain ForestLitter. Inland forest cannot become sand merely because it is low or a nearby river is wet.

The toolkit recognizes both its old four-layer names and the shoreline layer names for subsequent palette changes. Externally authored layer sets retain the existing ownership guard. Existing layer/material snapshot rollback remains in place. No native-world route, workout or terrain-carving contract changes.

Unity's [TerrainLayer API](https://docs.unity3d.com/ScriptReference/TerrainLayer.html) defines the diffuse and mask remapping used here. The existing adapter changes sampling only, leaving native Terrain Lit remaps, lighting, holes and geometry passes intact. New Editor tests cover signed ocean rectangle distance, absence/far/high/deep exclusions, wet versus dry classification, rock slopes, normalized weights and invalid profile limits. Root integration runs those tests and the assembled shoreline view; this source change does not claim an Editor, device or performance result on its own.
