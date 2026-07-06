using Adamantium.MVVM;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Text tab: a message shown by TextBlocks (font size slider-driven), an editable TextBox two-way bound to the
/// same <see cref="Message"/>, and a TextBlock built from bindable <c>Run</c>s - the accent run's Text binds to Message
/// (so it live-updates while you type) and its Foreground binds to <see cref="AccentBrush"/>.</summary>
[ViewModel]
public partial class TextViewModel : TabPageViewModel
{
    public TextViewModel() : base("Text") { }

    [Bindable, Affects(nameof(CharInfo))] private string _message = "The quick brown fox jumps over the lazy dog";

    [Bindable] private double _fontSize = 22;

    // Bound by a Run's Foreground - demonstrates that a Run's colour is bindable too, not just its text.
    [Bindable] private Brush _accentBrush = new SolidColorBrush("#22D3EE");

    public string CharInfo => $"  ({Message?.Length ?? 0} chars)";
}
