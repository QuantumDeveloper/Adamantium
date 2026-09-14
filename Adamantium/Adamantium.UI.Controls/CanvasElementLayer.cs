using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

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

    /// <summary>Puts the layer's children in step with the items it should be showing. Cheap to call - it returns having
    /// done nothing when the set is unchanged, which is what every camera move finds.</summary>
    public void Sync(List<ElementItem> items)
    {
        if (Same(items)) return;

        _items.Clear();
        _items.AddRange(items);

        Children.Clear();
        foreach (var item in _items)
        {
            if (item.Element is IMeasurableComponent measurable) Children.Add(measurable);
        }

        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Measured at its WORLD size, which does not change when the camera does - so a control is laid out once and the
        // zoom never re-measures what is inside it. That is not only cheaper, it is the only way the size is honest:
        // measured at the SCREEN size instead, a control zoomed out was handed a box smaller than its own text and its
        // floors and its content won, so it stopped shrinking and appeared to GROW as everything around it got smaller.
        for (var i = 0; i < Children.Count && i < _items.Count; i++)
        {
            var world = _items[i].World;
            Children[i].Measure(new Size(world.Width, world.Height));
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

            if (visual.RenderTransform is not { } transform)
            {
                transform = new Transform();
                visual.RenderTransform = transform;
            }

            visual.RenderTransformOrigin = Vector2.Zero;
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }

        return finalSize;
    }

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
