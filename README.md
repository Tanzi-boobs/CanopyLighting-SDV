# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied by Dynamic Shader. Dynamic Shader is not required at runtime.

## Candidate 0.4.17

- Queues tree canopy overlays and flushes them after `GameLocation.drawAboveFrontLayer`, rather than waiting until the later world-render event. This is intended to keep overlays closer to the tree layer and reduce them drawing over unrelated foreground objects.
- Avoids adding the shader overlay when Stardew's own tree alpha is below 0.98, so the mod doesn't paint a full-strength canopy over a tree being faded to reveal the player.
- Uses the lightweight `TreeCanopyLight` technique for pine trees by default. The detailed texture-guided pine rim profile performs additional neighbour-texture samples, so it is now an optional GMCM setting.
- Caches reflected property accessors and exits early for immature saplings to reduce repeated work in the `Tree.draw` hook.
- Retains the original bundled `ShadowEffect.mgfxo` from the provided Dynamic Shader assets.

This candidate needs visual and FPS verification in Cinderbox. The tree's translucency near the player is normally controlled by Stardew's `alpha` value; the mod intentionally skips the extra overlay when that value indicates a fade. If the geometry truly disappears rather than becoming translucent, send a screenshot from another angle.

## Visual behavior

- Directional canopy light/shading on mature broadleaf and pine trees.
- No added ground-shadow sprite; vanilla circular ground shadows are unchanged.
- GMCM has an optional detailed pine rim profile, disabled by default for mobile performance.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep `assets/shaders/ShadowEffect.mgfxo` in place.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.
