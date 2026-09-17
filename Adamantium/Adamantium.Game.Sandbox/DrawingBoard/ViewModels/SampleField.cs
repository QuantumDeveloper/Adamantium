using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>A FIELD on the plane, as data. The one that answers the hardest question of a zooming plane: typing into a
/// control that is being scaled - where the caret is, whether the focus survives, what the glyphs look like.</summary>
[ViewModel]
public partial class SampleField
{
    public SampleField(string text) => _text = text;

    /// <summary>What is typed in it. The page's own text - the control only shows it.</summary>
    [Bindable] private string _text = string.Empty;
}
