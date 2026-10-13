using System;
using StardewModdingAPI;

namespace CanopyLighting;

/// <summary>
/// Minimal copy of the public GMCM API interface for the methods used by this mod.
/// Generic Mod Config Menu remains an optional dependency.
/// </summary>
internal interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

    void AddParagraph(IManifest mod, Func<string> text);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null);

    void AddNumberOption(
        IManifest mod,
        Func<float> getValue,
        Action<float> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        float? min = null,
        float? max = null,
        float? interval = null,
        Func<float, string>? formatValue = null,
        string? fieldId = null);
}
