using System;

namespace CanopyLighting;

/// <summary>
/// Public interface used to map GMCM's API on Android SMAPI builds where IManifest isn't available.
/// The mod argument is typed as object and passed as null so GMCM uses the manifest of the requesting mod.
/// </summary>
public interface IGenericModConfigMenuApi
{
    void Register(object? mod, Action reset, Action save, bool titleScreenOnly = false);
    void AddSectionTitle(object? mod, Func<string> text, Func<string>? tooltip = null);
    void AddParagraph(object? mod, Func<string> text);
    void AddBoolOption(object? mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddNumberOption(object? mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);
}
