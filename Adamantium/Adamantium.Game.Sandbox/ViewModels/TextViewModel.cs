using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Text tab: a message shown by TextBlocks whose font size is driven by a slider (all through the view-model).
/// When an editable TextBox lands it will two-way bind to the same <see cref="Message"/>.</summary>
[ViewModel]
public partial class TextViewModel : TabPageViewModel
{
    public TextViewModel() : base("Text") { }

    [Bindable] private string _message = "The quick brown fox jumps over the lazy dog";

    [Bindable] private double _fontSize = 22;
}
