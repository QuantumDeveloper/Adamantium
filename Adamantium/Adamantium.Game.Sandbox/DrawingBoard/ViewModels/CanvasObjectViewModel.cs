using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>THIS PAGE'S OWN thing on the plane - a plain <see cref="ICanvasObject"/>.
/// <para>An application's object and not the engine's: a control works with the data it is given and generates what
/// shows it, the way an items control does, and a view-model is the application's side of that line.</para>
/// <para>Said the way every view-model here is said - the framework's base and <c>[Bindable]</c> fields, so the
/// notification side of it is written by the generator and not by hand.</para></summary>
[ViewModel]
public partial class CanvasObjectViewModel : ICanvasObject
{
    /// <summary>Where it sits on the plane, in world units.</summary>
    [Bindable] private double _left;

    [Bindable] private double _top;

    /// <summary>How big it is. A control put on the plane is laid out inside this.</summary>
    [Bindable] private double _width;

    [Bindable] private double _height;

    /// <summary>WHAT IT IS - a shape description, a control, whatever this page puts there. The canvas chooses what
    /// draws it by the type of this.</summary>
    [Bindable] private object _content;
}
