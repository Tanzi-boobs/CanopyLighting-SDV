# Canopy Lighting

A Stardew Valley 1.6 SMAPI mod that invokes the oak and pine canopy techniques from the locally installed Dynamic Shader mod.

## How it works

- Loads \`ShadowEffect.mgfxo\` from the user's installed \`Nook.DynamicShader\` folder at runtime.
- Uses the \`TreeCanopyLight\` technique for mature oak trees and \`TextureGuidedPineCanopyLight\` for mature pine trees.
- Sets the sun direction from the in-game time and fades the effect around sunrise and sunset.
- Does **not** include or redistribute Dynamic Shader's compiled shader asset in this repository or in the Canopy Lighting ZIP. Dynamic Shader must remain installed.

## Important setup

Because Canopy Lighting calls the same two shader techniques, avoid drawing the built-in canopy effect twice. In Dynamic Shader's own \`config.json\`, set these two values to \`false\`:

\`\`\`json
"EnableTreeCanopyLight": false,
"EnablePineCanopyLight": false
\`\`\`

Other Dynamic Shader settings can stay enabled. Canopy Lighting checks those values and logs a warning if either built-in canopy option is still on.

## Configuration

- \`Enabled\`: turns this mod's canopy pass on or off.
- \`OverallStrength\`: master multiplier from 0 to 2.
- \`EnableOakCanopy\` and \`EnablePineCanopy\`: independently toggle oak and pine.
- \`OakLightStrength\`, \`OakShadowStrength\`, \`PineLightStrength\`, \`PineShadowStrength\`, \`PineRimStrength\`: effect strengths, each clamped to 0–5.

## Testing status

The CI pipeline builds against a compile-only SMAPI API shim because the GitHub runner does not include the game installation. A successful build confirms compilation and packaging; it does **not** prove the effect works in-game. Test first on a mature oak and pine outdoors, then check camera movement, the player walking behind a canopy, and whether other sprites retain the correct draw order. Send the latest SMAPI log and screenshots if a runtime error occurs.
