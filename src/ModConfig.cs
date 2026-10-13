namespace CanopyLighting;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Strength of the warm canopy highlight, from 0 to 1.</summary>
    public float Strength { get; set; } = 0.22f;

    /// <summary>Warmth of the canopy highlight, from 0 to 1.</summary>
    public float Warmth { get; set; } = 0.35f;
}