using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Shows a hover tooltip for any element that has a <see cref="ToolTipProperty"/> set (a string, or any content). One
/// tooltip is visible at a time: on mouse-enter a shared timer starts, and after <see cref="InitialShowDelayProperty"/>
/// (if the pointer is still over the element) a shared <see cref="Popup"/> opens a small themed card next to it; the
/// pointer leaving the element hides it. Set it via the attached property (<c>ToolTipService.ToolTip="…"</c>) or the
/// WPF-style shorthand <see cref="Base.InputUIComponent.ToolTip"/> (<c>&lt;Button ToolTip="…"/&gt;</c>) - both drive
/// this same service on any element that receives pointer input.
/// </summary>
public static class ToolTipService
{
    public static readonly AdamantiumProperty ToolTipProperty = AdamantiumProperty.RegisterAttached(
        "ToolTip", typeof(object), typeof(AdamantiumComponent), new PropertyMetadata(null, OnToolTipChanged));

    /// <summary>Hover delay before the tooltip appears, in milliseconds (default 500).</summary>
    public static readonly AdamantiumProperty InitialShowDelayProperty = AdamantiumProperty.RegisterAttached(
        "InitialShowDelay", typeof(double), typeof(AdamantiumComponent), new PropertyMetadata(500.0));

    /// <summary>Where the card sits relative to the element (default <see cref="PlacementMode.Bottom"/>).</summary>
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.RegisterAttached(
        "Placement", typeof(PlacementMode), typeof(AdamantiumComponent), new PropertyMetadata(PlacementMode.Bottom));

    public static object GetToolTip(AdamantiumComponent element) => element.GetValue(ToolTipProperty);
    public static void SetToolTip(AdamantiumComponent element, object value) => element.SetValue(ToolTipProperty, value);

    public static double GetInitialShowDelay(AdamantiumComponent element) => element.GetValue<double>(InitialShowDelayProperty);
    public static void SetInitialShowDelay(AdamantiumComponent element, double value) => element.SetValue(InitialShowDelayProperty, value);

    public static PlacementMode GetPlacement(AdamantiumComponent element) => element.GetValue<PlacementMode>(PlacementProperty);
    public static void SetPlacement(AdamantiumComponent element, PlacementMode value) => element.SetValue(PlacementProperty, value);

    // Shared, one tooltip at a time (like WPF's PopupControlService). Built lazily on the first show so nothing is
    // created for an app that never uses a tooltip, and so the controls are created on the UI thread.
    private static Popup _popup;
    private static ToolTip _toolTip;
    private static DispatcherTimer _timer;
    private static IInputComponent _pending;   // element awaiting show, or the one currently shown

    private static void OnToolTipChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not IInputComponent input) return;

        // Idempotent: (re)wire hover only while a tooltip is set. Unsubscribe first so re-setting doesn't double-hook.
        input.MouseEnter -= OnMouseEnter;
        input.MouseLeave -= OnMouseLeave;
        if (e.NewValue != null)
        {
            input.MouseEnter += OnMouseEnter;
            input.MouseLeave += OnMouseLeave;
        }
        else if (ReferenceEquals(_pending, input))
        {
            Hide();   // tooltip cleared off the element it was showing for
        }
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not IInputComponent input || GetToolTip((AdamantiumComponent)input) == null) return;
        _pending = input;
        EnsureBuilt();
        _timer.Stop();
        _timer.Start(TimeSpan.FromMilliseconds(GetInitialShowDelay((AdamantiumComponent)input)));
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, _pending)) Hide();
    }

    private static void OnTimerTick(object sender, EventArgs e)
    {
        _timer.Stop();   // one-shot: fire once after the delay
        if (_pending is not { IsMouseOver: true }) return;   // pointer left during the delay

        var owner = _pending;
        // The themed ToolTip control renders both a string (via its template's ContentPresenter) and a UI element as-is.
        _toolTip.Content = GetToolTip((AdamantiumComponent)owner);

        _popup.PlacementTarget = owner as UIComponent;
        _popup.Placement = GetPlacement((AdamantiumComponent)owner);
        _popup.DataContext = (owner as UIComponent)?.DataContext;   // so bindings in a content tooltip resolve
        _popup.IsOpen = true;
    }

    private static void Hide()
    {
        _timer?.Stop();
        if (_popup != null) _popup.IsOpen = false;
        _pending = null;
    }

    private static void EnsureBuilt()
    {
        if (_popup != null) return;

        // The card's entire look is themed (the ToolTip style); the service only fills its Content and positions it.
        _toolTip = new ToolTip();
        _popup = new Popup { Child = _toolTip, VerticalOffset = 4 };

        _timer = new DispatcherTimer();
        _timer.Tick += OnTimerTick;
    }
}
