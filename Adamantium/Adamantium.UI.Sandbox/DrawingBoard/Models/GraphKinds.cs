using System;
using System.Collections.Generic;
using Adamantium.UI.Sandbox.DrawingBoard.ViewModels;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;
using static Adamantium.UI.Sandbox.DrawingBoard.Models.GraphWords;

namespace Adamantium.UI.Sandbox.DrawingBoard.Models;

/// <summary>What a node of each kind is MADE of - its sockets, its color, the section it stands in and what it does -
/// and the ONE place that says so. The palette, the inspector's drop-down and the loader all read this list.</summary>
public sealed class GraphNodeKind : ICanvasNodeKind
{
    private readonly Func<NodeSpecialization> _make;

    public GraphNodeKind(string kind, Func<NodeSpecialization> make,
        (string Name, string Kind)[] inputs, (string Name, string Kind)[] outputs,
        Color? accent = null, string group = "", int takes = 1)
    {
        Kind = kind;
        _make = make;
        In = inputs;
        Out = outputs;
        Accent = accent;
        Group = group;
        Takes = takes;
    }

    public string Kind { get; }

    /// <summary>The same word: these kinds have nothing prettier to be called, and a title that merely repeats the kind
    /// is not a second thing to keep in step.</summary>
    public string Title => Kind;

    /// <summary>Which section of the palette it stands in.</summary>
    public string Group { get; }

    /// <summary>What color a node of this kind is born.</summary>
    public Color? Accent { get; }

    public (string Name, string Kind)[] In { get; }

    public (string Name, string Kind)[] Out { get; }

    /// <summary>How many wires each input takes. Zero means as many as come - which is what a merge is.</summary>
    public int Takes { get; }

    public ICanvasNodeSpecialization Create()
    {
        var made = _make();

        made.In = In;
        made.Out = Out;
        made.Takes = Takes;

        return made;
    }

    /// <summary>A WHOLE NODE of this kind, which is what the canvas asks for when it needs one - from the palette, from
    /// the tool that places one, from a wire let go over nothing. The node is this page's object; the canvas only says
    /// where it goes.</summary>
    public ICanvasNode Make()
    {
        var made = new CanvasNodeViewModel
        {
            Kind = Kind,
            Title = Title,

            // BORN WITH ITS KIND'S COLOUR, so a node has one from the start and the line that edits it has something to
            // show.
            Accent = Accent
        };

        // The specialization LAST: installing it shapes the node, and what it shapes must be a node that is otherwise
        // finished.
        made.Specialization = Create();

        return made;
    }

    /// <summary>Shown in the palette and in the drop-down, which take the item itself.</summary>
    public override string ToString() => Title;

    /// <summary>The kinds this page offers, and the set they belong to: a small shading graph - colors in, one color
    /// out - which is a graph that DOES something rather than a picture of one.</summary>
    public static GraphNodeSet Set { get; } = new("Shading",
    [
        new GraphNodeKind(Kinds.Color, () => new ColorSpecialization(),
            [], [(Sockets.Out, Flows.Color)], Source, Groups.Source),

        new GraphNodeKind(Kinds.Number, () => new NumberSpecialization(),
            [], [(Sockets.Out, Flows.Number)], Source, Groups.Source),

        new GraphNodeKind(Kinds.Mix, () => new MixSpecialization(),
            [(Sockets.A, Flows.Color), (Sockets.B, Flows.Color), (Sockets.Amount, Flows.Number)],
            [(Sockets.Out, Flows.Color)], Maths, Groups.Blend),

        new GraphNodeKind(Kinds.Add, () => new AddSpecialization(),
            [(Sockets.A, Flows.Color), (Sockets.B, Flows.Color)], [(Sockets.Out, Flows.Color)], Maths, Groups.Blend),

        new GraphNodeKind(Kinds.Scale, () => new ScaleSpecialization(),
            [(Sockets.Color, Flows.Color), (Sockets.By, Flows.Number)], [(Sockets.Out, Flows.Color)], Maths,
            Groups.Blend),

        new GraphNodeKind(Kinds.Gray, () => new GraySpecialization(),
            [(Sockets.Color, Flows.Color)], [(Sockets.Out, Flows.Color)], Maths, Groups.Blend),

        // A MERGE: its one input takes as many wires as anybody brings, which is the many-to-one case a graph has to be
        // able to say - and here it MEANS something, the average of everything that arrives.
        new GraphNodeKind(Kinds.Merge, () => new MergeSpecialization(),
            [(Sockets.Colors, Flows.Color)], [(Sockets.Out, Flows.Color)], Maths, Groups.Blend, takes: 0),

        new GraphNodeKind(Kinds.Output, () => new OutputSpecialization(),
            [(Sockets.Color, Flows.Color)], [], Sink, Groups.Result)
    ]);

    public static IReadOnlyList<ICanvasNodeKind> All => Set.Kinds;

    // ONE COLOUR PER FAMILY OF WORK - where a value comes from, what is done to it, where it ends. What makes a
    // screenful of nodes readable without reading a single title.
    private static Color Maths => Color.FromRgba(58, 120, 210, 255);

    private static Color Source => Color.FromRgba(58, 160, 110, 255);

    private static Color Sink => Color.FromRgba(190, 90, 60, 255);

    /// <summary>The entry for a word, or null when this page offers no such kind.</summary>
    public static ICanvasNodeKind Named(string kind) => Set.Named(kind);
}
