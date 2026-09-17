namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One socket of a saved node.</summary>
public sealed class CanvasSocketSeed
{
    public string Name { get; init; } = string.Empty;

    /// <summary>The socket's own colour as "#AARRGGBB", or empty to take the node's.</summary>
    public string Color { get; init; } = string.Empty;
}
