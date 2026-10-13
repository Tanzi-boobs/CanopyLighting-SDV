namespace CanopyLighting;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Overall canopy overlay strength, from 0 (off) to 1 (strong).</summary>
    public float Strength { get; set; } = 0.35f;

    /// <summary>Warmth of the sunlit canopy tint, from 0 (neutral) to 1 (golden).</summary>
    public float Warmth { get; set; } = 0.35f;
}