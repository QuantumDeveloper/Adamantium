using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>What a SOCKET may carry on this page, and what each of those looks like.
/// <para>The color belongs to the kind: a graph is read by color, and two sockets carrying a number that were
/// colored one at a time would drift the first time somebody picked the wrong shade. One list, read by the
/// inspector's drop-down and by whatever paints a pin.</para></summary>
public sealed class GraphSocketKind : ICanvasSocketKind
{
    public GraphSocketKind(string kind, Color? color = null)
    {
        Kind = kind;
        Color = color;
    }

    public string Kind { get; }

    public Color? Color { get; }

    /// <summary>Shown in the drop-down, which takes the item itself. An empty kind reads as "anything".</summary>
    public override string ToString() => string.IsNullOrEmpty(Kind) ? "anything" : Kind;

    public static IReadOnlyList<ICanvasSocketKind> All { get; } =
    [
        // FIRST and colorless: a socket that has not been told what it carries takes anything, and wears whatever the
        // theme paints a pin with.
        new GraphSocketKind(string.Empty),

        new GraphSocketKind(GraphWords.Flows.Number, Adamantium.Mathematics.Color.FromRgba(120, 190, 230, 255)),
        new GraphSocketKind(GraphWords.Flows.Color, Adamantium.Mathematics.Color.FromRgba(230, 170, 90, 255)),
        new GraphSocketKind(GraphWords.Flows.Vector, Adamantium.Mathematics.Color.FromRgba(160, 130, 220, 255)),
        new GraphSocketKind(GraphWords.Flows.Texture, Adamantium.Mathematics.Color.FromRgba(90, 200, 150, 255)),
        new GraphSocketKind(GraphWords.Flows.Bool, Adamantium.Mathematics.Color.FromRgba(220, 120, 140, 255))
    ];
}
