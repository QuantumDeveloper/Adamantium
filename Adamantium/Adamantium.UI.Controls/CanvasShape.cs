namespace Adamantium.UI.Controls;

/// <summary>What a <see cref="ShapeItem"/> IS. One item type rather than three, because all three are the same thing to
/// everything around them - a box in the world that can be moved, resized, hit and drawn.</summary>
public enum CanvasShape
{
    Rectangle,
    Ellipse,

    /// <summary>A straight line from one corner of the box to the other. Its box is what the drag made, so which way it
    /// leans is remembered by the box being read either way round.</summary>
    Line,

    /// <summary>A regular polygon inscribed in the box - a triangle, a pentagon, a hexagon. How many sides is
    /// <see cref="ShapeItem.Sides"/>, so one shape covers all of them rather than an enum entry each.</summary>
    Polygon,

    /// <summary>A line with a HEAD on one end, the other, or both - see <see cref="ShapeItem.StartHead"/> and
    /// <see cref="ShapeItem.EndHead"/>. Its own shape rather than a switch on <see cref="Line"/>, so that an inspector
    /// offering head settings offers them exactly where they mean something.</summary>
    Arrow
}
