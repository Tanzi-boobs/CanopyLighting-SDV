namespace CanopyLighting;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Master multiplier for the imported Dynamic Shader canopy effect (0-2).</summary>
    public float OverallStrength { get; set; } = 1.0f;

    public bool EnableOakCanopy { get; set; } = true;
    public bool EnablePineCanopy { get; set; } = true;

    public float OakLightStrength { get; set; } = 1.54f;
    public float OakShadowStrength { get; set; } = 2.97f;
    public float PineLightStrength { get; set; } = 3.0f;
    public float PineShadowStrength { get; set; } = 2.02f;
    public float PineRimStrength { get; set; } = 2.67f;
}
