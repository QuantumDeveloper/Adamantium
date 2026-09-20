using Adamantium.MVVM;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;


/// <summary>A BUTTON PUT ON THE PLANE, as DATA. What the page says is there; the canvas builds the control from the
/// template chosen for this type, and the page never touches one.
/// <para>It exists to be measured against: a control on a plane that zooms has to stay pressable, stay sharp and keep
/// its place, and none of that can be seen without one standing there.</para></summary>
[ViewModel]
public partial class SampleButton
{
    public SampleButton(string text) => _text = text;

    /// <summary>What it says.</summary>
    [Bindable] private string _text = string.Empty;
}
