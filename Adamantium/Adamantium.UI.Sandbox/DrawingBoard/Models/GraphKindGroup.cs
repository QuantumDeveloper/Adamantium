using System.Collections.Generic;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.Models;

/// <summary>One SECTION of the palette - a family of kinds under its name, with the kinds as its children.
/// <para>A tree and not a flat list with a word beside each row: what a person looks for is "something that mixes", and
/// a family is the answer to that. The section is only a way of showing the catalogue - the catalogue itself is a flat
/// list of kinds, and what a node IS has nothing to do with which section it was picked from.</para></summary>
public sealed class GraphKindGroup
{
    public GraphKindGroup(string title, IReadOnlyList<ICanvasNodeKind> kinds, bool expanded)
    {
        Title = title;
        Kinds = kinds;
        IsExpanded = expanded;
    }

    /// <summary>The family's name, shown the same way a kind's is - one template draws both.</summary>
    public string Title { get; }

    public IReadOnlyList<ICanvasNodeKind> Kinds { get; }

    /// <summary>Open or shut. Open while a search is narrowing the list: a section that hid what the search just found
    /// would make the search look broken.</summary>
    public bool IsExpanded { get; set; }
}
