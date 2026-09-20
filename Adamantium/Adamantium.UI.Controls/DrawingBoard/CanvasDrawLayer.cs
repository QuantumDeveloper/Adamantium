using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Draws ONE RUN of an <see cref="InfiniteCanvas"/>'s scene - the items between two controls.
/// <para>It exists because the plane has ONE order and everything on it stands in that order, controls included. A
/// control has to be a real child of a layer to be laid out, drawn and clicked at all, and a layer is one place in
/// paint order - so a picture with a stroke over it and another picture over that is three places, and they have to be
/// three layers. What makes that affordable is that a layer is made per RUN of neighbours of the same sort and not per
/// item: a drawing with two pictures in it is three layers whatever else is on the plane, and a graph of ten thousand
/// nodes with no drawing between them is still one.</para>
/// <para>It draws and nothing else: it takes no part in hit-testing, so what is under it stays clickable. Picking is
/// the canvas's own walk over the scene, which sees the same order this does.</para></summary>
public class CanvasDrawLayer : MeasurableUIComponent
{
    private readonly List<ICanvasItem> _items = new();

    /// <summary>The canvas whose items this draws. Written by the canvas when it makes the layer.</summary>
    public InfiniteCanvas Owner { get; set; }

    public CanvasDrawLayer()
    {
        IsHitTestVisible = false;
    }

    /// <summary>What this run holds now. Cheap to call: it does nothing when the run is unchanged, which is what every
    /// camera move finds.</summary>
    public void Sync(List<ICanvasItem> items)
    {
        if (Same(items)) return;

        _items.Clear();
        _items.AddRange(items);

        InvalidateRender(false);
    }

    /// <summary>What this layer paints, in the order it paints it. For anything that has to check the order without
    /// looking at pixels.</summary>
    public ICanvasItem[] Painted => _items.ToArray();

    /// <summary>Says the items have changed WITHOUT the run changing - a stroke grown by another point, a shape
    /// dragged. The list is the same objects, so <see cref="Sync"/> would do nothing.</summary>
    public void Repaint() => InvalidateRender(false);

    private bool Same(List<ICanvasItem> items)
    {
        if (items.Count != _items.Count) return false;

        for (var i = 0; i < items.Count; i++)
        {
            if (!ReferenceEquals(items[i], _items[i])) return false;
        }

        return true;
    }

    // THE WHOLE SLOT. A layer that draws the plane covers the plane: asked how much room it wants it used to answer
    // "none" - it has no children and nothing of its own to measure - and a thing of no size draws nothing, wherever
    // its items happen to be. What it draws is placed from the camera and not from this box, so the box is simply
    // whatever it has been given.
    protected override Size MeasureOverride(Size availableSize) =>
        new(Double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            Double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);

        if (Owner == null || _items.Count == 0) return;

        var session = context.ForControl(this);

        // THE FRAME STANDS WHERE ITS OBJECT STANDS. Drawn over everything, it said nothing about where the thing it is
        // round actually is - and where a thing is in the order is the one question a person moving it is asking. So
        // the canvas is asked at each step whether the frame belongs HERE: before this run, when what is held is a
        // control in the run below, and straight after the item itself otherwise.
        if (Owner.ChromeGoesBefore(_items[0])) Owner.DrawChrome(session);

        foreach (var item in _items)
        {
            item.Render(session, Owner);

            if (Owner.ChromeGoesAfter(item)) Owner.DrawChrome(session);
        }
    }
}
