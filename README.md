# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied with Dynamic Shader. Dynamic Shader is not required at runtime.

## Current release

Version 0.4.15 retains the Dynamic Shader compiled `ShadowEffect.mgfxo` canopy techniques and batches overlays to avoid SpriteBatch restarts for every mature tree. It fixes an Android compatibility issue in 0.4.14: it no longer references `RenderedWorldEventArgs.SpriteBatch`, which is missing in the user's SMAPI Android build. Instead, the SpriteBatch instance observed by the existing `Tree.draw` hook is retained and used at `RenderedWorld`.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular shadows are unchanged.
- GMCM options include effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Rendering note

The grouped pass draws canopy overlays after the world draw and before the HUD. This reduces per-tree batch switches, but it may change the overlap order where a canopy crosses characters, buildings, or other tree canopies. Verify visual layering and FPS in-game. CI verifies compilation only.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep the shader file in `assets/shaders/ShadowEffect.mgfxo`.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.
