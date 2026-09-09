using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// The tab strip's items panel. The owning <see cref="TabControl"/> decides both things it does: IsVirtualizing says
/// whether only the visible headers are built, and TabWidth/TabHeight give the slot. Virtualizing implies a uniform
/// slot - slot n starts at n x slot, so an unbuilt tab still has an exact position - and a default stands in when none
/// was set. Otherwise every tab is realized and stacked at its own measured size.
/// <para>There is deliberately no third mode. Guessing where an unrealized tab of unknown width sits gives an estimate
/// that moves as its neighbours are realized - slots slide under the pointer and a drag lands beside its target.</para>
/// <para>Positions are ABSOLUTE: the host applies the scroll offset by translating this panel and clipping (see
/// <see cref="TabStripScroller"/>), so panning is one matrix write.</para>
/// </summary>
public class TabPanel : VirtualizingPanel
{
    private const int Buffer = 2;             // realized either side of the viewport
    private const double DefaultViewport = 600;   // stands in while the strip is measured unbounded
    private const int UnknownOwnerWindow = 64;    // ceiling while the TabControl is not yet reachable

    // Stand-ins for an unset TabWidth/TabHeight once virtualization is asked for, so it is never asked for in vain.
    private const double DefaultTabWidth = 180;
    private const double DefaultTabHeight = 32;

    // Content-sized mode only; there every tab is realized, so both hold measured values. A uniform strip needs neither.
    private readonly List<double> _extents = new();
    private readonly List<double> _starts = new();
    private double _lastViewport;
    private double _lastCross;

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(TabPanel),
        new PropertyMetadata(Orientation.Horizontal,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    private bool IsHorizontal => Orientation == Orientation.Horizontal;

    // The switch is the CONTROL's, because nothing outside can address this panel instance. Once it is on a slot always
    // exists (a default stands in for an unset TabWidth), so there is no "asked to virtualize but cannot".
    private bool Virtualizes => _tabs != null && OwnerTabSize.virtualizing;

    // NOT chained to the base, which asks its own IsVirtualizing: with no slot the panel's size does follow its children.
    public override bool IsMeasureBoundary => IsItemsHost && Virtualizes;

    // The slot ALONG the strip, NaN when tabs size to their content. Width and height keep their names; the orientation
    // only decides which of them runs along. Taking TabWidth for both made a side strip a column of 180px-tall tabs.
    private double UniformExtent
    {
        get
        {
            var explicitSize = OwnerTabWidth;
            if (Usable(explicitSize)) return explicitSize;
            return Virtualizes ? (IsHorizontal ? DefaultTabWidth : DefaultTabHeight) : Double.NaN;
        }
    }

    // ...and the size ACROSS it, which is the strip's thickness. Unset, the widest tab decides it.
    private double UniformCross => Pick(IsHorizontal ? OwnerTabSize.height : OwnerTabSize.width);

    private static double Pick(double value) => Usable(value) ? value : Double.NaN;

    private static bool Usable(double value) => value > 0 && !Double.IsNaN(value) && !Double.IsInfinity(value);

    private double OwnerTabWidth => IsHorizontal ? OwnerTabSize.width : OwnerTabSize.height;

    // Up the visual tree, because the headers can hang off a nested TabItemsControl. Found once and KEPT: a placement
    // change swaps the template branch and the new panel is measured before it is parented, so the walk finds nothing -
    // and reading that as "no slot" realized all 1032 tabs, once per orientation.
    private TabControl _tabs;

    private (double width, double height, bool virtualizing) OwnerTabSize
    {
        get
        {
            if (_tabs == null)
                for (IUIComponent node = this; node != null; node = node.VisualParent)
                    if (node is TabControl found) { _tabs = found; break; }

            return _tabs != null
                ? (_tabs.TabWidth, _tabs.TabHeight, _tabs.IsVirtualizing)
                : (Double.NaN, Double.NaN, false);
        }
    }

    private bool OwnerUnknown => OwnerTabSize.width is var _ && _tabs == null;

    /// <summary>The arrows walk the strip ALONG the way it runs - Left/Right on a top strip, Up/Down on a side one -
    /// and answer nothing across it, where there is no other tab to go to. Tab keeps walking the headers itself; these
    /// are the moves that belong to the strip's shape.</summary>
    public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
    {
        if (!IsArrow(direction))
            return base.Navigate(from, direction);

        if (IsVertical(direction) != (Orientation == Orientation.Vertical))
            return null;

        if (!IsItemsHost) return Neighbour(from, IsForward(direction));

        // By ITEM INDEX: the base walks realized children, in realization order, and the neighbour an arrow at the edge
        // wants is exactly the one not realized - so the arrows stopped at the window's edge, mid-strip.
        var index = Owner.ItemContainerGenerator.IndexFromContainer(from);
        if (index < 0) return Neighbour(from, IsForward(direction));

        var next = index + (IsForward(direction) ? 1 : -1);
        return next >= 0 && next < Owner.Items.Count ? RealizeInWindow(next) : null;
    }

    /// <summary>Whether a LONE tab fills the strip - the owning control's <see cref="TabControl.StretchSingleTab"/>. Read
    /// from the owner rather than set on this panel: the panel is built from an ItemsPanelTemplate, so the theme cannot
    /// address this instance, and writing it from the control's code would outrank the theme and never give it back.</summary>
    private bool StretchesTheOnlyTab(int count) =>
        count == 1 && (VisualParent as ItemsPresenter)?.Owner is TabControl { StretchSingleTab: true };

    protected override Size MeasurePlain(Size availableSize)
    {
        var horizontal = IsHorizontal;
        // Unbounded main axis so each tab takes exactly its content extent - unless a uniform slot is set, which every
        // tab is then measured AT, so the header trims to it instead of overflowing it.
        var uniform = UniformExtent;
        var uniformCross = UniformCross;
        var mainConstraint = double.IsNaN(uniform) ? double.PositiveInfinity : uniform;
        var crossConstraint = double.IsNaN(uniformCross)
            ? (horizontal ? availableSize.Height : availableSize.Width)
            : uniformCross;
        var childConstraint = horizontal
            ? new Size(mainConstraint, crossConstraint)
            : new Size(crossConstraint, mainConstraint);

        double main = 0, cross = 0;
        foreach (var child in Children)
        {
            child.Measure(childConstraint);
            var size = child.DesiredSize;
            var step = double.IsNaN(uniform) ? (horizontal ? size.Width : size.Height) : uniform;
            main += step;
            cross = Math.Max(cross, horizontal ? size.Height : size.Width);
        }

        if (!double.IsNaN(uniformCross)) cross = uniformCross;
        return horizontal ? new Size(main, cross) : new Size(cross, main);
    }

    protected override Size ArrangePlain(Size finalSize)
    {
        var horizontal = IsHorizontal;
        // Cross = the content extent clamped to the slot (honest bounds - a strip only occupies what it stacks).
        var cross = horizontal
            ? Math.Min(finalSize.Height, DesiredSize.Height)
            : Math.Min(finalSize.Width, DesiredSize.Width);

        // ONE tab told to fill takes the whole strip. Only in ARRANGE: the strip is measured with an unbounded axis (see
        // TabStripScroller) so there is no length to fill until the slot is handed down.
        if (StretchesTheOnlyTab(Children.Count))
        {
            var only = Children[0];
            only.Arrange(horizontal ? new Rect(0, 0, finalSize.Width, cross) : new Rect(0, 0, cross, finalSize.Height));
            return horizontal ? new Size(finalSize.Width, cross) : new Size(cross, finalSize.Height);
        }

        var uniform = UniformExtent;
        double main = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var step = double.IsNaN(uniform) ? (horizontal ? size.Width : size.Height) : uniform;
            child.Arrange(horizontal ? new Rect(main, 0, step, cross) : new Rect(0, main, cross, step));
            main += step;
        }

        return horizontal ? new Size(main, cross) : new Size(cross, main);
    }

    protected override Size MeasureVirtualized(Size availableSize, Vector2 offset)
    {
        var horizontal = IsHorizontal;
        var count = Owner.Items.Count;
        if (count == 0)
        {
            foreach (var c in Owner.ItemContainerGenerator.SetWindow(0, -1)) ParkContainer(c);
            _extents.Clear();
            _starts.Clear();
            return new Size();
        }

        var uniformCross = UniformCross;
        var crossAvailable = double.IsNaN(uniformCross)
            ? (horizontal ? availableSize.Height : availableSize.Width)
            : uniformCross;
        // A uniform strip measures its tabs AT the slot, not unbounded: that is what gives the header a width to trim its
        // title against instead of reporting the length it would like to be and overflowing the slot it will get.
        var uniform = UniformExtent;
        var mainConstraint = double.IsNaN(uniform) ? double.PositiveInfinity : uniform;
        var childConstraint = horizontal
            ? new Size(mainConstraint, crossAvailable)
            : new Size(crossAvailable, mainConstraint);

        var mainViewport = horizontal ? availableSize.Width : availableSize.Height;
        double effectiveViewport;
        if (double.IsInfinity(mainViewport))
        {
            OnNoViewport();
            effectiveViewport = _lastViewport > 0 ? _lastViewport : DefaultViewport;
        }
        else
        {
            effectiveViewport = mainViewport;
            _lastViewport = mainViewport;
        }

        SyncExtents(count);
        RebuildStarts(count);

        var mainOffset = horizontal ? offset.X : offset.Y;
        int first, last;
        if (Virtualizes)
        {
            // Arithmetic on the slot: no container is consulted and none has to exist.
            first = Math.Max(0, (int)Math.Floor(mainOffset / uniform) - Buffer);
            last = Math.Min(count - 1, (int)Math.Ceiling((mainOffset + effectiveViewport) / uniform) + Buffer);
        }
        else
        {
            first = 0;
            last = count - 1;

            // "I cannot see the control yet" is not "there is no slot", and answering it by building a thousand tabs is
            // the freeze this guards. Build a screenful and ask again with the tree finished.
            if (OwnerUnknown && count > UnknownOwnerWindow)
            {
                last = UnknownOwnerWindow - 1;
                InvalidateMeasure();
            }
        }

        foreach (var c in Owner.ItemContainerGenerator.SetWindow(first, last)) ParkContainer(c);

        double crossMax = 0;
        for (var i = first; i <= last; i++)
        {
            var container = (IMeasurableComponent)RealizeInWindow(i);
            container.Measure(childConstraint);
            var size = container.DesiredSize;
            var main = double.IsNaN(uniform) ? (horizontal ? size.Width : size.Height) : uniform;
            if (main > 0) _extents[i] = main;   // a recycled container mid-rebind can answer zero; keep the last good one
            crossMax = Math.Max(crossMax, horizontal ? size.Height : size.Width);
        }

        RebuildStarts(count);   // after measuring: the arrange in this same pass places tabs at these starts

        if (crossMax > 0) _lastCross = crossMax;
        var cross = !double.IsNaN(uniformCross) ? uniformCross
            : crossMax > 0 ? crossMax : _lastCross;
        var extent = TotalMain(count);
        return horizontal ? new Size(extent, cross) : new Size(cross, extent);
    }

    protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
    {
        var horizontal = IsHorizontal;
        var count = Owner.Items.Count;
        if (count == 0) return;

        var cross = horizontal ? finalSize.Height : finalSize.Width;

        if (StretchesTheOnlyTab(count)
            && Owner.ItemContainerGenerator.ContainerFromIndex(0) is IMeasurableComponent only)
        {
            only.Arrange(horizontal ? new Rect(0, 0, finalSize.Width, cross) : new Rect(0, 0, cross, finalSize.Height));
            return;
        }

        // ABSOLUTE slots (no -offset): the host applies the offset once by translating this panel and clipping, so a tab
        // that kept its index keeps its rect and Arrange short-circuits for it.
        foreach (var i in Owner.ItemContainerGenerator.RealizedIndices)
        {
            if (i < 0 || i >= count) continue;
            if (Owner.ItemContainerGenerator.ContainerFromIndex(i) is not IMeasurableComponent container) continue;
            var start = StartAt(i);
            var main = ExtentAt(i);
            container.Arrange(horizontal ? new Rect(start, 0, main, cross) : new Rect(0, start, cross, main));
        }
    }

    /// <summary>Where a tab sits along the strip, realized or not - what a "scroll this tab into view" needs, since the
    /// tab it is aiming at is usually the one that was virtualized away.</summary>
    public override bool TryGetItemRect(int index, out Rect rect)
    {
        rect = default;
        if (!IsItemsHost || index < 0 || index >= Owner.Items.Count) return false;

        var start = StartAt(index);
        var main = ExtentAt(index);
        if (main <= 0) return false;   // content-sized and not measured yet: say nothing rather than guess
        var cross = _lastCross;
        rect = IsHorizontal ? new Rect(start, 0, main, cross) : new Rect(0, start, cross, main);
        return true;
    }

    // Either arithmetic on a uniform slot, or a measured value - never a guess. See Virtualizes.
    private double StartAt(int index)
    {
        var uniform = UniformExtent;
        if (!Double.IsNaN(uniform)) return index * uniform;
        return index >= 0 && index < _starts.Count ? _starts[index] : 0;
    }

    private double ExtentAt(int index)
    {
        var uniform = UniformExtent;
        if (!Double.IsNaN(uniform)) return uniform;
        return index >= 0 && index < _extents.Count ? _extents[index] : 0;
    }

    private double TotalMain(int count)
    {
        var uniform = UniformExtent;
        if (!Double.IsNaN(uniform)) return count * uniform;

        double sum = 0;
        for (var i = 0; i < count && i < _extents.Count; i++) sum += _extents[i];
        return sum;
    }

    private void SyncExtents(int count)
    {
        while (_extents.Count > count) _extents.RemoveAt(_extents.Count - 1);
        while (_extents.Count < count) _extents.Add(0);
    }

    private void RebuildStarts(int count)
    {
        _starts.Clear();
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            _starts.Add(sum);
            sum += i < _extents.Count ? _extents[i] : 0;
        }
    }
}
