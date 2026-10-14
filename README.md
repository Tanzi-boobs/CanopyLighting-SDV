# Canopy Lighting

A standalone Stardew Valley 1.6 SMAPI mod for directional lighting on mature wild-tree canopies, based on the canopy techniques supplied by Dynamic Shader. Dynamic Shader is not required at runtime.

## Candidate 0.4.17

- Flushes the queued shader overlays at the end of `GameLocation.drawAboveFrontLayer`, immediately after the tree layer, instead of waiting until `Display.RenderedWorld`. This is intended to preserve world draw order and reduce lighting overlays appearing over unrelated foreground sprites.
- Skips the extra canopy overlay when the player is within the tree's canopy fade zone, so the vanilla transparency used to reveal the player is not covered by a fully opaque shader overlay.
- Retains the bundled `ShadowEffect.mgfxo` and grouped drawing to avoid restarting SpriteBatch once per tree.

This is a candidate fix and needs visual/FPS verification in Cinderbox. The shader still processes mature visible canopies, so performance may remain limited on dense, heavily modded maps.

## Visual behavior

- Broadleaf technique on mature non-pine trees; pine-specific technique on mature pine trees.
- Canopy pixels only; no added ground-shadow sprite. Vanilla circular ground shadows are unchanged.
- GMCM settings control effect enable, overall strength, broadleaf strengths and pine strengths.
- The mod loads `assets/shaders/ShadowEffect.mgfxo` from its own folder; Dynamic Shader is not a runtime dependency.

## Install

Extract the complete `CanopyLighting` folder into `Mods`. Keep `assets/shaders/ShadowEffect.mgfxo` in place.

The personal-use ZIP includes the compiled shader supplied by the user. That binary is intentionally not committed to this public repository; GitHub Actions produces a DLL-only validation artifact.
