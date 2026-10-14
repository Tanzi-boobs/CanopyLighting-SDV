# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied by Dynamic Shader. Dynamic Shader is not required at runtime.

## Current candidate: 0.4.16

This candidate retains the bundled Dynamic Shader `ShadowEffect.mgfxo` canopy techniques and groups canopy overlays to avoid restarting SpriteBatch for every tree. It captures the world transform during `Tree.draw` and uses that transform for overlay coordinates while restoring the event-time batch state afterward. This addresses a likely cause of invisible or misplaced overlays in the Cinderbox test.

The dawn intensity curve has also been adjusted: full daylight strength is reached shortly after 6:00 instead of 7:00. This should make the canopy lighting easier to observe during the user's frozen-6:00 test. It remains a hypothesis until visually verified in Cinderbox.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular shadows are unchanged.
- GMCM options include effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Rendering note

The grouped pass draws canopy overlays after the world draw and before the HUD. This reduces per-tree batch switches, but may change overlap order where a canopy crosses characters, buildings, or other tree canopies. Check visual layering and FPS in-game. CI verifies compilation only.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep the shader file in `assets/shaders/ShadowEffect.mgfxo`.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.
