namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A card shown in the ItemsControl region: several are visible AT ONCE (no selection). Demonstrates
/// ItemsControlRegionAdapter, in contrast to the single-view ContentControl region beside it.</summary>
public class CardPageViewModel
{
    public CardPageViewModel(int number) => Title = $"Card {number}";

    public string Title { get; }
}
