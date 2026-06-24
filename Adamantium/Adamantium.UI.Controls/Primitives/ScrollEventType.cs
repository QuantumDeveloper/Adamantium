namespace Adamantium.UI.Controls.Primitives;

/// <summary>What caused a <see cref="ScrollBar.Scroll"/> - mirrors WPF's ScrollEventType.</summary>
public enum ScrollEventType
{
    /// <summary>A line button / arrow towards the minimum (SmallChange).</summary>
    SmallDecrement,

    /// <summary>A line button / arrow towards the maximum (SmallChange).</summary>
    SmallIncrement,

    /// <summary>A page click towards the minimum (LargeChange).</summary>
    LargeDecrement,

    /// <summary>A page click towards the maximum (LargeChange).</summary>
    LargeIncrement,

    /// <summary>The thumb was dragged (continuous).</summary>
    ThumbTrack,

    /// <summary>Jumped to the minimum.</summary>
    First,

    /// <summary>Jumped to the maximum.</summary>
    Last,

    /// <summary>The thumb drag finished.</summary>
    EndScroll
}
