using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Where the CONTROLS on an <see cref="InfiniteCanvas"/> live: a layer that places each one at the rectangle
/// its <see cref="ElementItem"/> claims in the world, turned into screen pixels by the camera.
/// <para>A layer rather than the canvas placing them itself, because a control has to be a real child of something to
/// be laid out, drawn and given input at all - which is the whole point of putting one here instead of drawing a
/// picture of it.</para>
/// <para>Panning and zooming re-ARRANGE it and nothing more: the size a control was measured at only changes when its
/// own rectangle does, so the camera never re-measures what is inside one.</para></summary>
public class CanvasElementLayer : Panel
{
    private readonly List<ElementItem> _items = new();

    /// <summary>The canvas whose camera places this layer's children.</summary>
    public InfiniteCanvas Owner { get; set; }

    /// <summary>NOT ITSELF A TARGET - the same reason <see cref="CanvasChromeLayer"/> is not. A panel catches the mouse
    /// across its whole box here, and this one is the size of the canvas: left as it was, the plane underneath could
    /// not be pressed at all. The controls it holds are still hit-tested; the empty space between them is not.</summary>
    public override bool HitTestCore(Vector2 localPoint) => false;

    public CanvasElementLayer()
    {
        // Its OWN event, so there is nothing to let go of: MouseDown bubbles, which is what lets one handler here stand
        // for every node the layer will ever hold.
        MouseDown += OnPressed;
    }

    /// <summary>Puts the layer's children in step with the items it should be showing. Cheap to call - it returns having
    /// done nothing when the set is unchanged, which is what every camera move finds.</summary>
    public void Sync(List<ElementItem> items)
    {
        if (Same(items)) return;

        _items.Clear();
        _items.AddRange(items);

        // WHAT CHANGED AND NOTHING ELSE. Emptying the layer and filling it again takes every control that is staying
        // off the plane and puts it back: it is detached, re-attached, and re-recorded from scratch - and what is drawn
        // does not survive the round trip. One node added, or the window resized so another comes into view, blanked
        // every node on the plane until something walked the whole scene again (a zoom, another node).
        var wanted = new List<IMeasurableComponent>(_items.Count);
        foreach (var item in _items)
        {
            if (item.Element is IMeasurableComponent measurable) wanted.Add(measurable);
        }

        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Children[i])) Children.RemoveAt(i);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var at = Children.IndexOf(wanted[i]);

            if (at == i) continue;

            // Only what actually moved: a control that is where it belongs is left alone, and one that is not has
            // genuinely changed places in the drawn order.
            if (at >= 0) Children.RemoveAt(at);

            Children.Insert(i, wanted[i]);
        }

        ApplyDesignMode();
        InvalidateMeasure();
    }

    /// <summary>Puts each hosted control in or out of the pointer's reach, by what it IS.
    /// <para>A button, a field, a box: editing, it is invisible to the pointer so that a press lands on the plane and
    /// the tool picks it up - a press cannot both operate one and drag it.</para>
    /// <para>A NODE is the exception. What is in one is a field, a switch, a list, and a node whose contents cannot be
    /// clicked is a picture of a node - so it stays live whatever the mode, and is dragged by its title strip. That
    /// press is the only one this layer forwards, in <see cref="OnPressed"/>.</para></summary>
    internal void ApplyDesignMode()
    {
        var editing = Owner is { IsDesignMode: true };

        foreach (var child in Children)
        {
            if (child is IUIComponent visual) visual.IsHitTestVisible = child is CanvasNode || !editing;
        }

    }

    // WHICH OF THE TWO a press on a node means, decided here because this is the last place that can decide it: the
    // press bubbles on to the canvas, and a canvas that also heard it would pick the node up while the field inside it
    // was being typed in.
    //
    // A bubbling handler and not one per node: a press lands on whatever is actually under the pointer - a strip, a
    // socket, a presenter, a button three levels inside the content - and only the node can say which of those is a
    // handle.
    //
    // The RIGHT button is left alone on purpose: it puts the tool back, and that means the same thing over a node as
    // anywhere else on the plane.
    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || Owner == null || e.ChangedButton != MouseButtons.Left) return;
        if (Node(e.OriginalSource) is not { } node) return;

        // The strip and the node's own chrome MEAN the plane: handed to the canvas, and from there on it is an ordinary
        // press - the tool picks, the gesture begins, one undo step opens.
        if (node.IsHandle(e.OriginalSource)) Owner.PressFromElement(e);

        // Either way the canvas must not hear it a second time. What was under the pointer has answered.
        e.Handled = true;
    }

    private static CanvasNode Node(object source)
    {
        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            if (at is CanvasNode node) return node;
        }

        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Core.Diagnostics.LayoutTrace.Count(typeof(CanvasElementLayer), "measure-pass");

        // Measured at its WORLD size, which does not change when the camera does - so a control is laid out once and the
        // zoom never re-measures what is inside it. That is not only cheaper, it is the only way the size is honest:
        // measured at the SCREEN size instead, a control zoomed out was handed a box smaller than its own text and its
        // floors and its content won, so it stopped shrinking and appeared to GROW as everything around it got smaller.
        for (var i = 0; i < Children.Count && i < _items.Count; i++)
        {
            var item = _items[i];
            var world = item.World;

            if (!item.SizeFollowsContent)
            {
                Children[i].Measure(new Size(world.Width, world.Height));
                continue;
            }

            // WITH NOTHING IMPOSED, because a measure is clamped to what it was offered: asking a control inside its own
            // box can only ever answer "it fits", which is what a node with four sockets in a two-socket box said while
            // drawing them over each other, and what a node narrower than its own labels said while spilling out of its
            // frame.
            Children[i].Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            var natural = Children[i].DesiredSize;

            // WHAT IT NEEDS, told to the item: the measure is the only place this is known, and a grip pulling the
            // frame has to stop there rather than squeezing the control down to nothing.
            item.Smallest = natural;

            var width = Math.Max(world.Width, natural.Width);
            var height = natural.Height;

            // Dragged WIDER than it needs - which is allowed, and often wanted. The height then has to be asked again
            // at that width, because a control given more room can use it and come out shorter.
            if (width - natural.Width > 0.5)
            {
                Children[i].Measure(new Size(width, Double.PositiveInfinity), force: true);
                height = Children[i].DesiredSize.Height;
            }

            if (height <= 0) continue;

            if (Math.Abs(width - world.Width) > 0.5 || Math.Abs(height - world.Height) > 0.5)
            {
                item.World = new Rect(world.X, world.Y, width, height);
            }
        }

        // Nothing of its own: the layer is a place, not a thing with a size. Asking for room would push the canvas's
        // own measure around, and the canvas is the one that decides how big the plane's window is.
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Owner == null) return finalSize;

        var scale = Owner.Scale;

        for (var i = 0; i < Children.Count && i < _items.Count; i++)
        {
            var world = _items[i].World;
            var at = Owner.WorldToScreen(new Vector2(world.X, world.Y));

            // Placed in SCREEN pixels but sized in WORLD units, and then scaled about its own top-left: the camera is a
            // transform on a finished control, never a new layout for it.
            Children[i].Arrange(new Rect(at.X, at.Y, world.Width, world.Height));

            if (Children[i] is not IUIComponent visual) continue;

            // A CONTROL CANNOT BE SMALLER THAN IT CAN BE, and the item has to say so - the floor is the control's own
            // (a theme's MinWidth, its content) and the camera changes none of it. Dragged out at 8x, a box 200 screen
            // pixels wide is 25 in the world, which a field refuses: it stood at 120 and drew eight times that, while
            // the frame and every hit stayed on the 25 nobody could see. Read after the arrange, because that is where
            // a floor shows - a measure answers within what it was offered.
            //
            // A control TOLD what size to be is left alone: that size is the application's word, not a floor, and the
            // item's box stays what it was given.
            if (!_items[i].SizeFollowsContent && Free(visual)
                && (visual.RenderSize.Width > world.Width + 0.5 || visual.RenderSize.Height > world.Height + 0.5))
            {
                _items[i].Smallest = visual.RenderSize;
                _items[i].World = new Rect(world.X, world.Y,
                    Math.Max(world.Width, visual.RenderSize.Width), Math.Max(world.Height, visual.RenderSize.Height));

                world = _items[i].World;
            }

            if (visual.RenderTransform is not { } transform)
            {
                transform = new Transform();
                visual.RenderTransform = transform;
            }

            visual.RenderTransformOrigin = Vector2.Zero;
            transform.ScaleX = scale;
            transform.ScaleY = scale;

            // ...AND THE ITEM'S OWN TURN, in the SAME transform. One control has one render transform: a second one
            // written anywhere else simply replaces this, and the control then stands unscaled inside a frame drawn at
            // the camera's scale. The turn is about the MIDDLE of the control's own box - stated as a centre rather
            // than as an origin, because the origin here is the top-left the zoom scales about.
            var turn = _items[i] is ICanvasTransformed turned ? turned.Transform : CanvasTransform.None;

            transform.RotationAngle = turn.Angle;
            transform.SkewX = turn.SkewX;
            transform.SkewY = turn.SkewY;
            transform.RotationCenterX = world.Width / 2;
            transform.RotationCenterY = world.Height / 2;
        }

        // The hosted controls have just been put somewhere, and what the canvas DRAWS depends on where they are - a
        // wire ends at a socket inside one of them. Nothing else would ask for that: a node folded by its own switch
        // never touches the canvas, so its wires kept the shape they had until something unrelated redrew the plane.
        Owner.Repaint();

        return finalSize;
    }

    // Whether the control's size is its OWN business - nothing was written into Width/Height from outside.
    private static bool Free(IUIComponent visual) =>
        visual is not IMeasurableComponent sized || (Double.IsNaN(sized.Width) && Double.IsNaN(sized.Height));


    private bool Same(List<ElementItem> items)
    {
        if (items.Count != _items.Count) return false;

        for (var i = 0; i < items.Count; i++)
        {
            if (!ReferenceEquals(items[i], _items[i])) return false;
        }

        return true;
    }
}
