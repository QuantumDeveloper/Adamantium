using System.Collections.Generic;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.Models;

/// <summary>A WHOLE CATALOGUE of kinds, under a name - what a graph is made with.
/// <para>An application doing two different jobs has two of these, and switches by binding the one it wants to the
/// canvas: the canvas knows nothing about sets, it is handed a list of kinds like any other. Sections inside a set are
/// the kinds' own <see cref="ICanvasNodeKind.Group"/> - a section organises one catalogue, a set IS the catalogue.
/// </para>
/// <para>The NAME is what a saved graph records, so a file made with one set is not opened with another's kinds - which
/// would find nothing by those words and come back as a plane of blank nodes.</para></summary>
public sealed class GraphNodeSet
{
    public GraphNodeSet(string name, IReadOnlyList<ICanvasNodeKind> kinds)
    {
        Name = name;
        Kinds = kinds;
    }

    public string Name { get; }

    public IReadOnlyList<ICanvasNodeKind> Kinds { get; }

    /// <summary>The kind by that word, or null when this set has no such kind.</summary>
    public ICanvasNodeKind Named(string kind)
    {
        foreach (var entry in Kinds)
        {
            if (entry.Kind == kind) return entry;
        }

        return null;
    }
}
