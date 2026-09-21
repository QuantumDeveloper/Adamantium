namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>ONE KIND a node can be, as the application offers it - an entry of the catalogue bound to the canvas.
/// <para>What replaced a factory handed to the control: the list of kinds is DATA, and an entry of it is asked for a
/// node when the canvas needs one - from the palette, from the tool that places one, from a wire let go over nothing.
/// So there is no delegate on the canvas's surface, and no node objects of the engine's own either: what a node IS
/// belongs to the application, and the control does with it what a generator does with an item.</para></summary>
public interface ICanvasNodeKind
{
    /// <summary>The word a node of this kind carries, and what a file names it by.</summary>
    string Kind { get; }

    /// <summary>What the palette shows. The kind itself when there is nothing better to say.</summary>
    string Title { get; }

    /// <summary>WHICH FAMILY OF WORK it belongs to - "Math", "Colour". What a palette puts its sections in, the same way
    /// the tool rail groups tools; empty means it belongs to no section and stands on its own.
    /// <para>A section is not a SET: a set of kinds is a whole catalogue bound to the canvas, and a graph made with one
    /// is not made with another. Sections organise what is inside one catalogue.</para></summary>
    string Group => string.Empty;

    /// <summary>WHAT COLOUR a node of this kind is - what a screenful of them is read by at a glance, and the colour a
    /// new one is born with. Null leaves it to the theme, which is what a catalogue that has not been asked to think
    /// about colour says.</summary>
    Adamantium.Mathematics.Color? Accent { get; }

    /// <summary>A fresh specialization of this kind - one per node, never shared.</summary>
    ICanvasNodeSpecialization Create();

    /// <summary>A WHOLE NODE of this kind, ready to join the graph - the application's own object, with this kind's
    /// specialization already in it.
    /// <para>Asked of the catalogue because a node is the application's to define: the canvas holds no node type of its
    /// own to fall back on, exactly as an items control holds no item type. Where it goes is the canvas's business and
    /// is set after this returns; everything else about it is the application's.</para></summary>
    ICanvasNode Make();
}