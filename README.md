# Canopy Lighting

A Stardew Valley 1.6 SMAPI mod that applies directional lighting directly to mature wild-tree canopies. Dynamic Shader is **not required**.

## Behavior

- Loads \`assets/shaders/ShadowEffect.mgfxo\` from this mod's own folder.
- Uses the texture-guided pine technique for mature pine trees and the broadleaf canopy technique for every other mature \`Tree\` type, including oak, maple, mahogany, palm, mushroom and mystic trees.
- Mirrors Stardew 1.6.15's selected canopy atlas cell for seed-ready, untouched and mossy variants.
- Applies the full SpriteBatch transform and viewport projection matrix to the custom shader, which is required for custom effects to render in screen space.
- Updates the sun direction over the in-game day and fades the lighting around dawn and dusk.
- Renders only a canopy-lighting pass. It does not draw a ground shadow; the vanilla circular shadow under each tree is left unchanged.
- Registers settings with Generic Mod Config Menu (GMCM) when GMCM is installed. GMCM is optional; settings can also be edited in \`config.json\`.

## Install

Extract the complete \`CanopyLighting\` folder to \`Mods\`. It must contain:

\`\`\`text
CanopyLighting/
├── manifest.json
├── config.json
├── CanopyLighting.dll
└── assets/
    └── shaders/
        └── ShadowEffect.mgfxo
\`\`\`

The personal-use ZIP includes the compiled shader supplied with the user's Dynamic Shader archive. That binary is intentionally not committed to this public repository. GitHub Actions therefore uploads a DLL-only validation artifact, not a complete installable ZIP.

## GMCM options

- Enable canopy lighting and overall strength.
- Enable/disable the broadleaf profile (all non-pine mature wild-tree types) and pine profile separately.
- Set broadleaf light/shading, pine light/shading, and pine rim-lighting strengths.
- Canopy shading strength affects only tree-canopy pixels; it does not add ground shadows.

## Diagnostics and testing

The log reports whether the Tree.draw hook executes, when it reaches a mature tree, and when a shader overlay is successfully drawn. These distinguish shader initialization from actual rendering. GitHub Actions checks C# compilation using a compile-only SMAPI API shim; live MonoGame rendering still needs to be verified in-game.
