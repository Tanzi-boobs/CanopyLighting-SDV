# Double Tap HUD 0.2.1

Standalone SMAPI mod for Stardew Valley 1.6, targeting the desktop-style HUD on Cinderbox/Android.

## Installable build
This project must be compiled; a source ZIP alone is not installable. The CI workflow packages DoubleTapHUD.dll, manifest.json, and config.json into an installable Mods/DoubleTapHUD folder.

## Gestures
- Double-tap the upper-right date/time panel to open the main game menu (Inventory).
- Double-tap the HUD quest icon to open Quest Log.
- Double-tap the Map or Crafting tab to switch to that page.
- Double-tap an active crafting recipe to craft it. The first tap is suppressed; the second is handed to the normal game or Better Crafting click handler.
- Double-tap empty map space to return to the previously open game-menu tab. Map location markers, the tabs, and the close button keep their normal behavior.

## Crafting compatibility
The mod does not create items or consume ingredients itself. It identifies vanilla CraftingPage recipe buttons and Better Crafting live ComponentRecipes / RecipeComponents button mappings through reflection, without a hard dependency on Better Crafting. Other mods that replace the entire crafting screen with an unrelated custom UI are not guaranteed to expose a compatible recipe-button map.

## Configuration
config.json options: DoubleTapWindowMilliseconds (180-900; default 420), MaxTapDistance (16-160; default 72), and booleans for the clock, quest icon, Map tab, Crafting tab, crafting recipes, and blank-map close.

## Testing status
Compile validation is run in GitHub Actions. Runtime behavior still needs Cinderbox testing because input event routing can differ between Android/desktop mode and installed UI mods.