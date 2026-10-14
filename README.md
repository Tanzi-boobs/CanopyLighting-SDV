# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied by Dynamic Shader. Dynamic Shader is not required at runtime.

## Candidate 0.4.18

This candidate keeps the canopy overlay inline with `Tree.draw`, so the lighting pass stays at the same world-layer position as each tree rather than being drawn over the whole world after the fact.

Changes since 0.4.17:
- Cache shader-baked canopy textures for a whole in-game hour instead of clearing and rebaking every 10 in-game minutes.
- Tint the overlay with the tree's current alpha and stop skipping all overlay pixels when the tree begins to fade, so the lighting should no longer visibly snap off near the player.
- Cache texture dimensions and per-tree-type alternate-sprite flags to reduce repeated reflection during the per-tree draw hook.
- Keep the lightweight pine profile enabled by default; the more expensive texture-guided pine rim is an optional GMCM setting.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine technique on mature pine trees.
- Canopy pixels only; no additional ground-shadow sprite. Vanilla ground shadows are unchanged.
- GMCM exposes overall strength, broadleaf settings, pine settings, and the optional detailed pine profile.
- Loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Verification status

GitHub Actions verifies compilation only. The cache bucket, fade tint, layering, and FPS changes remain a Cinderbox/Android test candidate and are not claimed as device-verified.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep `assets/shaders/ShadowEffect.mgfxo` in place.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.
