using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of a <see cref="RibbonQuickAccess"/>: lays out as many commands as fit and hands the rest to
/// the bar to offer under its chevron.</summary>
public class RibbonQuickAccessPanel : Panel
{
    private readonly List<IUIComponent> _overflow = [];
    private readonly List<IUIComponent> _published = [];
    private readonly Dictionary<IMeasurableComponent, double> _naturalWidth = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        _overflow.Clear();
        Forget();

        var owner = Owner;
        var budget = Budget(availableSize.Width, owner);

        foreach (var child in Children)
        {
            var childWidth = NaturalWidth(child, availableSize.Height);
            if (child.Visibility == Visibility.Visible)
            {
                height = Math.Max(height, child.DesiredSize.Height);
            }

            if (!double.IsPositiveInfinity(budget) && width + childWidth > budget)
            {
                _overflow.Add(child);
                child.Visibility = Visibility.Collapsed;
                continue;
            }

            child.Visibility = Visibility.Visible;
            width += childWidth;
        }

        // Publishing is a write, and a write from inside layout asks for another pass - only a changed set is worth it.
        if (!Same(_overflow, _published))
        {
            _published.Clear();
            _published.AddRange(_overflow);
            owner?.SetOverflow(_published);
        }

        return new Size(width, height);
    }

    /// <summary>What the command asks for when it is shown. A collapsed one measures to zero, so the width it had while
    /// it was still in the row is remembered: what decides who fits must not be produced BY that decision, or the last
    /// command flickers in and out for ever.</summary>
    private double NaturalWidth(IMeasurableComponent child, double height)
    {
        if (child.Visibility == Visibility.Collapsed && _naturalWidth.TryGetValue(child, out var remembered))
        {
            return remembered;
        }

        child.Measure(new Size(double.PositiveInfinity, height));
        var width = child.DesiredSize.Width;
        _naturalWidth[child] = width;
        return width;
    }

    private void Forget()
    {
        if (_naturalWidth.Count <= Children.Count) return;

        foreach (var gone in _naturalWidth.Keys.Where(x => !Children.Contains(x)).ToList())
        {
            _naturalWidth.Remove(gone);
        }
    }

    /// <summary>How much width the buttons may take: the caption hands the bar an Auto column (infinity), so the limit
    /// has to come from its own MaxWidth. The chevron's width is reserved ALWAYS - a budget that depended on whether the
    /// chevron is currently shown would depend on the answer it produces, and flip-flop every frame.</summary>
    private double Budget(double available, RibbonQuickAccess owner)
    {
        if (owner == null) return available;

        if (double.IsPositiveInfinity(available) && owner.MaxWidth > 0 && !double.IsPositiveInfinity(owner.MaxWidth))
        {
            available = owner.MaxWidth;
        }

        if (double.IsPositiveInfinity(available)) return available;

        return Math.Max(0, available - owner.OverflowButtonWidth);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            var width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return new Size(x, finalSize.Height);
    }

    private static bool Same(List<IUIComponent> a, List<IUIComponent> b)
    {
        if (a.Count != b.Count) return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i])) return false;
        }

        return true;
    }

    private RibbonQuickAccess Owner => this.GetLogicalAncestors().OfType<RibbonQuickAccess>().FirstOrDefault();
}
