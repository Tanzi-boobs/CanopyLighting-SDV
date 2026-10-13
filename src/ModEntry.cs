using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Canopy Lighting prototype.
/// The current version validates the SMAPI loading path and configuration;
/// canopy rendering is not implemented yet.
/// </summary>
public sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(Config);

        Monitor.Log(
            "Canopy Lighting scaffold loaded. Canopy lighting effects are not implemented in this build.",
            LogLevel.Warn);
    }
}