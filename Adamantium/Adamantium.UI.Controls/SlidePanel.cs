using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A panel that slides in from an edge of the WINDOW and overlays all content (the MahApps "Flyout" idea). It hosts its
/// chrome in the window's popup/overlay layer via an edge-docked <see cref="Popup"/>, so it is always on top wherever it
/// is declared and takes no layout space in place. <see cref="Placement"/> chooses the edge; along that edge it either
/// stretches to the window (drawer alignment = Stretch on the cross axis) or is content-sized and aligned. Toggle
/// <see cref="IsOpen"/> to animate it in/out; the built-in close button (opt out via <see cref="ShowCloseButton"/>)
/// closes it. Set <see cref="MeasurableUIComponent.Width"/> (Left/Right) or Height (Top/Bottom) for its thickness.
/// </summary>
public class SlidePanel : ContentControl
{
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(Dock), typeof(SlidePanel), new PropertyMetadata(Dock.Left));

    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(SlidePanel), new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(SlidePanel), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(nameof(ShowCloseButton),
        typeof(bool), typeof(SlidePanel), new PropertyMetadata(true));

    public static readonly AdamantiumProperty FullWindowProperty = AdamantiumProperty.Register(nameof(FullWindow),
        typeof(bool), typeof(SlidePanel), new PropertyMetadata(false));

    public static readonly AdamantiumProperty SlideDurationProperty = AdamantiumProperty.Register(nameof(SlideDuration),
        typeof(double), typeof(SlidePanel), new PropertyMetadata(0.25));

    static SlidePanel()
    {
        // A drawer stands off its content; the chrome property itself is Control's, only the default differs.
        PaddingProperty.OverrideMetadata(typeof(SlidePanel),
            new PropertyMetadata(new Thickness(16),
                PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));
    }

    /// <summary>Which window edge the panel docks to and slides from.</summary>
    public Dock Placement
    {
        get => GetValue<Dock>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Open (slid in) or closed (slid off behind the edge). Toggling animates the slide.</summary>
    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Title content shown at the top of the panel.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowCloseButton
    {
        get => GetValue<bool>(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>When true, the panel also fills the MAIN (slide) axis with the window - a full-window overlay. Default
    /// false: the main axis is content-sized (or the explicit Width/Height). The cross axis always fills the window.</summary>
    public bool FullWindow
    {
        get => GetValue<bool>(FullWindowProperty);
        set => SetValue(FullWindowProperty, value);
    }

    /// <summary>Slide animation duration in seconds.</summary>
    public double SlideDuration
    {
        get => GetValue<double>(SlideDurationProperty);
        set => SetValue(SlideDurationProperty, value);
    }

    private Popup _popup;
    private UIComponent _drawer;
    private Button _closeButton;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _drawer = (GetTemplateChild("PART_Drawer") ?? _popup?.Child) as UIComponent;

        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        _closeButton = GetTemplateChild("PART_CloseButton") as Button;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;

        if (IsOpen) Open();
    }

    // SetCurrentValue, NOT the CLR setter: closing from the x button must not write a Local value that would mask the
    // TwoWay IsOpen binding - otherwise the source (the toggle) couldn't reopen the panel afterwards (see ToggleButton).
    private void OnCloseClick(object sender, RoutedEventArgs e) => SetCurrentValue(IsOpenProperty, false);

    // Renders nothing where declared - its whole UI lives in the overlay via the Popup. Its template root (a zero-size
    // Popup) is arranged to fill the layout slot, and a UIComponent hit-tests its box by default (=> true), so without
    // this the invisible panel would SWALLOW every click over that slot (the content behind it stopped responding).
    public override bool HitTestCore(Vector2 localPoint) => false;

    private static void OnIsOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not SlidePanel p || p._popup == null) return;
        if ((bool)e.NewValue) p.Open(); else p.Close();
    }

    private UIComponent Drawer => _popup?.Child as UIComponent;

    private void Open()
    {
        _popup.IsOpen = true;                 // portal the drawer into the overlay layer (on top of everything)
        AnimateSlide(toOpen: true);
    }

    private void Close()
    {
        if (!_popup.IsOpen) return;
        AnimateSlide(toOpen: false);          // slide off, then leave the overlay when it settles
    }

    // Slide the drawer along the axis perpendicular to its edge, between fully off-screen (behind the edge) and 0.
    private void AnimateSlide(bool toOpen)
    {
        var drawer = Drawer;
        if (drawer == null) return;

        var horizontal = Placement is Dock.Left or Dock.Right;
        var axis = horizontal ? Transform.TranslateXProperty : Transform.TranslateYProperty;
        var distance = SlideDistance(drawer, horizontal);
        var sign = Placement is Dock.Left or Dock.Top ? -1.0 : 1.0;   // which way is "off-screen behind the edge"
        var offscreen = sign * distance;
        var duration = TimeSpan.FromSeconds(Math.Max(0, SlideDuration));

        var transform = EnsureTransform(drawer);
        if (toOpen)
            transform.BeginAnimation(axis, new DoubleAnimation { From = offscreen, To = 0, Duration = duration });
        else
            transform.BeginAnimation(axis, new DoubleAnimation { From = 0, To = offscreen, Duration = duration },
                () => { if (!IsOpen) _popup.IsOpen = false; });
    }

    // Slide distance = the panel's thickness on the main axis: the explicit Width/Height if set, else the drawer's
    // measured size, else a sensible default so a first open still animates.
    private double SlideDistance(UIComponent drawer, bool horizontal)
    {
        var explicitSize = horizontal ? Width : Height;
        if (!double.IsNaN(explicitSize) && explicitSize > 0) return explicitSize;
        var rendered = horizontal ? drawer.RenderSize.Width : drawer.RenderSize.Height;
        return rendered > 0 ? rendered : 300;
    }

    private static Transform EnsureTransform(UIComponent c)
    {
        if (c.RenderTransform is not Transform t) { t = new Transform(); c.RenderTransform = t; }
        return t;
    }
}
