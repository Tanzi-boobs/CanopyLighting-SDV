# Canopy Lighting

A Stardew Valley 1.6 SMAPI mod that applies directional lighting to mature oak and pine canopies using a bundled MonoGame effect. Dynamic Shader is **not required**.

## Standalone behavior

- Loads \`assets/shaders/ShadowEffect.mgfxo\` from this mod's own folder.
- Uses the \`TreeCanopyLight\` technique for oak and \`TextureGuidedPineCanopyLight\` for pine.
- Changes the sun direction and fades effect strength according to in-game time.
- Includes independent oak/pine toggles and strength controls in \`config.json\`.

## Install

Extract the complete \`CanopyLighting\` folder to Stardew Valley's \`Mods\` folder. The folder must contain:

\`\`\`text
CanopyLighting/
├── manifest.json
├── config.json
├── CanopyLighting.dll
└── assets/
    └── shaders/
        └── ShadowEffect.mgfxo
\`\`\`

The full private/personal-use package includes the compiled shader provided by the user. That binary is intentionally not committed to this public repository. Consequently, the public GitHub Actions workflow uploads a DLL-only validation artifact, not an installable ZIP.

## Configuration

- \`Enabled\`: enables/disables Canopy Lighting.
- \`OverallStrength\`: master multiplier from 0 to 2.
- \`EnableOakCanopy\` and \`EnablePineCanopy\`: toggle each tree type.
- \`OakLightStrength\`, \`OakShadowStrength\`, \`PineLightStrength\`, \`PineShadowStrength\`, \`PineRimStrength\`: individual effect strengths, clamped to 0–5.

## Testing status

CI verifies C# compilation using a compile-only SMAPI API shim; it does not validate live MonoGame rendering. After installing, test a mature oak and pine outdoors, then check camera movement, sprites walking behind the canopy, and the SMAPI log. The mod has not yet been verified in-game on Cinderbox Android.
