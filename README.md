# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied with Dynamic Shader. Dynamic Shader is not required at runtime.

## Current release

Version 0.4.14 keeps the supplied compiled `ShadowEffect.mgfxo` and uses only `TreeCanopyLight` and `TextureGuidedPineCanopyLight`. In version 0.4.12 the canopy shader was confirmed to render in Cinderbox/Android; version 0.4.13 narrowed brightness limits and cached reflection metadata. Version 0.4.14 changes the rendering path to queue tree canopy overlays and render them in grouped shader passes, avoiding SpriteBatch restarts for every mature tree.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular shadows are unchanged.
- GMCM options include effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Important rendering note

The grouped pass draws canopy overlays after the world draw and before the HUD. This reduces per-tree batch switches, but it may change the overlap order where a canopy crosses characters, buildings, or other tree canopies. Please verify visual layering and FPS in-game. CI only verifies compilation.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep the shader file in `assets/shaders/ShadowEffect.mgfxo`.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.

[Build run #153](https://github.com/Tanzi-boobs/CanopyLighting-SDV/actions/runs/38540450603)
