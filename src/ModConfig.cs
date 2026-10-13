namespace CanopyLighting;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Master multiplier for canopy-only lighting (0-2).</summary>
    public float OverallStrength { get; set; } = 1.0f;

    /// <summary>Enables the broadleaf profile on oak, maple, palm, mushroom, mahogany, mystic and other non-pine wild trees.</summary>
    public bool EnableOakCanopy { get; set; } = true;

    /// <summary>Enables the texture-guided profile on mature pine trees.</summary>
    public bool EnablePineCanopy { get; set; } = true;

    /// <summary>Broadleaf-profile light strength, applied to every non-pine wild-tree canopy.</summary>
    public float OakLightStrength { get; set; } = 1.54f;

    /// <summary>Broadleaf-profile back-facing canopy shading. Does not draw a ground shadow.</summary>
    public float OakShadowStrength { get; set; } = 2.97f;

    public float PineLightStrength { get; set; } = 3.0f;
    public float PineShadowStrength { get; set; } = 2.02f;
    public float PineRimStrength { get; set; } = 2.67f;
}
