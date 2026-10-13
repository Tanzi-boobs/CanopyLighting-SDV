using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>Loads the standalone canopy-lighting effect.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        Config.Strength = Math.Clamp(Config.Strength, 0f, 1f);
        Config.Warmth = Math.Clamp(Config.Warmth, 0f, 1f);
        helper.WriteConfig(Config);

        try
        {
            TreeCanopyLighting.Install(Config, Monitor);
            Monitor.Log(
                $"Canopy Lighting enabled={Config.Enabled}, strength={Config.Strength:0.00}, warmth={Config.Warmth:0.00}.",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log("Couldn't install the canopy lighting hook: " + ex.Message, LogLevel.Error);
        }
    }
}