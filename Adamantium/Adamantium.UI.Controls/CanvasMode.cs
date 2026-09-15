namespace Adamantium.UI.Controls;

/// <summary>What the canvas is being used AS.
/// <para>Two different things share the plane, the camera and the selection, and nothing else: a drawing is ink,
/// shapes and text laid out by eye, and a graph is nodes joined by wires. Mixing them was tried and does not work -
/// they disagree about the one thing a plane has to answer, which is what is in front of what. A shape brought to the
/// front cannot go over a node, because a node is a real control in a layer and a shape is drawn by the canvas; and
/// even if it could, "this hexagon is over that node" means nothing in a graph.</para>
/// <para>So the mode says which of the two is live. Every node editor worth the name is its own editor for the same
/// reason, and it buys the same thing: the tools, the commands and the things that can be selected are all about one
/// kind of work instead of being the union of two.</para></summary>
public enum CanvasMode
{
    /// <summary>Ink, shapes, text, images - a drawing.</summary>
    Drawing,

    /// <summary>Nodes and the wires between them - a graph.</summary>
    Nodes
}
