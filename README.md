# Canopy Lighting

A Stardew Valley 1.6 SMAPI mod that applies directional lighting directly to mature wild-tree canopies. Dynamic Shader is **not required**.

## Behavior

- Loads \`assets/shaders/ShadowEffect.mgfxo\` from this mod's own folder.
- Uses the texture-guided pine technique for mature pine trees and the broadleaf canopy technique for every other mature \`Tree\` type, including oak, maple, mahogany, palm, mushroom and mystic trees.
- Mirrors Stardew 1.6.15's selected canopy atlas cell for seed-ready, untouched and mossy variants.
- Updates the sun direction over the in-game day and fades the lighting around dawn and dusk.
- Renders only a canopy-lighting pass. It does not draw a ground shadow; the vanilla circular shadow under each tree is left unchanged.

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

## Configuration

- \`Enabled\`: enables/disables Canopy Lighting.
- \`OverallStrength\`: master multiplier from 0 to 2.
- \`EnableOakCanopy\`: toggles the broadleaf profile on all non-pine wild trees.
- \`EnablePineCanopy\`: toggles the pine profile.
- \`OakLightStrength\` / \`OakShadowStrength\`: broadleaf lighting/shading strengths.
- \`PineLightStrength\` / \`PineShadowStrength\` / \`PineRimStrength\`: pine lighting/shading strengths.

Changing canopy shadow strength only affects pixels within the canopy shader. It does not add a ground shadow.

## Testing

CI validates compilation using a compile-only SMAPI API shim; it cannot confirm live rendering. The user's latest SMAPI log showed the hook installing without a runtime shader error, but visual output still needs verification in Cinderbox. Test more than one mature tree type and confirm the vanilla ground shadow remains unchanged.
