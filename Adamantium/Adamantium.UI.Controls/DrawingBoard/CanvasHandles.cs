using System;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Which grips of the manipulation frame an item offers.
/// <para>Here because the frame used to appear round everything selected with all eight grips on it, whatever was
/// selected. That is right for a box and wrong for several things at once: a CONNECTION between two nodes has nothing
/// to resize - its ends are held by what it joins - and a CURVE is reshaped by its own points, so a box round it with
/// eight grips offers a gesture that fights the one that means something.</para></summary>
[Flags]
public enum CanvasHandles
{
    /// <summary>No frame at all. It can still be selected and moved from the list or the keyboard; what it cannot be is
    /// dragged about by a box.</summary>
    None = 0,

    /// <summary>The inside of the frame, which moves what is selected.</summary>
    Body = 1,

    /// <summary>The four corners, which resize in both directions at once.</summary>
    Corners = 2,

    /// <summary>The four middles, which resize in one.</summary>
    Sides = 4,

    /// <summary>Everything - what an ordinary box offers, and the default.</summary>
    All = Body | Corners | Sides
}
