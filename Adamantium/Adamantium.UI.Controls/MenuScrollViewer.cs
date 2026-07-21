using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// A <see cref="ScrollViewer"/> for MENUS: no scrollbar. Instead ▲/▼ RepeatButtons at the top/bottom of the card that show
/// ONLY when the content overflows (the flyout is capped to the window height) and scroll it on click - and keep scrolling
/// while held (mouse-captured, so it's stable, unlike a hover strip whose hit-test flickers as the content moves under it).
/// The wheel scrolls too (inherited). A short menu keeps the arrows hidden and behaves like a plain card.
/// </summary>
public class MenuScrollViewer : ScrollViewer
{
    private const double LineStep = 48;   // px scrolled per arrow click; the RepeatButton repeats it while held

    // Read-only: whether there is content scrolled off the top / bottom - drives each arrow's visibility (theme trigger).
    public static readonly AdamantiumProperty CanScrollUpProperty = AdamantiumProperty.Register(nameof(CanScrollUp),
        typeof(bool), typeof(MenuScrollViewer), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanScrollDownProperty = AdamantiumProperty.Register(nameof(CanScrollDown),
        typeof(bool), typeof(MenuScrollViewer), new PropertyMetadata(false));

    public MenuScrollViewer()
    {
        ScrollChanged += (_, _) => UpdateArrows();
    }

    public bool CanScrollUp { get => GetValue<bool>(CanScrollUpProperty); private set => SetValue(CanScrollUpProperty, value); }

    public bool CanScrollDown { get => GetValue<bool>(CanScrollDownProperty); private set => SetValue(CanScrollDownProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        WireArrow("PART_ScrollUp", -1);
        WireArrow("PART_ScrollDown", 1);
        UpdateArrows();
    }

    private void WireArrow(string part, int direction)
    {
        if (GetTemplateChild(part) is RepeatButton button)
            button.Click += (_, _) => ScrollBy(direction * LineStep);
    }

    private void ScrollBy(double delta)
    {
        var maxY = Math.Max(0, ExtentSize.Height - ViewportSize.Height);
        var y = Math.Clamp(ScrollOffset.Y + delta, 0, maxY);
        SetScrollOffset(new Vector2(ScrollOffset.X, (float)y));
    }

    private void UpdateArrows()
    {
        var maxY = Math.Max(0, ExtentSize.Height - ViewportSize.Height);
        CanScrollUp = ScrollOffset.Y > 0.5;
        CanScrollDown = ScrollOffset.Y < maxY - 0.5;
    }
}
