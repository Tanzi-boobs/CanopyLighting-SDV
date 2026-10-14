# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied with Dynamic Shader. Dynamic Shader is not required at runtime.

## Current release

Version 0.4.13 keeps the supplied compiled `ShadowEffect.mgfxo` and uses only `TreeCanopyLight` and `TextureGuidedPineCanopyLight`. Version 0.4.12 was confirmed in-game to draw the canopy overlay. Version 0.4.13 narrows the effective brightness slider ranges to reduce white, overexposed broadleaf canopies and caches reflection metadata used by the per-tree render pass to reduce repeated work on mobile.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular shadows are unchanged.
- GMCM options include effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep the shader file in `assets/shaders/ShadowEffect.mgfxo`.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.

## Verification status

GitHub Actions run #149 successfully compiled version 0.4.13. The prior version's first canopy overlay was confirmed in Cinderbox/Android by the user's SMAPI log. Version 0.4.13's revised brightness limits and cached render metadata still require on-device visual and FPS verification.

[Build run #149](https://github.com/Tanzi-boobs/CanopyLighting-SDV/actions/runs/38535837408)
