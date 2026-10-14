# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied with Dynamic Shader. Dynamic Shader is not required at runtime.

## Current release

Version 0.4.12 uses the same compiled `ShadowEffect.mgfxo` supplied by the user, selecting only `TreeCanopyLight` and `TextureGuidedPineCanopyLight`. The shader asset in the personal-use ZIP has been verified byte-for-byte against the supplied Dynamic Shader archive. This release changes the SpriteBatch invocation path to use reflected overloads instead of dynamic binding, addressing the `Matrix?` overload error recorded in the user's SMAPI log.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular shadows are unchanged.
- GMCM options include effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep the shader file in `assets/shaders/ShadowEffect.mgfxo`.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.

## Verification status

GitHub Actions run #142 compiled the current C# source successfully. This verifies compilation only, not live rendering. In the next in-game test, check whether `Canopy shader drew its first overlay` is logged and whether any new SpriteBatch exception occurs. Visual appearance must be confirmed in Cinderbox before the shader parameters are tuned further.

[Build run #142](https://github.com/Tanzi-boobs/CanopyLighting-SDV/actions/runs/38532636599)
