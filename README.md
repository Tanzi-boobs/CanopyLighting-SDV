# CanopyLighting-SDV

Standalone Stardew Valley 1.6 SMAPI mod project targeting canopy lighting.

## Build
Push to `main` or run **Actions → Build Canopy Lighting → Run workflow**.
Download the `CanopyLighting` artifact, extract it, and place the folder in
`Stardew Valley/Mods`.

## Current status
This repository currently contains a buildable SMAPI scaffold, not a finished
lighting effect. The entry point loads configuration and logs that canopy
rendering is not implemented yet.

## Planned implementation
Implement canopy-only lighting using a render-time mask for mature tree canopy
pixels. Avoid broad green-pixel classification, which would incorrectly affect
grass, NPC clothing, crops, and buildings. Test against Stardew Valley 1.6 and
the user's Cinderbox Android environment before considering it usable.