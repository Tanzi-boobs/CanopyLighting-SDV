using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Canopy Lighting prototype.
/// 
/// This first safe scaffold exposes the config and lifecycle. The actual
/// canopy-only render hook still needs implementation and in-game verification;
/// vanilla Stardew does not expose a canopy mask as a simple public API.
/// </summary>
internal sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(Config);

        Monitor.Log(
            "Canopy Lighting loaded. This build is a scaffold; canopy rendering is not implemented yet.",
            LogLevel.Warn);
    }
}