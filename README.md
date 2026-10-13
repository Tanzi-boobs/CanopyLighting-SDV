# CanopyLighting-SDV

A standalone Stardew Valley 1.6 SMAPI mod that adds time-dependent light and shade to mature tree crowns.

## What it does
- Applies a warm, directional tint to the tree canopy itself, with the light direction and intensity changing through the day.
- Fades the effect in after sunrise and out around sunset.
- Uses the tree's own canopy texture as the draw mask, so the overlay should stay within the leaf sprite instead of painting rectangles onto grass, buildings, or nearby characters.
- Exposes `Enabled`, `Strength`, and `Warmth` in `config.json`.

## Install
Download the `CanopyLighting` artifact from the latest successful **Build Canopy Lighting** run under [GitHub Actions](https://github.com/Tanzi-boobs/CanopyLighting-SDV/actions). Extract the `CanopyLighting` folder into Stardew Valley's `Mods` folder.

## Testing status
The cloud workflow compiles against a compile-only SMAPI API shim because it runs without a Stardew Valley installation. A successful CI build verifies C# compilation and packaging, but **does not replace in-game testing**. The first test should check that the overlay aligns with the canopy, stays behind the trunk, fades correctly while the player walks behind a tree, and behaves on Cinderbox Android.

If you test it, please send the SMAPI log and a screenshot of a mature tree in daylight and near sunset. That will help tune layer depth, tint strength, and mobile rendering compatibility.
