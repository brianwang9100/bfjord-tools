# Original masonry tunnel detail

CC0-1.0 artwork, authored from scratch in Blender; MIT generator and verification scripts. No downloaded models, maps or reference pixels are inputs. The private reference library is observation-only and excluded from these assets.

`MasonryPortal.blend` retains editable Blender mesh geometry. The catalog contains a metric, three-LOD `MasonryPortal.json` consumed by the Structures tool and four original 512px PBR maps. Renderer geometry uses +X right, +Y up and +Z into the tunnel, with no object scaling. The opening is 8m wide and 5.5m high. Footings reach 0.35m below the road plane. The bevelled facade is visual detail; the closed tool-owned shell provides collision.

`portal-review.png` is a Blender studio render, not a Unity integration screenshot. `manifest.json` records measured export counts/checks; `catalog-additions.json` records the exact files to merge into the common asset catalog at integration. See `docs/fidelity/TUNNELS.md` in the source repository for tool behavior and the assembled Unity review procedure.
