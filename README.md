# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies. Dynamic Shader is not required at runtime.

## Visual behavior

- The broadleaf technique is applied to all mature non-pine wild trees; the pine-specific technique is used for mature pine trees.
- The morning sun direction favors the upper-left canopy edge, then shifts across the screen through the day, matching the supplied Dynamic Shader reference.
- Only canopy pixels are processed. No additional ground-shadow sprite is drawn; vanilla ground shadows are unchanged.
- The mod bundles and loads `assets/shaders/ShadowEffect.mgfxo` from its own folder.

## Setup

Extract the complete `CanopyLighting` folder into `Mods`. Generic Mod Config Menu is optional, but when installed it exposes effect enable, overall strength, broadleaf strengths, and pine strengths.

The personal-use ZIP includes the compiled shader supplied by the user. The binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.

## Diagnostics

The hook binds the first Tree.draw argument directly as `__0` rather than relying on Harmony's `__args` array. This is important because the last device log showed `Tree.draw hook is executing` but no following tree-sample messages, which indicates the old hook exited before evaluating tree eligibility. The new version logs missing runtime bindings and tree samples before filtering, then reports the first successful canopy overlay.

GitHub Actions verifies C# compilation only. The shader result still needs to be checked in the game. After testing, inspect SMAPI-latest.txt for `Tree draw sample`, `Mature tree reached canopy pass`, and `Canopy shader drew its first overlay`.
