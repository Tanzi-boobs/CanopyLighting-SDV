# CanopyLighting-SDV

Standalone Stardew Valley 1.6 SMAPI mod project for a future canopy-lighting effect.

## Build on GitHub
Every push to `main` runs the **Build Canopy Lighting** workflow.
Open **Actions → Build Canopy Lighting**, select a successful run, and download
the `CanopyLighting` artifact. Extract it and place the `CanopyLighting`
folder in the game's `Mods` directory.

## Current status
The current build is a **loader/configuration scaffold only**. It checks the
SMAPI entry point and config plumbing; it does not yet render canopy lighting.
The CI build uses a compile-only SMAPI API shim, so a successful build is not
proof of runtime compatibility. Test with Stardew Valley 1.6 and Cinderbox
before treating this as a working mod.

## Planned effect
Add a canopy-specific mask and lighting pass for mature tree crowns. Avoid
classifying all green pixels, which could unintentionally affect grass, NPC
clothing, crops, and buildings.