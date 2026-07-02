using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>ScrollBar tab (kept on its own so the bars don't pull focus from the other controls): a horizontal and a
/// vertical ScrollBar are bound two-way to one <see cref="Offset"/>, so dragging either thumb moves the other and the
/// live read-out.</summary>
[ViewModel]
public partial class ScrollBarViewModel : TabPageViewModel
{
    public ScrollBarViewModel() : base("ScrollBar") { }

    [Bindable] private double _offset = 20;
}
