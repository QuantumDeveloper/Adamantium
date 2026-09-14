using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>Where an <see cref="InfiniteCanvas"/>'s <see cref="CanvasPane"/>s live: a layer on the GLASS that puts
/// each one where its <see cref="CanvasPane.Placement"/> says, in screen pixels, and never moves with the camera.
/// <para>A layer rather than the canvas placing them itself, for the same reason the controls on the plane have one: a
/// panel has to be a real child of something to be laid out, drawn and given input at all.</para>
/// <para>It also answers what the panes take AWAY - see <see cref="Inset"/>. A docked pane is a wall, and the canvas
/// has to know where its usable middle actually is.</para></summary>
public class CanvasChromeLayer : Panel
{
    private readonly List<CanvasPane> _panes = new();

    public static readonly AdamantiumProperty PaneInsetProperty = AdamantiumProperty.Register(nameof(PaneInset),
        typeof(Thickness), typeof(CanvasChromeLayer),
        new PropertyMetadata(new Thickness(10), PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty PaneGapProperty = AdamantiumProperty.Register(nameof(PaneGap),
        typeof(Double), typeof(CanvasChromeLayer),
        new PropertyMetadata(8.0, PropertyMetadataOptions.AffectsArrange));

    /// <summary>How far a pane sits from the edge it is placed against.</summary>
    public Thickness PaneInset
    {
        get => GetValue<Thickness>(PaneInsetProperty);
        set => SetValue(PaneInsetProperty, value);
    }

    /// <summary>The space between two panes sharing one placement.</summary>
    public Double PaneGap
    {
        get => GetValue<Double>(PaneGapProperty);
        set => SetValue(PaneGapProperty, value);
    }

    /// <summary>The canvas whose viewport and selection this layer places against.</summary>
    public InfiniteCanvas Owner { get; set; }

    /// <summary>Puts the layer's children in step with the panes it should be showing. Cheap to call - it returns
    /// having done nothing when the set is unchanged.</summary>
    public void Sync(CanvasPanes panes)
    {
        if (Same(panes)) return;

        foreach (var pane in _panes)
        {
            pane.Layer = null;
            pane.Canvas = null;
        }

        _panes.Clear();
        Children.Clear();

        // FOLLOWERS FIRST, and that is a z-order rule rather than a list one: children later in the collection draw
        // over earlier ones, and a pane anchored to an edge is furniture while one that rides the selection is a
        // passing label. The label must not cover the furniture - a bar landing on the inspector hides the very rows
        // it was opened to change. Order among equals is kept, so the markup still decides everything else.
        if (panes != null)
        {
            foreach (var pane in panes)
            {
                if (pane.Placement == CanvasPanePlacement.Selection) Take(pane);
            }

            foreach (var pane in panes)
            {
                if (pane.Placement != CanvasPanePlacement.Selection) Take(pane);
            }
        }

        SyncSelection();
        InvalidateMeasure();
    }

    private void Take(CanvasPane pane)
    {
        pane.Layer = this;
        pane.Canvas = Owner;

        _panes.Add(pane);
        Children.Add(pane);
    }

    /// <summary>Show or hide the panes that FOLLOW the selection. Called when the selection changes, and deliberately
    /// not from the arrange pass: visibility is a layout input, and writing one while laying out is how a pass ends up
    /// invalidating itself.</summary>
    public void SyncSelection()
    {
        var anything = Owner is { } canvas && canvas.SelectionBounds is not null;

        foreach (var pane in _panes)
        {
            if (pane.Placement != CanvasPanePlacement.Selection) continue;

            var wanted = anything ? Visibility.Visible : Visibility.Collapsed;
            if (pane.Visibility != wanted) pane.Visibility = wanted;
        }
    }

    /// <summary>What the DOCKED panes take out of the viewport, as a margin. The canvas asks so that "fit to view" and
    /// "home" center on the part of the plane a person can actually see.</summary>
    public Thickness Inset()
    {
        double left = 0, top = 0, right = 0, bottom = 0;

        foreach (var pane in _panes)
        {
            var taken = pane.Reserved();

            // The LARGEST claim on a side, not the sum: two panes docked to the same edge sit one above the other, so
            // together they take the width of the wider, not of both.
            if (taken.Left > left) left = taken.Left;
            if (taken.Top > top) top = taken.Top;
            if (taken.Right > right) right = taken.Right;
            if (taken.Bottom > bottom) bottom = taken.Bottom;
        }

        return new Thickness(left, top, right, bottom);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = Double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;

        foreach (var child in Children) child.Measure(new Size(width, height));

        // Nothing of its own: the layer is a place, not a thing with a size. Asking for room would push the canvas's
        // own measure around, and the canvas is the one that decides how big the plane's window is.
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = PaneInset;
        var gap = PaneGap;

        // Each slot runs its own stack, so two panes placed against the same edge sit one after the other instead of
        // on top of each other.
        double topLeft = inset.Top, topRight = inset.Top;
        double bottomLeft = finalSize.Height - inset.Bottom, bottomRight = finalSize.Height - inset.Bottom;
        double topCenter = inset.Left, bottomCenter = inset.Left;
        double left = 0, right = 0;

        var leftTotal = Total(CanvasPanePlacement.Left, gap, true);
        var rightTotal = Total(CanvasPanePlacement.Right, gap, true);
        left = Math.Max(inset.Top, (finalSize.Height - leftTotal) / 2);
        right = Math.Max(inset.Top, (finalSize.Height - rightTotal) / 2);

        foreach (var pane in _panes)
        {
            if (pane.Visibility == Visibility.Collapsed) continue;

            var size = pane.DesiredSize;
            var x = 0.0;
            var y = 0.0;

            switch (pane.Placement)
            {
                case CanvasPanePlacement.TopLeft:
                    x = inset.Left;
                    y = topLeft;
                    topLeft += size.Height + gap;
                    break;

                case CanvasPanePlacement.Left:
                    x = inset.Left;
                    y = left;
                    left += size.Height + gap;
                    break;

                case CanvasPanePlacement.BottomLeft:
                    x = inset.Left;
                    y = bottomLeft - size.Height;
                    bottomLeft -= size.Height + gap;
                    break;

                case CanvasPanePlacement.TopRight:
                    x = finalSize.Width - inset.Right - size.Width;
                    y = topRight;
                    topRight += size.Height + gap;
                    break;

                case CanvasPanePlacement.Right:
                    x = finalSize.Width - inset.Right - size.Width;
                    y = right;
                    right += size.Height + gap;
                    break;

                case CanvasPanePlacement.BottomRight:
                    x = finalSize.Width - inset.Right - size.Width;
                    y = bottomRight - size.Height;
                    bottomRight -= size.Height + gap;
                    break;

                case CanvasPanePlacement.TopCenter:
                    x = Centered(CanvasPanePlacement.TopCenter, finalSize.Width, gap) + topCenter - inset.Left;
                    y = inset.Top;
                    topCenter += size.Width + gap;
                    break;

                case CanvasPanePlacement.BottomCenter:
                    x = Centered(CanvasPanePlacement.BottomCenter, finalSize.Width, gap) + bottomCenter - inset.Left;
                    y = finalSize.Height - inset.Bottom - size.Height;
                    bottomCenter += size.Width + gap;
                    break;

                case CanvasPanePlacement.Selection:
                    var over = AboveSelection(size, finalSize, inset);
                    x = over.X;
                    y = over.Y;
                    break;

                default:
                    x = pane.Offset.X;
                    y = pane.Offset.Y;
                    break;
            }

            // Never off the edge, whatever the arithmetic said: a pane that cannot be reached is a pane that is gone,
            // and there is no edge on the plane itself to find it by.
            x = Math.Clamp(x, 0, Math.Max(0, finalSize.Width - size.Width));
            y = Math.Clamp(y, 0, Math.Max(0, finalSize.Height - size.Height));

            pane.Arrange(new Rect(x, y, size.Width, size.Height));
        }

        return finalSize;
    }

    // Above the selection frame if it fits there, below it if it does not - which is what a context bar has to do, or
    // it covers the very thing it is about as soon as the selection is near the top of the viewport.
    private Vector2 AboveSelection(Size size, Size room, Thickness inset)
    {
        if (Owner is not { } canvas || canvas.SelectionBounds is not { } world)
        {
            return new Vector2(inset.Left, inset.Top);
        }

        var at = canvas.WorldToScreen(new Vector2(world.X, world.Y));
        var far = canvas.WorldToScreen(new Vector2(world.Right, world.Bottom));

        var x = (at.X + far.X) / 2 - size.Width / 2;
        var y = at.Y - size.Height - PaneGap;

        if (y < inset.Top) y = far.Y + PaneGap;

        return new Vector2(x, y);
    }

    private double Total(CanvasPanePlacement placement, double gap, bool vertical)
    {
        var total = 0.0;
        var count = 0;

        foreach (var pane in _panes)
        {
            if (pane.Placement != placement || pane.Visibility == Visibility.Collapsed) continue;

            total += vertical ? pane.DesiredSize.Height : pane.DesiredSize.Width;
            count++;
        }

        return count > 1 ? total + gap * (count - 1) : total;
    }

    private double Centered(CanvasPanePlacement placement, double room, double gap) =>
        Math.Max(0, (room - Total(placement, gap, false)) / 2);

    private bool Same(CanvasPanes panes)
    {
        var count = panes?.Count ?? 0;
        if (count != _panes.Count) return false;

        for (var i = 0; i < count; i++)
        {
            if (!ReferenceEquals(panes[i], _panes[i])) return false;
        }

        return true;
    }
}
