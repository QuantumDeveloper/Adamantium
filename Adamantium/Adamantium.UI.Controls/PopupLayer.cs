using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// Holds the open <see cref="Popup"/>s of a window and lays their children out on the window-wide overlay (NOT part of
/// the content visual tree, so it draws on top). Each frame it re-evaluates every popup's position from its target's
/// CURRENT world position - so a popup follows a moving target - and arranges the child there, clamped inside the window.
/// The renderer consumes <see cref="Roots"/> (the laid-out children) as a flat list of subtrees to draw last.
/// </summary>
public class PopupLayer
{
    private readonly List<Popup> _popups = [];

    /// <summary>The laid-out child of every open popup (declaration order = back-to-front), for the overlay to render.</summary>
    public IReadOnlyList<IUIComponent> Roots => _popups.Select(p => p.Child).OfType<IUIComponent>().ToList();

    public bool HasPopups => _popups.Count > 0;

    public void Add(Popup popup)
    {
        if (_popups.Contains(popup)) return;
        _popups.Add(popup);
        // Its render units were disposed when it last closed, but its components are still geometry-VALID (closing doesn't
        // invalidate layout), so a clean reopen would record nothing and the cache would "reuse" the disposed units (the
        // fill + border vanish, only re-dirtied text rebuilds). Mark the whole subtree dirty so the next layout re-measures
        // it and the render cache re-records its units.
        if (popup.Child is { } child) InvalidateSubtree(child);
    }

    public void Remove(Popup popup) => _popups.Remove(popup);

    /// <summary>
    /// Re-evaluate every open popup's position from its target's CURRENT world position (so it follows a moving target)
    /// and lay its child out there, clamped inside the window. Called each frame, after the main layout, before render.
    /// </summary>
    public void UpdateLayout(Size windowSize)
    {
        if (!IsFinitePositive(windowSize.Width) || !IsFinitePositive(windowSize.Height)) return;   // window not sized yet

        foreach (var popup in _popups)
        {
            if (popup.Child is not MeasurableUIComponent child) continue;

            // Measure UNCONSTRAINED so the content reports its intrinsic (fit-to-content) size - measuring against the
            // window made a stretchy child fill the window, which then built a window-sized text render target and FAULTED
            // the GPU. Guard every value: a NaN/non-positive size (or position) must NOT reach Arrange + the renderer, or
            // it produces invalid geometry / an oversized RT and faults the device.
            // Re-measure ONLY when the subtree is dirty (just opened, or its content changed) - NOT every frame: it's a
            // DETACHED subtree (logical child only, no LayoutManager), so a content change flags IsMeasureValid=false on the
            // changed element but nothing drains it; we detect that flag instead, so a static tooltip costs ~nothing.
            // force:true because the dirty flag may sit on a DESCENDANT (the TextBlock) while the measured root (the Border)
            // stayed valid - its gate would otherwise SKIP and the badge would keep its stale size (text clipped). Arrange
            // below self-gates on the rect, so following a moving target stays cheap on its own.
            if (NeedsLayout(child))
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity), force: true);
            var size = child.DesiredSize;
            if (!IsFinitePositive(size.Width) || !IsFinitePositive(size.Height)) continue;

            // Never larger than the window (a degenerate huge content can't blow up the render target).
            size = new Size(Math.Min(size.Width, windowSize.Width), Math.Min(size.Height, windowSize.Height));
            var pos = ComputePosition(popup, size, windowSize);
            if (double.IsNaN(pos.X) || double.IsNaN(pos.Y)) continue;

            child.Arrange(new Rect(pos.X, pos.Y, size.Width, size.Height));
        }
    }

    // True if any element in the detached subtree needs re-measuring (its IsMeasureValid was cleared by a content change
    // or by the open invalidation). Cheap - flag reads only, early-out on the first dirty node.
    private static bool NeedsLayout(IUIComponent node)
    {
        if (node is IMeasurableComponent { IsMeasureValid: false }) return true;
        foreach (var child in node.VisualChildren)
            if (NeedsLayout(child)) return true;
        return false;
    }

    private static void InvalidateSubtree(IUIComponent node)
    {
        (node as IMeasurableComponent)?.InvalidateMeasure();
        foreach (var child in node.VisualChildren)
            InvalidateSubtree(child);
    }

    private static bool IsFinitePositive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

    private static Vector2 ComputePosition(Popup popup, Size size, Size windowSize)
    {
        double tx = 0, ty = 0, tw = 0, th = 0;
        if (popup.EffectiveTarget is { } target)
        {
            var t = target.WorldTransform.TranslationVector;   // target's origin in window space
            tx = t.X; ty = t.Y; tw = target.RenderSize.Width; th = target.RenderSize.Height;
        }

        // Top/Bottom center horizontally over the target; Left/Right center vertically - the natural tooltip anchor.
        var cx = tx + (tw - size.Width) / 2;
        var cy = ty + (th - size.Height) / 2;
        double x, y;
        switch (popup.Placement)
        {
            case PlacementMode.Top:      x = cx; y = ty - size.Height;             break;
            case PlacementMode.Left:     x = tx - size.Width;  y = cy;             break;
            case PlacementMode.Right:    x = tx + tw;          y = cy;             break;
            case PlacementMode.Center:   x = cx; y = cy;                           break;
            case PlacementMode.Relative: x = tx; y = ty;                           break;
            default:                     x = cx; y = ty + th;                      break;   // Bottom
        }
        x += popup.HorizontalOffset;
        y += popup.VerticalOffset;

        // Never leave the window.
        x = Math.Clamp(x, 0, Math.Max(0, windowSize.Width - size.Width));
        y = Math.Clamp(y, 0, Math.Max(0, windowSize.Height - size.Height));
        return new Vector2((float)x, (float)y);
    }
}
