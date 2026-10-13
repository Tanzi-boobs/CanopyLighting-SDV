# Canopy Lighting

A Stardew Valley 1.6 SMAPI mod that applies directional lighting to mature wild-tree canopies. Dynamic Shader is **not required** at runtime.

## Behavior

- Loads `assets/shaders/ShadowEffect.mgfxo` from this mod's own folder.
- Uses the texture-guided pine technique for mature pine trees and the broadleaf canopy technique for other mature `Tree` types.
- Mirrors the Stardew tree canopy atlas cell for seed-ready, untouched, and mossy variants.
- Applies a SpriteBatch transform and viewport projection to the custom effect.
- Renders only a canopy-lighting pass; it does not draw an additional ground shadow.
- Registers GMCM options through reflection to avoid an Android SMAPI `IManifest` interface-loading error. GMCM remains optional.

## Install

Extract the complete `CanopyLighting` folder to `Mods`. It must contain:

```text
CanopyLighting/
├── manifest.json
├── config.json
├── CanopyLighting.dll
└── assets/
    └── shaders/
        └── ShadowEffect.mgfxo
```

The personal-use ZIP includes the compiled shader supplied by the user. The binary is intentionally not committed to this public repository, so GitHub Actions uploads a DLL-only validation artifact rather than a complete installable ZIP.

## GMCM options

- Enable/disable canopy lighting and set overall strength.
- Toggle broadleaf/non-pine trees and pine trees separately.
- Adjust broadleaf light and canopy shading.
- Adjust pine light, canopy shading, and rim lighting.

Canopy shading changes canopy pixels only; it does not add ground shadows.

## Diagnostics

The log reports when `Tree.draw` executes, up to 12 samples of outdoor state, tree type, growth stage, and stump state, when a mature tree reaches the shader pass, and when the first overlay succeeds. These messages distinguish filters, API setup, and rendering.

CI validates C# compilation using a compile-only SMAPI API shim. It does not prove that the shader renders correctly at runtime.
