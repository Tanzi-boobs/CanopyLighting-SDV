namespace CanopyLighting;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Master multiplier for canopy-only lighting (0-1.25) to avoid overexposure.</summary>
    public float OverallStrength { get; set; } = 1.0f;

    /// <summary>Enables the broadleaf profile on oak, maple, palm, mushroom, mahogany, mystic and other non-pine wild trees.</summary>
    public bool EnableOakCanopy { get; set; } = true;

    /// <summary>Enables lighting on mature pine trees.</summary>
    public bool EnablePineCanopy { get; set; } = true;

    /// <summary>Use the more expensive texture-guided rim profile on pine trees. Off by default for mobile performance.</summary>
    public bool UseTextureGuidedPineProfile { get; set; } = false;

    /// <summary>Broadleaf-profile light strength (0-1.75), applied to every non-pine wild-tree canopy.</summary>
    public float OakLightStrength { get; set; } = 1.54f;

    /// <summary>Broadleaf-profile back-facing canopy shading. Does not draw a ground shadow.</summary>
    public float OakShadowStrength { get; set; } = 2.97f;

    /// <summary>Lightweight pine-profile brightness (0-1.75).</summary>
    public float PineLightStrength { get; set; } = 1.54f;
    /// <summary>Lightweight pine-profile shading (0-2.5).</summary>
    public float PineShadowStrength { get; set; } = 2.02f;
    public float PineRimStrength { get; set; } = 2.67f;
}
