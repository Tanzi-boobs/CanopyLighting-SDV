namespace DoubleTapHUD;

internal sealed class ModConfig
{
    public int DoubleTapWindowMilliseconds { get; set; } = 420;
    public int MaxTapDistance { get; set; } = 72;
    public bool ClockOpensGameMenu { get; set; } = true;
    public bool DoubleTapQuestButton { get; set; } = true;
    public bool DoubleTapMapTab { get; set; } = true;
    public bool DoubleTapCraftingTab { get; set; } = true;
    public bool DoubleTapCraftingRecipes { get; set; } = true;
    public bool DoubleTapBlankMapToClose { get; set; } = true;
}