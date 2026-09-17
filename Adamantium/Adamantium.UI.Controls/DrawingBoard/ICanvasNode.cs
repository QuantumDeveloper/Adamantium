using System.ComponentModel;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A NODE of a graph, as the application holds it. One contract for every sort of node: what sort it is, is
/// data - see <see cref="Kind"/>.
/// <para>The canvas takes a collection of these and makes a <see cref="CanvasNode"/> for each; everything visual - the
/// strip, the sockets, the body, dragging, the frame, folding, the wires - is the canvas's. What is asked here is only
/// what it cannot do without.</para>
/// <para>The collection is written from BOTH sides: a node made on the plane appears in it, and one removed from it
/// leaves the plane. So this is the application's own object and the only source of truth about the graph - the canvas
/// keeps no second copy of it.</para></summary>
public interface ICanvasNode : ICanvasPlaced
{
    /// <summary>What sort of node this is, in the application's own words - "Multiply", "Texture".
    /// <para>A string and not a type, for two reasons. Nodes are made INSIDE the canvas - somebody picks from the
    /// palette or drags one out with a tool - and there the canvas has a word and nothing else. And a kind that was a
    /// type would make changing it a new object, so a pick in a drop-down would have to re-point every wire, the
    /// selection and the undo step.</para></summary>
    string Kind { get; set; }

    /// <summary>What the strip says. Written as well as read: renaming a node in an inspector belongs to the node.
    /// </summary>
    string Title { get; set; }

    /// <summary>ITS COLOUR - the strip a graph is read by at a glance. On the node and not on the control drawn for it,
    /// because a colour a person chose is part of the document: it has to survive the container being rebuilt, and it
    /// has to be saved with everything else.
    /// <para>A COLOUR and not a brush. A brush is a thing that paints and can be shared - written into, it recolours
    /// everything else pointed at it, which is how one node's strip took the theme's accent with it. A colour is a
    /// value: the brush that paints with it is made where the painting happens. Null wears the theme's.</para></summary>
    Color? Accent { get; set; }

    /// <summary>Folded down to its title strip.</summary>
    bool IsCollapsed { get; set; }

    /// <summary>The sockets down each side. Observable because an inspector adds and drops them one at a time, and a
    /// wire has to hear about the socket it is sitting on going away.</summary>
    TrackingCollection<ICanvasSocket> Inputs { get; }

    TrackingCollection<ICanvasSocket> Outputs { get; }

    /// <summary>WHAT THIS NODE IS - the part that differs between kinds, and the only part a change of kind replaces.
    /// It shapes the sockets when it is installed and stands as the node's content, drawn by the template chosen for
    /// its type. The canvas never looks inside it.</summary>
    ICanvasNodeSpecialization Specialization { get; set; }
}
