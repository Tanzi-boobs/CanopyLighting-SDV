using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>Loads the Dynamic Shader-backed canopy-lighting effect.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        Config.OverallStrength = Math.Clamp(Config.OverallStrength, 0f, 2f);
        Config.OakLightStrength = Math.Clamp(Config.OakLightStrength, 0f, 5f);
        Config.OakShadowStrength = Math.Clamp(Config.OakShadowStrength, 0f, 5f);
        Config.PineLightStrength = Math.Clamp(Config.PineLightStrength, 0f, 5f);
        Config.PineShadowStrength = Math.Clamp(Config.PineShadowStrength, 0f, 5f);
        Config.PineRimStrength = Math.Clamp(Config.PineRimStrength, 0f, 5f);
        helper.WriteConfig(Config);

        try
        {
            TreeCanopyLighting.Install(Config, Monitor, helper.DirectoryPath);
            Monitor.Log(
                $"Canopy Lighting enabled={Config.Enabled}, strength={Config.OverallStrength:0.00}, oak={Config.EnableOakCanopy}, pine={Config.EnablePineCanopy}.",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log("Couldn't install the standalone canopy hook: " + ex.Message, LogLevel.Error);
        }
    }
}
