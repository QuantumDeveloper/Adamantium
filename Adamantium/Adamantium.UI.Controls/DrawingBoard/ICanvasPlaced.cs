using System.ComponentModel;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WHERE something of the application's stands on the plane - the little that a node and a drawn thing have in
/// common, and all that what draws them needs in order to read and write a place.
/// <para>Two numbers rather than a point, because a point cannot be half-written: an inspector line editing the left
/// edge has to have something to bind to.</para></summary>
public interface ICanvasPlaced : INotifyPropertyChanged
{
    double Left { get; set; }

    double Top { get; set; }

    /// <summary>How wide it was pulled, in world units. Zero leaves it to the contents.</summary>
    double Width { get; set; }
}
