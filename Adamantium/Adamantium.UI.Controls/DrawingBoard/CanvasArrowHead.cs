namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>What sits on the end of an arrow.</summary>
public enum CanvasArrowHead
{
    /// <summary>Nothing - a plain end.</summary>
    None,

    /// <summary>Two strokes back from the tip, drawn with the line's own pen. The plainest arrow there is, and the one
    /// that stays readable when the line is thin.</summary>
    Barbs,

    /// <summary>A filled triangle. Heavier, and what a diagram usually wants: it reads as a direction at a glance and
    /// at any size.</summary>
    Triangle
}
