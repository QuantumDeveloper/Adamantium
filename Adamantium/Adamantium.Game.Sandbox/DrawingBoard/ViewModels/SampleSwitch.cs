using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>A TICK BOX on the plane, as data - and one that holds a state, which is the half a plain button cannot
/// show: what a person set on the plane has to survive being scrolled away from and back.</summary>
[ViewModel]
public partial class SampleSwitch
{
    public SampleSwitch(string text) => _text = text;

    /// <summary>What it says beside the box.</summary>
    [Bindable] private string _text = string.Empty;

    /// <summary>Whether it is ticked - the state the page keeps, not the control.</summary>
    [Bindable] private bool _isOn;
}
