using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Commands;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// Base class for controls that respond to a click - a mouse press/release over the control, or the Space/Enter keys
/// while focused - and optionally invoke an <see cref="ICommand"/>. Carries the chrome (border/corner/padding) that
/// button templates bind to.
/// </summary>
public abstract class ButtonBase : ContentControl
{
    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(ButtonBase),
        new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(ButtonBase),
        new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(ButtonBase),
        new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(ButtonBase),
        new PropertyMetadata(default(Thickness),
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    // State brushes the default template's triggers project onto the chrome for hover/press/disabled. Exposing them as
    // properties is what lets ONE template serve every button variant: the Accent style just overrides these brushes
    // (via {ThemeResource}), no second template. Set by the theme; null means "no change in that state".
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(ButtonBase), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundPressedProperty = AdamantiumProperty.Register(
        nameof(BackgroundPressed), typeof(Brush), typeof(ButtonBase), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundDisabledProperty = AdamantiumProperty.Register(
        nameof(BackgroundDisabled), typeof(Brush), typeof(ButtonBase), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundPressedProperty = AdamantiumProperty.Register(
        nameof(ForegroundPressed), typeof(Brush), typeof(ButtonBase), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundDisabledProperty = AdamantiumProperty.Register(
        nameof(ForegroundDisabled), typeof(Brush), typeof(ButtonBase), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ClickModeProperty = AdamantiumProperty.Register(nameof(ClickMode),
        typeof(ClickMode), typeof(ButtonBase), new PropertyMetadata(ClickMode.Release));

    public static readonly AdamantiumProperty IsPressedProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsPressed),
        typeof(bool), typeof(ButtonBase), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CommandProperty = AdamantiumProperty.Register(nameof(Command),
        typeof(ICommand), typeof(ButtonBase), new PropertyMetadata(null, OnCommandChanged));

    public static readonly AdamantiumProperty CommandParameterProperty = AdamantiumProperty.Register(nameof(CommandParameter),
        typeof(object), typeof(ButtonBase), new PropertyMetadata(null, OnCommandParameterChanged));

    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(nameof(Click),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ButtonBase));

    static ButtonBase()
    {
        Keyboard.KeyDownEvent.RegisterClassHandler<ButtonBase>(new KeyEventHandler(KeyDownClassHandler));
        Keyboard.KeyUpEvent.RegisterClassHandler<ButtonBase>(new KeyEventHandler(KeyUpClassHandler));
        // A button IS a keyboard-focus target (space/enter activate it) - opt in, since the base default is now false.
        // Metadata priority (not a ctor set), so a {Binding}/Style/Trigger can still override it (e.g. a scrollbar's
        // repeat buttons set Focusable="False" in their template). Covers Button/RepeatButton/Toggle*/CheckBox/Radio.
        FocusableProperty.OverrideMetadata(typeof(ButtonBase), new PropertyMetadata(true));
    }

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public Brush BackgroundPressed
    {
        get => GetValue<Brush>(BackgroundPressedProperty);
        set => SetValue(BackgroundPressedProperty, value);
    }

    public Brush BackgroundDisabled
    {
        get => GetValue<Brush>(BackgroundDisabledProperty);
        set => SetValue(BackgroundDisabledProperty, value);
    }

    public Brush ForegroundPressed
    {
        get => GetValue<Brush>(ForegroundPressedProperty);
        set => SetValue(ForegroundPressedProperty, value);
    }

    public Brush ForegroundDisabled
    {
        get => GetValue<Brush>(ForegroundDisabledProperty);
        set => SetValue(ForegroundDisabledProperty, value);
    }

    public ClickMode ClickMode
    {
        get => GetValue<ClickMode>(ClickModeProperty);
        set => SetValue(ClickModeProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        private set => SetValue(IsPressedProperty, value);
    }

    public ICommand Command
    {
        get => GetValue<ICommand>(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue<object>(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    // Track the command's CanExecuteChanged so the button auto-enables/disables. The subscription is weak (the relay
    // holds the button weakly), so a command owned by a long-lived view-model doesn't keep a removed button alive.
    private WeakCanExecuteChangedRelay<ButtonBase> _commandRelay;

    private static void OnCommandChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ButtonBase button) button.OnCommandChanged(e.NewValue as ICommand);
    }

    private static void OnCommandParameterChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ButtonBase button) button.UpdateCanExecute();
    }

    private void OnCommandChanged(ICommand newCommand)
    {
        _commandRelay?.Detach();
        _commandRelay = newCommand != null
            ? new WeakCanExecuteChangedRelay<ButtonBase>(newCommand, this, static b => b.UpdateCanExecute())
            : null;
        UpdateCanExecute();
    }

    private void UpdateCanExecute()
    {
        var command = Command;
        IsEnabled = command == null || command.CanExecute(CommandParameter);
    }

    /// <summary>Programmatically clicks the button (e.g. a default/cancel button activated by Enter/Esc), if enabled.</summary>
    public void PerformClick()
    {
        if (IsEnabled) OnClick();
    }

    protected virtual void OnClick()
    {
        var parameter = CommandParameter;
        if (Command != null && Command.CanExecute(parameter))
        {
            Command.Execute(parameter);
        }

        RaiseEvent(new RoutedEventArgs(ClickEvent, this) { RoutedEvent = ClickEvent });
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;
        e.Handled = true;

        // Capture so move/up keep arriving even when the pointer leaves the button (press-drag-release semantics).
        Focus();
        CaptureMouse();
        IsPressed = true;

        if (ClickMode == ClickMode.Press)
        {
            OnClick();
        }
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        var wasPressed = IsPressed;
        IsPressed = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        // A click only counts if the press was never cancelled and the pointer is still over the button on release.
        if (wasPressed && IsEnabled && IsMouseOver && ClickMode == ClickMode.Release)
        {
            OnClick();
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        if (ClickMode == ClickMode.Hover)
        {
            if (IsEnabled) OnClick();
            return;
        }

        // Re-pressing when the pointer returns while the button is still held (it was disarmed on leave).
        if (IsMouseCaptured && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            IsPressed = true;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        // Dragging off visually disarms the button but keeps the capture, so returning re-presses it.
        if (IsMouseCaptured)
        {
            IsPressed = false;
        }
    }

    private static void KeyDownClassHandler(object sender, KeyEventArgs e)
    {
        if (sender is ButtonBase button) button.OnKeyDownInternal(e);
    }

    private static void KeyUpClassHandler(object sender, KeyEventArgs e)
    {
        if (sender is ButtonBase button) button.OnKeyUpInternal(e);
    }

    private void OnKeyDownInternal(KeyEventArgs e)
    {
        if (!IsEnabled || e.IsRepeated) return;

        switch (e.Key)
        {
            case Key.Space:
                IsPressed = true;
                if (ClickMode == ClickMode.Press) OnClick();
                e.Handled = true;
                break;
            case Key.Enter:
                OnClick();
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUpInternal(KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;

        var wasPressed = IsPressed;
        IsPressed = false;
        if (wasPressed && IsEnabled && ClickMode == ClickMode.Release)
        {
            OnClick();
        }
        e.Handled = true;
    }
}
