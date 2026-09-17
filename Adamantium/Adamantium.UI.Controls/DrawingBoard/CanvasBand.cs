namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Which side of the canvas's hosted CONTROLS a thing is drawn on.
/// <para>A band and not a number, because that is the only ordering a layered UI can actually give. A control on the
/// plane has to be a real child of a layer to be laid out, drawn and clicked at all, and a layer is one place in paint
/// order - so a free z-index per item would mean a layer per item, which is the cost that makes a plane of ten
/// thousand things impossible. Every node editor worth the name works this way: a fixed handful of bands - comments,
/// wires, nodes, annotations - with free ordering INSIDE a band and none across them.</para>
/// <para>Within a band the scene's own order decides, which is what "bring to front" and "send to back" move.</para>
/// </summary>
public enum CanvasBand
{
    /// <summary>Drawn BEHIND the controls. Where the drawing is: strokes, shapes, wires, the frames and comments a
    /// graph is grouped by.</summary>
    Under,

    /// <summary>Drawn IN FRONT of the controls. For the things that are ABOUT what is on the plane rather than part of
    /// it - an arrow pointing at a node, a measurement, a note.</summary>
    Over
}
