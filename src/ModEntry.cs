using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>Loads standalone canopy lighting and registers its GMCM options when available.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig Config = new();

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        ClampConfig();
        helper.WriteConfig(Config);

        try
        {
            TreeCanopyLighting.Install(Config, Monitor, helper.DirectoryPath);
            RegisterConfigMenu(helper);
            Monitor.Log(
                $"Canopy Lighting enabled={Config.Enabled}, strength={Config.OverallStrength:0.00}, broadleaf/other={Config.EnableOakCanopy}, pine={Config.EnablePineCanopy}.",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log("Couldn't install the standalone canopy hook: " + ex.Message, LogLevel.Error);
        }
    }

    private void RegisterConfigMenu(IModHelper helper)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
        {
            Monitor.Log("Generic Mod Config Menu wasn't found; settings remain editable in config.json.", LogLevel.Debug);
            return;
        }

        api.Register(
            mod: ModManifest,
            reset: () =>
            {
                Config = new ModConfig();
                ClampConfig();
                TreeCanopyLighting.UpdateConfig(Config);
                helper.WriteConfig(Config);
            },
            save: () =>
            {
                ClampConfig();
                TreeCanopyLighting.UpdateConfig(Config);
                helper.WriteConfig(Config);
            });

        api.AddSectionTitle(ModManifest, () => "Canopy Lighting");
        api.AddParagraph(ModManifest, () => "Lights tree canopies only. It does not draw extra shadows on the ground; vanilla ground shadows are unchanged.");

        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.Enabled,
            setValue: value => Config.Enabled = value,
            name: () => "Enable canopy lighting",
            tooltip: () => "Turn the canopy-lighting effect on or off.");

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.OverallStrength,
            setValue: value => Config.OverallStrength = value,
            name: () => "Overall strength",
            tooltip: () => "Master strength for all canopy lighting.",
            min: 0f,
            max: 2f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        api.AddSectionTitle(ModManifest, () => "Broadleaf and Other Trees");
        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.EnableOakCanopy,
            setValue: value => Config.EnableOakCanopy = value,
            name: () => "Enable broadleaf profile",
            tooltip: () => "Applies to mature non-pine wild trees, such as oak, maple, mahogany, palm, mushroom and mystic trees.");

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.OakLightStrength,
            setValue: value => Config.OakLightStrength = value,
            name: () => "Broadleaf light strength",
            tooltip: () => "Brightness applied to the lit portion of the canopy.",
            min: 0f,
            max: 5f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.OakShadowStrength,
            setValue: value => Config.OakShadowStrength = value,
            name: () => "Broadleaf canopy shading",
            tooltip: () => "Darkening on the back-facing canopy pixels only; no ground shadow is drawn.",
            min: 0f,
            max: 3f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        api.AddSectionTitle(ModManifest, () => "Pine Trees");
        api.AddBoolOption(
            ModManifest,
            getValue: () => Config.EnablePineCanopy,
            setValue: value => Config.EnablePineCanopy = value,
            name: () => "Enable pine lighting",
            tooltip: () => "Apply the texture-guided lighting profile to mature pine canopies.");

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.PineLightStrength,
            setValue: value => Config.PineLightStrength = value,
            name: () => "Pine light strength",
            tooltip: () => "Brightness on lit pine-canopy pixels.",
            min: 0f,
            max: 5f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.PineShadowStrength,
            setValue: value => Config.PineShadowStrength = value,
            name: () => "Pine canopy shading",
            tooltip: () => "Darkening on the back-facing pine canopy pixels only; no ground shadow is drawn.",
            min: 0f,
            max: 5f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        api.AddNumberOption(
            ModManifest,
            getValue: () => Config.PineRimStrength,
            setValue: value => Config.PineRimStrength = value,
            name: () => "Pine rim lighting",
            tooltip: () => "Additional light along the sun-facing pine-canopy edges.",
            min: 0f,
            max: 5f,
            interval: 0.05f,
            formatValue: value => value.ToString("0.00"));

        Monitor.Log("Registered canopy lighting settings with Generic Mod Config Menu.", LogLevel.Info);
    }

    private void ClampConfig()
    {
        Config.OverallStrength = Math.Clamp(Config.OverallStrength, 0f, 2f);
        Config.OakLightStrength = Math.Clamp(Config.OakLightStrength, 0f, 5f);
        Config.OakShadowStrength = Math.Clamp(Config.OakShadowStrength, 0f, 3f);
        Config.PineLightStrength = Math.Clamp(Config.PineLightStrength, 0f, 5f);
        Config.PineShadowStrength = Math.Clamp(Config.PineShadowStrength, 0f, 5f);
        Config.PineRimStrength = Math.Clamp(Config.PineRimStrength, 0f, 5f);
    }
}
