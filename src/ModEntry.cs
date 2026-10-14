using System.Reflection;
using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>Loads standalone canopy lighting and registers GMCM options through a public API interface.</summary>
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
        }
        catch (Exception ex)
        {
            Monitor.Log("Couldn't install the standalone canopy hook: " + ex.Message, LogLevel.Error);
            return;
        }

        // SMAPI explicitly requires mod-provided API calls to happen after all mods initialize.
        // GameLaunched is the safe point for retrieving and registering the optional GMCM API.
        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            try
            {
                RegisterConfigMenu(helper);
            }
            catch (Exception ex)
            {
                Monitor.Log("Canopy Lighting is active, but GMCM options couldn't be registered: " + ex.GetType().Name + ": " + ex.Message, LogLevel.Warn);
            }
        };

        Monitor.Log(
            $"Canopy Lighting enabled={Config.Enabled}, strength={Config.OverallStrength:0.00}, broadleaf/other={Config.EnableOakCanopy}, pine={Config.EnablePineCanopy}.",
            LogLevel.Info);
    }

    private void RegisterConfigMenu(IModHelper helper)
    {
        // Use SMAPI's non-generic GetApi overload to get the raw GMCM object.
        // This avoids Pintail's public-interface mapping, which is not reliable on
        // some Android SMAPI forks. All access happens after GameLaunched.
        object? api = helper.ModRegistry.GetApi("spacechase0.GenericModConfigMenu");
        if (api is null)
        {
            Monitor.Log("Generic Mod Config Menu wasn't found; settings remain editable in config.json.", LogLevel.Debug);
            return;
        }

        InvokeApi(api, "Register",
            null,
            (Action)(() =>
            {
                Config = new ModConfig();
                ClampConfig();
                TreeCanopyLighting.UpdateConfig(Config);
                helper.WriteConfig(Config);
            }),
            (Action)(() =>
            {
                ClampConfig();
                TreeCanopyLighting.UpdateConfig(Config);
                helper.WriteConfig(Config);
            }),
            false);

        InvokeApi(api, "AddSectionTitle", null, (Func<string>)(() => "Canopy Lighting"), null);
        InvokeApi(api, "AddParagraph", null, (Func<string>)(() => "Lights tree canopies only. It does not draw extra shadows on the ground; vanilla ground shadows are unchanged."));

        InvokeApi(api, "AddBoolOption", null,
            (Func<bool>)(() => Config.Enabled),
            (Action<bool>)(value => Config.Enabled = value),
            (Func<string>)(() => "Enable canopy lighting"),
            (Func<string>)(() => "Turn the canopy-lighting effect on or off."),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.OverallStrength),
            (Action<float>)(value => Config.OverallStrength = value),
            (Func<string>)(() => "Overall strength"),
            (Func<string>)(() => "Master strength for all canopy lighting."),
            (float?)0f, (float?)1.25f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        InvokeApi(api, "AddSectionTitle", null, (Func<string>)(() => "Broadleaf and Other Trees"), null);
        InvokeApi(api, "AddBoolOption", null,
            (Func<bool>)(() => Config.EnableOakCanopy),
            (Action<bool>)(value => Config.EnableOakCanopy = value),
            (Func<string>)(() => "Enable broadleaf profile"),
            (Func<string>)(() => "Applies to mature non-pine wild trees, such as oak, maple, mahogany, palm, mushroom and mystic trees."),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.OakLightStrength),
            (Action<float>)(value => Config.OakLightStrength = value),
            (Func<string>)(() => "Broadleaf light strength"),
            (Func<string>)(() => "Brightness applied to the lit portion of the canopy."),
            (float?)0f, (float?)1.75f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.OakShadowStrength),
            (Action<float>)(value => Config.OakShadowStrength = value),
            (Func<string>)(() => "Broadleaf canopy shading"),
            (Func<string>)(() => "Darkening on back-facing canopy pixels only; no ground shadow is drawn."),
            (float?)0f, (float?)3f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        InvokeApi(api, "AddSectionTitle", null, (Func<string>)(() => "Pine Trees"), null);
        InvokeApi(api, "AddBoolOption", null,
            (Func<bool>)(() => Config.EnablePineCanopy),
            (Action<bool>)(value => Config.EnablePineCanopy = value),
            (Func<string>)(() => "Enable pine lighting"),
            (Func<string>)(() => "Apply the texture-guided lighting profile to mature pine canopies."),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.PineLightStrength),
            (Action<float>)(value => Config.PineLightStrength = value),
            (Func<string>)(() => "Pine light strength"),
            (Func<string>)(() => "Brightness on lit pine-canopy pixels."),
            (float?)0f, (float?)3f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.PineShadowStrength),
            (Action<float>)(value => Config.PineShadowStrength = value),
            (Func<string>)(() => "Pine canopy shading"),
            (Func<string>)(() => "Darkening on back-facing pine canopy pixels only; no ground shadow is drawn."),
            (float?)0f, (float?)3f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        InvokeApi(api, "AddNumberOption", null,
            (Func<float>)(() => Config.PineRimStrength),
            (Action<float>)(value => Config.PineRimStrength = value),
            (Func<string>)(() => "Pine rim lighting"),
            (Func<string>)(() => "Additional light along sun-facing pine-canopy edges."),
            (float?)0f, (float?)3f, (float?)0.05f,
            (Func<float, string>)(value => value.ToString("0.00")),
            null);

        Monitor.Log("Registered canopy lighting settings with Generic Mod Config Menu.", LogLevel.Info);
    }

    private static object? InvokeApi(object api, string methodName, params object?[] arguments)
    {
        MethodInfo? method = api.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length)
            .FirstOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    Type? nullableType = Nullable.GetUnderlyingType(parameterType);
                    object? argument = arguments[i];

                    if (argument is null)
                    {
                        if (parameterType.IsValueType && nullableType is null)
                            return false;
                        continue;
                    }

                    if (!parameterType.IsInstanceOfType(argument)
                        && (nullableType is null || !nullableType.IsInstanceOfType(argument)))
                        return false;
                }

                return true;
            });

        if (method is null)
        {
            string argumentTypes = string.Join(", ", arguments.Select(argument => argument?.GetType().FullName ?? "null"));
            throw new MissingMethodException($"GMCM API method {methodName}({argumentTypes}) wasn't found on {api.GetType().FullName}.");
        }

        try
        {
            return method.Invoke(api, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"GMCM API method {methodName} failed: {ex.InnerException.Message}", ex.InnerException);
        }
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
