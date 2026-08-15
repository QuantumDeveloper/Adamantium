using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class InputUIComponent : MeasurableUIComponent, IInputComponent
{
    #region Routed events
    
    private bool _isLoaded;

    public static readonly RoutedEvent LoadedEvent = EventManager.RegisterRoutedEvent( nameof(Loaded),
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));
    
    public static readonly RoutedEvent UnloadedEvent = EventManager.RegisterRoutedEvent("Unloaded",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent InitializedEvent = EventManager.RegisterRoutedEvent(nameof(Initialized),
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    // TextInputEvent / PreviewTextInputEvent moved to Core (Keyboard) so the Core KeyboardDevice can raise them; the CLR
    // event wrappers below now bind to Keyboard.TextInputEvent / Keyboard.PreviewTextInputEvent.

    public static readonly RoutedEvent MouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent( nameof(MouseLeftButtonDown),
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseLeftButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseLeftButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseLeftButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseRightButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseRightButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseRightButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseMiddleButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseMiddleButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseMiddleButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseLeftButtonUpEvent = 
        EventManager.RegisterRoutedEvent("MouseLeftButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseRightButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseRightButtonUpEvent = EventManager.RegisterRoutedEvent(
        "MouseRightButtonUp",
        RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseMiddleButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseMiddleButtonUpEvent = EventManager.RegisterRoutedEvent(
        "MouseMiddleButtonUp",
        RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));



    public static readonly RoutedEvent PreviewMouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseLeftButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseLeftButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseLeftButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseRightButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseRightButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseRightButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseMiddleButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseMiddleButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseMiddleButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    #endregion
    
    static InputUIComponent()
    {
        FocusManager.GotFocusEvent.RegisterClassHandler<IInputComponent>(new RoutedEventHandler(GotFocusHandler));
        FocusManager.LostFocusEvent.RegisterClassHandler<IInputComponent>(new RoutedEventHandler(LostFocusHandler));
        Keyboard.GotKeyboardFocusWithinEvent.RegisterClassHandler<IInputComponent>(
            new RoutedEventHandler(GotKeyboardFocusWithinHandler));
        Keyboard.LostKeyboardFocusWithinEvent.RegisterClassHandler<IInputComponent>(
            new RoutedEventHandler(LostKeyboardFocusWithinHandler));
        Keyboard.KeyDownEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(KeyDownHandler));
        Keyboard.KeyUpEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(KeyUpHandler));
        Keyboard.PreviewKeyDownEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(PreviewKeyDownHandler));
        Keyboard.PreviewKeyUpEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(PreviewKeyUpHandler));
        Mouse.MouseEnterEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseEnterHandler));
        Mouse.MouseLeaveEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseLeaveHandler));
        Mouse.PreviewMouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseDownHandler));
        Mouse.MouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseDownHandler));
        Mouse.PreviewMouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseUpHandler));
        Mouse.MouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseUpHandler));
        Mouse.RawMouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseDownHandler));
        Mouse.RawMouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseUpHandler));
        Mouse.MouseMoveEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseMoveHandler));
        Mouse.RawMouseMoveEvent.RegisterClassHandler<IInputComponent>(new RawMouseEventHandler(RawMouseMoveHandler));

        PreviewMouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonDownHandler));
        PreviewMouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonUpHandler));
        MouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseLeftButtonDownHandler));
        MouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseLeftButtonUpHandler));

        PreviewMouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonDownHandler));
        PreviewMouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonUpHandler));
        MouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseRightButtonDownHandler));
        MouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseRightButtonUpHandler));

        PreviewMouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonDownHandler));
        PreviewMouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonUpHandler));
        MouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseMiddleButtonDownHandler));
        MouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseMiddleButtonUpHandler));

        RawMouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseLeftButtonDownHandler));
        RawMouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseLeftButtonUpHandler));
        RawMouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseRightButtonDownHandler));
        RawMouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseRightButtonUpHandler));
        RawMouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonDownHandler));
        RawMouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonUpHandler));

        MouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(OpenContextMenuHandler));

        // Keyboard navigation is a static service, and a static class registers nothing until something touches it.
        // Here is the one place guaranteed to run before any element exists.
        KeyboardNavigation.Register();
        // ...and the one fact it cannot work out for itself: which window an element hosted on the OVERLAY belongs to.
        // Such an element has no visual path back, and only the popup layer knows the way - see Popup.HostOf.
        KeyboardNavigation.HostOf = element => Popup.HostOf(element);
    }

    // A right-click on an element with a ContextMenu (its own or an ancestor's) opens it at the cursor. The right-button-up
    // event is Direct (fires on the deepest target), so walk up to the first element that carries a ContextMenu.
    private static void OpenContextMenuHandler(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        for (var node = sender as IUIComponent; node != null; node = node.VisualParent)
            if (node is InputUIComponent { ContextMenu: { } menu } host)
            {
                menu.Open(host, e.GetPosition(host));
                e.Handled = true;
                return;
            }
    }

    public static readonly AdamantiumProperty IsFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsFocused),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));

    // A REGULAR (bindable) tooltip on every input element - like WPF's FrameworkElement.ToolTip (distinct from the
    // ToolTipService.ToolTip attached form, kept for advanced/non-input targets). Being a real registered property, it
    // resolves for binding (ToolTip="{Binding}"); its change is forwarded to the shared hover service so both forms drive
    // one code path.
    public static readonly AdamantiumProperty ToolTipProperty = AdamantiumProperty.Register(nameof(ToolTip),
        typeof(object), typeof(InputUIComponent), new PropertyMetadata(null, OnToolTipChanged));

    private static void OnToolTipChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is InputUIComponent c) ToolTipService.SetToolTip(c, e.NewValue);
    }

    /// <summary>A right-click flyout for this element (WPF's FrameworkElement.ContextMenu): set it and it opens at the
    /// cursor on right-button-up. Kept connected as a logical child so it is themed (its template + popup build) and
    /// inherits DataContext; it is zero-size inline (its rows live in the popup overlay), so it doesn't affect layout.</summary>
    public static readonly AdamantiumProperty ContextMenuProperty = AdamantiumProperty.Register(nameof(ContextMenu),
        typeof(Adamantium.UI.Controls.ContextMenu), typeof(InputUIComponent), new PropertyMetadata(null, OnContextMenuChanged));

    public Adamantium.UI.Controls.ContextMenu ContextMenu
    {
        get => GetValue<Adamantium.UI.Controls.ContextMenu>(ContextMenuProperty);
        set => SetValue(ContextMenuProperty, value);
    }

    private static void OnContextMenuChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not InputUIComponent host) return;
        if (e.OldValue is Adamantium.UI.Controls.ContextMenu old) host.RemoveLogicalChild(old);
        if (e.NewValue is Adamantium.UI.Controls.ContextMenu menu) host.AddLogicalChild(menu);
    }

    // Default FALSE (as in WPF's UIElement): most elements - panels, presenters, decorators, text, shapes, plain
    // containers - are NOT keyboard-focus targets. Genuinely interactive controls opt IN via OverrideMetadata(true)
    // in their own static ctor (ButtonBase, TextBoxBase, Slider, Selector items, ...). This keeps the focus walk from
    // a clicked template part landing on some passive container instead of the owning control, WITHOUT having to
    // remember to opt every new container OUT.
    public static readonly AdamantiumProperty FocusableProperty = AdamantiumProperty.Register(nameof(Focusable),
        typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));
    
    public static readonly AdamantiumProperty IsKeyboardFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsKeyboardFocused),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));
    
    public static readonly AdamantiumProperty IsMouseOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseOver),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));

    /// <summary>The focus is on this element OR on something inside it. What a composite control needs: a NumericUpDown
    /// is never itself focused - its editor is - so IsFocused is false on it while the user is very much in it.</summary>
    public static readonly AdamantiumProperty IsKeyboardFocusWithinProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsKeyboardFocusWithin),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));

    /// <summary>...and the focus got there BY KEYBOARD, which is when a focus ring is worth drawing. A ring that also
    /// appeared on every click would be noise: the click already said where you are.</summary>
    public static readonly AdamantiumProperty IsFocusVisibleProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsFocusVisible),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));

    /// <summary>What the focus ring on THIS control looks like: a <see cref="Style"/> applied to the
    /// <see cref="FocusAdorner"/> the keyboard puts on it - the same shape WPF's FocusVisualStyle has.
    /// <para>Null (the default) means the theme's own <c>FocusAdorner</c> style decides, which is where the ring is
    /// described for the whole application. Set this - from a style, like anything else - only where one control needs
    /// a different one: a tighter ring on a dense toolbar. It is applied AFTER the theme, so its setters win.</para>
    /// <para>NO ring is a style with no <c>Template</c> - for a control that shows the focus its own way (an editor
    /// that accents its own border), so the two never say the same thing twice. That is why there is no separate
    /// on/off switch: the ring a control shows IS this style, and a style that draws nothing shows nothing.</para></summary>
    public static readonly AdamantiumProperty FocusVisualStyleProperty = AdamantiumProperty.Register(
        nameof(FocusVisualStyle), typeof(Style), typeof(InputUIComponent), new PropertyMetadata(null));

    public Style FocusVisualStyle
    {
        get => GetValue<Style>(FocusVisualStyleProperty);
        set => SetValue(FocusVisualStyleProperty, value);
    }

    public static readonly AdamantiumProperty IsMouseDirectlyOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseDirectlyOver),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false));
    
    public static readonly AdamantiumProperty IsInitializedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsInitialized),
            typeof(Boolean), typeof(InputUIComponent), new PropertyMetadata(false, OnIsInitializedChanged));

    public bool IsInitialized
    {
        get => GetValue<bool>(IsInitializedProperty);
        private set => SetValue(IsInitializedProperty, value);
    }
    
    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsKeyboardFocusWithin
    {
        get => GetValue<bool>(IsKeyboardFocusWithinProperty);
        private set => SetValue(IsKeyboardFocusWithinProperty, value);
    }

    public bool IsFocusVisible
    {
        get => GetValue<bool>(IsFocusVisibleProperty);
        private set => SetValue(IsFocusVisibleProperty, value);
    }

    public bool IsMouseDirectlyOver
    {
        get => GetValue<bool>(IsMouseDirectlyOverProperty);
        private set => SetValue(IsMouseDirectlyOverProperty, value);
    }

    
    public bool IsKeyboardFocused
    {
        get => GetValue<bool>(IsKeyboardFocusedProperty);
        private set => SetValue(IsKeyboardFocusedProperty, value);
    }
    

    /// <summary>Content shown as a hover tooltip - a string, or any UI content. A regular, bindable property (WPF's
    /// FrameworkElement.ToolTip); its change is forwarded to the shared <see cref="ToolTipService"/> that shows the card.</summary>
    public object ToolTip
    {
        get => GetValue<object>(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public Boolean IsFocused
    {
        get => GetValue<Boolean>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }
    
    public Boolean Focusable
    {
        get => GetValue<Boolean>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }
    
    public event RoutedEventHandler Loaded
    {
        add => AddHandler(LoadedEvent, value);
        remove => RemoveHandler(LoadedEvent, value);
    }
    
    public event RoutedEventHandler Unloaded
    {
        add => AddHandler(UnloadedEvent, value);
        remove => RemoveHandler(UnloadedEvent, value);
    }
    
    public event RoutedEventHandler Initialized
    {
        add => AddHandler(InitializedEvent, value);
        remove => RemoveHandler(InitializedEvent, value);
    }

    public event RawMouseEventHandler RawMouseMove
    {
        add => AddHandler(Mouse.RawMouseMoveEvent, value);
        remove => RemoveHandler(Mouse.RawMouseMoveEvent, value);
    }

    public event MouseButtonEventHandler RawMouseDown
    {
        add => AddHandler(Mouse.RawMouseDownEvent, value);
        remove => RemoveHandler(Mouse.RawMouseDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseUp
    {
        add => AddHandler(Mouse.RawMouseUpEvent, value);
        remove => RemoveHandler(Mouse.RawMouseUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseLeftButtonDown
    {
        add => AddHandler(RawMouseLeftButtonDownEvent, value);
        remove => RemoveHandler(RawMouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseLeftButtonUp
    {
        add => AddHandler(RawMouseLeftButtonUpEvent, value);
        remove => RemoveHandler(RawMouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseRightButtonDown
    {
        add => AddHandler(RawMouseRightButtonDownEvent, value);
        remove => RemoveHandler(RawMouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseRightButtonUp
    {
        add => AddHandler(RawMouseRightButtonUpEvent, value);
        remove => RemoveHandler(RawMouseRightButtonUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseMiddleButtonDown
    {
        add => AddHandler(RawMouseMiddleButtonDownEvent, value);
        remove => RemoveHandler(RawMouseMiddleButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseMiddleButtonUp
    {
        add => AddHandler(RawMouseMiddleButtonUpEvent, value);
        remove => RemoveHandler(RawMouseMiddleButtonUpEvent, value);
    }

    

    public event MouseButtonEventHandler MouseDoubleClick
    {
        add => AddHandler(Mouse.MouseDoubleClickEvent, value);
        remove => RemoveHandler(Mouse.MouseDoubleClickEvent, value);
    }

    public event MouseButtonEventHandler MouseMiddleButtonDown
    {
        add => AddHandler(MouseMiddleButtonDownEvent, value);
        remove => RemoveHandler(MouseMiddleButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseMiddleButtonUp
    {
        add => AddHandler(MouseMiddleButtonUpEvent, value);
        remove => RemoveHandler(MouseMiddleButtonUpEvent, value);
    }

    public event RoutedEventHandler GotFocus
    {
        add => AddHandler(FocusManager.GotFocusEvent, value);
        remove => RemoveHandler(FocusManager.GotFocusEvent, value);
    }

    public event RoutedEventHandler LostFocus
    {
        add => AddHandler(FocusManager.LostFocusEvent, value);
        remove => RemoveHandler(FocusManager.LostFocusEvent, value);
    }

    public event KeyEventHandler KeyDown
    {
        add => AddHandler(Keyboard.KeyDownEvent, value);
        remove => RemoveHandler(Keyboard.KeyDownEvent, value);
    }

    public event KeyEventHandler KeyUp
    {
        add => AddHandler(Keyboard.KeyUpEvent, value);
        remove => RemoveHandler(Keyboard.KeyUpEvent, value);
    }

    public event KeyboardGotFocusEventHandler GotKeyboardFocus
    {
        add => AddHandler(Keyboard.GotKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.GotKeyboardFocusEvent, value);
    }

    public event KeyboardFocusChangedEventHandler LostKeyboardFocus
    {
        add => AddHandler(Keyboard.LostKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.LostKeyboardFocusEvent, value);
    }

    public event MouseEventHandler GotMouseCapture
    {
        add => AddHandler(Mouse.GotMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.GotMouseCaptureEvent, value);
    }

    public event MouseEventHandler LostMouseCapture
    {
        add => AddHandler(Mouse.LostMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.LostMouseCaptureEvent, value);
    }

    public event MouseEventHandler MouseEnter
    {
        add => AddHandler(Mouse.MouseEnterEvent, value);
        remove => RemoveHandler(Mouse.MouseEnterEvent, value);
    }

    public event MouseEventHandler MouseLeave
    {
        add => AddHandler(Mouse.MouseLeaveEvent, value);
        remove => RemoveHandler(Mouse.MouseLeaveEvent, value);
    }

    public event MouseEventHandler MouseMove
    {
        add => AddHandler(Mouse.MouseMoveEvent, value);
        remove => RemoveHandler(Mouse.MouseMoveEvent, value);
    }

    public event MouseWheelEventHandler MouseWheel
    {
        add => AddHandler(Mouse.MouseWheelEvent, value);
        remove => RemoveHandler(Mouse.MouseWheelEvent, value);
    }

    public event DragDropEventHandler DragEnter
    {
        add => AddHandler(DragDropEvents.DragEnterEvent, value);
        remove => RemoveHandler(DragDropEvents.DragEnterEvent, value);
    }

    public event DragDropEventHandler DragOver
    {
        add => AddHandler(DragDropEvents.DragOverEvent, value);
        remove => RemoveHandler(DragDropEvents.DragOverEvent, value);
    }

    public event DragDropEventHandler DragLeave
    {
        add => AddHandler(DragDropEvents.DragLeaveEvent, value);
        remove => RemoveHandler(DragDropEvents.DragLeaveEvent, value);
    }

    public event DragDropEventHandler Drop
    {
        add => AddHandler(DragDropEvents.DropEvent, value);
        remove => RemoveHandler(DragDropEvents.DropEvent, value);
    }

    public event DragDropEventHandler PreviewDragEnter
    {
        add => AddHandler(DragDropEvents.PreviewDragEnterEvent, value);
        remove => RemoveHandler(DragDropEvents.PreviewDragEnterEvent, value);
    }

    public event DragDropEventHandler PreviewDragOver
    {
        add => AddHandler(DragDropEvents.PreviewDragOverEvent, value);
        remove => RemoveHandler(DragDropEvents.PreviewDragOverEvent, value);
    }

    public event DragDropEventHandler PreviewDragLeave
    {
        add => AddHandler(DragDropEvents.PreviewDragLeaveEvent, value);
        remove => RemoveHandler(DragDropEvents.PreviewDragLeaveEvent, value);
    }

    public event DragDropEventHandler PreviewDrop
    {
        add => AddHandler(DragDropEvents.PreviewDropEvent, value);
        remove => RemoveHandler(DragDropEvents.PreviewDropEvent, value);
    }

    public event MouseButtonEventHandler MouseDown
    {
        add => AddHandler(Mouse.MouseDownEvent, value);
        remove => RemoveHandler(Mouse.MouseDownEvent, value);
    }

    public event MouseButtonEventHandler MouseUp
    {
        add => AddHandler(Mouse.MouseUpEvent, value);
        remove => RemoveHandler(Mouse.MouseUpEvent, value);
    }

    public event MouseButtonEventHandler MouseLeftButtonDown
    {
        add => AddHandler(MouseLeftButtonDownEvent, value);
        remove => RemoveHandler(MouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseLeftButtonUp
    {
        add => AddHandler(MouseLeftButtonUpEvent, value);
        remove => RemoveHandler(MouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler MouseRightButtonDown
    {
        add => AddHandler(MouseRightButtonDownEvent, value);
        remove => RemoveHandler(MouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseRightButtonUp
    {
        add => AddHandler(MouseRightButtonUpEvent, value);
        remove => RemoveHandler(MouseRightButtonUpEvent, value);
    }

    public event TextInputEventHandler TextInput
    {
        add => AddHandler(Keyboard.TextInputEvent, value);
        remove => RemoveHandler(Keyboard.TextInputEvent, value);
    }


    public event KeyEventHandler PreviewKeyDown
    {
        add => AddHandler(Keyboard.PreviewKeyDownEvent, value);
        remove => RemoveHandler(Keyboard.PreviewKeyDownEvent, value);
    }

    public event KeyEventHandler PreviewKeyUp
    {
        add => AddHandler(Keyboard.PreviewKeyUpEvent, value);
        remove => RemoveHandler(Keyboard.PreviewKeyUpEvent, value);
    }

    public event KeyboardGotFocusEventHandler PreviewGotKeyboardFocus
    {
        add => AddHandler(Keyboard.PreviewGotKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.PreviewGotKeyboardFocusEvent, value);
    }

    public event KeyboardFocusChangedEventHandler PreviewLostKeyboardFocus
    {
        add => AddHandler(Keyboard.PreviewLostKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.PreviewLostKeyboardFocusEvent, value);
    }

    public event MouseEventHandler PreviewGotMouseCapture
    {
        add => AddHandler(Mouse.PreviewGotMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.PreviewGotMouseCaptureEvent, value);
    }

    public event MouseEventHandler PreviewLostMouseCapture
    {
        add => AddHandler(Mouse.PreviewLostMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.PreviewLostMouseCaptureEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseDown
    {
        add => AddHandler(Mouse.PreviewMouseDownEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseUp
    {
        add => AddHandler(Mouse.PreviewMouseUpEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseUpEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseLeftButtonDown
    {
        add => AddHandler(PreviewMouseLeftButtonDownEvent, value);
        remove => RemoveHandler(PreviewMouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseLeftButtonUp
    {
        add => AddHandler(PreviewMouseLeftButtonUpEvent, value);
        remove => RemoveHandler(PreviewMouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseRightButtonDown
    {
        add => AddHandler(PreviewMouseRightButtonDownEvent, value);
        remove => RemoveHandler(PreviewMouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseRightButtonUp
    {
        add => AddHandler(PreviewMouseRightButtonUpEvent, value);
        remove => RemoveHandler(PreviewMouseRightButtonUpEvent, value);
    }

    public event MouseWheelEventHandler PreviewMouseWheel
    {
        add => AddHandler(Mouse.PreviewMouseWheelEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseWheelEvent, value);
    }

    public event MouseEventHandler PreviewMouseMove
    {
        add => AddHandler(Mouse.PreviewMouseMoveEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseMoveEvent, value);
    }

    public event TextInputEventHandler PreviewTextInput
    {
        add => AddHandler(Keyboard.PreviewTextInputEvent, value);
        remove => RemoveHandler(Keyboard.PreviewTextInputEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseDoubleClick
    {
        add => AddHandler(Mouse.PreviewMouseDoubleClickEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseDoubleClickEvent, value);
    }
    
    private static void GotFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnGotFocus(e);
    }

    private static void LostFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnLostFocus(e);
    }

    // The keyboard is subscribed ONCE, here, like the mouse and the focus - and handed to a virtual, so a control that
    // wants a key overrides OnKeyDown instead of registering a class handler of its own. It runs on every element the
    // key bubbles through, each with itself as sender, which is what lets a control claim a key its child ignored.
    private static void KeyDownHandler(object sender, KeyEventArgs e)
    {
        (sender as InputUIComponent)?.OnKeyDown(e);
    }

    private static void KeyUpHandler(object sender, KeyEventArgs e)
    {
        (sender as InputUIComponent)?.OnKeyUp(e);
    }

    private static void PreviewKeyDownHandler(object sender, KeyEventArgs e)
    {
        (sender as InputUIComponent)?.OnPreviewKeyDown(e);
    }

    private static void PreviewKeyUpHandler(object sender, KeyEventArgs e)
    {
        (sender as InputUIComponent)?.OnPreviewKeyUp(e);
    }

    /// <summary>A key travelling up through this element. Set <see cref="RoutedEventArgs.Handled"/> to claim it - that
    /// is the whole contract with navigation, which only ever gets the keys nobody claimed.</summary>
    protected virtual void OnKeyDown(KeyEventArgs e)
    {
    }

    protected virtual void OnKeyUp(KeyEventArgs e)
    {
    }

    /// <summary>The same key on the way DOWN, before anything inside this element sees it - what a composite control
    /// takes a key with when its own editor would otherwise claim it first (an arrow key stepping a numeric's value
    /// rather than the caret inside its text box).</summary>
    protected virtual void OnPreviewKeyDown(KeyEventArgs e)
    {
    }

    protected virtual void OnPreviewKeyUp(KeyEventArgs e)
    {
    }

    // Raised individually on each element that JOINED or LEFT the focused element's ancestor chain, so the state is
    // simply set - there is no chain to walk here, that already happened.
    private static void GotKeyboardFocusWithinHandler(object sender, RoutedEventArgs e)
    {
        if (sender is not InputUIComponent ui) return;
        ui.IsKeyboardFocusWithin = true;
        ui.IsFocusVisible = FocusManager.IsFocusVisible;
    }

    private static void LostKeyboardFocusWithinHandler(object sender, RoutedEventArgs e)
    {
        if (sender is not InputUIComponent ui) return;
        ui.IsKeyboardFocusWithin = false;
        ui.IsFocusVisible = false;
    }

    private static void MouseEnterHandler(object sender, MouseEventArgs e)
    {
        if (sender is InputUIComponent ui && !ui.IsMouseOver)
        {
            ui.OnMouseEnter(e);
        }
    }

    private static void MouseLeaveHandler(object sender, MouseEventArgs e)
    {
        if (sender is InputUIComponent ui && ui.IsMouseOver)
        {
            ui.OnMouseLeave(e);
        }
    }

    private static void PreviewMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = PreviewMouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = PreviewMouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = PreviewMouseMiddleButtonDownEvent;
            }
            input.RaiseEvent(args);
        }
    }

    private static void PreviewMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = PreviewMouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = PreviewMouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = PreviewMouseMiddleButtonUpEvent;
            }
            input.RaiseEvent(args);
        }
    }

    private static void MouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = MouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = MouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = MouseMiddleButtonDownEvent;
            }

            input.RaiseEvent(args);
        }
    }

    private static void MouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = MouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = MouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = MouseMiddleButtonUpEvent;
            }

            input.RaiseEvent(args);
        }
    }

    private static void RawMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = RawMouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = RawMouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = RawMouseMiddleButtonDownEvent;
            }

            if (e.ChangedButton != MouseButtons.None)
            {
                input.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp) { ClickCount = e.ClickCount, OriginalSource = e.OriginalSource };
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = RawMouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = RawMouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = RawMouseMiddleButtonUpEvent;
            }

            if (e.ChangedButton != MouseButtons.None)
            {
                input.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseMoveHandler(object sender, UnboundMouseEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMove(ui, e);
    }

    private static void MouseMoveHandler(object sender, MouseEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMove(ui, e);
    }

    private static void PreviewMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseLeftButtonDown(ui, e);
    }

    private static void PreviewMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseLeftButtonUp(ui, e);
    }

    private static void MouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseLeftButtonDown(ui, e);
    }

    private static void MouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseLeftButtonUp(ui, e);
    }

    private static void PreviewMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseRightButtonDown(ui, e);
    }

    private static void PreviewMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseRightButtonUp(ui, e);
    }

    private static void MouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseRightButtonDown(ui, e);
    }

    private static void MouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseRightButtonUp(ui, e);
    }

    private static void PreviewMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseMiddleButtonDown(ui, e);
    }

    private static void PreviewMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseMiddleButtonUp(ui, e);
    }

    private static void MouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMiddleButtonDown(ui, e);
    }

    private static void MouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMiddleButtonUp(ui, e);
    }

    private static void RawMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseLeftButtonDown(ui, e);
    }

    private static void RawMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseLeftButtonUp(ui, e);
    }

    private static void RawMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseRightButtonDown(ui, e);
    }

    private static void RawMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseRightButtonUp(ui, e);
    }

    private static void RawMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMiddleButtonDown(ui, e);
    }

    private static void RawMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMiddleButtonUp(ui, e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
         base.OnAttachedToVisualTree(e);

         // A one-way latch - nothing ever puts it back. Writing it on every later attach cost a full trip through the
         // property system per node to set the value that was already there.
         if (!IsInitialized) IsInitialized = true;
    }

    public IWindow GetWindow()
    {
        if (!IsInitialized) return null;

        // The shared walk: it bridges template boundaries by TemplatedParent, and it STOPS - the loop this replaces
        // dereferenced its way past the root and threw for anything not under a window yet.
        return this.GetSelfAndLogicalAncestors().OfType<IWindow>().FirstOrDefault();
    }
    
    private static void OnIsInitializedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            var ui = a as InputUIComponent;
            ui?.RaiseEvent(new RoutedEventArgs(InitializedEvent));
            ui?.OnInitialized();
        }
    }

    protected virtual void OnInitialized()
    {
        
    }

    protected virtual void OnRawMouseMove(object sender, UnboundMouseEventArgs e)
    {

    }

    protected virtual void OnMouseMove(object sender, MouseEventArgs e)
    {
    }

    protected virtual void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }



    protected virtual void OnRawMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }
    
    protected virtual void OnGotFocus(RoutedEventArgs e)
    {
        IsFocused = e.OriginalSource == this;
        if (IsFocused && FocusManager.IsFocusVisible)
        {
            AdornerHost()?.AdornerLayer.SetFocus(FocusVisualOwner());
            // Tabbing past the bottom of a scrolled list left the focus ring somewhere off screen: the focus moved, the
            // viewport did not. So the element the keyboard just landed on scrolls itself into sight, the minimum
            // needed, through every enclosing viewer. Gated on IsFocusVisible - the same flag the ring uses - because
            // it means "the keyboard put you here"; a CLICK needs no scrolling (you clicked what you could see), and
            // scrolling under a click would move the thing out from under the cursor mid-gesture.
            (FocusVisualOwner() as UIComponent)?.BringIntoView();
        }
    }

    /// <summary>The control the focus ring belongs to: a focused TEMPLATE PART marks the control it is part of, never
    /// itself. The keyboard is in a NumericUpDown - not in "the text box inside its frame" - and a ring around that
    /// editor draws a second box inside the control's own one, around a part the user does not think of as a control at
    /// all. Content authored in a view has no templated parent, so it rings itself (measured: a page's buttons, check
    /// boxes and drop-downs all report none, while the numeric's editor reports the numeric).</summary>
    private InputUIComponent FocusVisualOwner()
    {
        var owner = this;
        while (owner.TemplatedParent is InputUIComponent templated)
            owner = templated;

        return owner;
    }

    protected virtual void OnLostFocus(RoutedEventArgs e)
    {
        IsFocused = false;
        // The move announces Lost before Got, so clearing here and setting there leaves exactly one ring - and none at
        // all when the focus was taken by a click, which says where you are without any help.
        AdornerHost()?.AdornerLayer.SetFocus(null);
    }

    // The ring goes on the WINDOW's adorner layer, not into this control's template: a template is a thing that can be
    // forgotten, and a control whose template forgot it would silently have no focus visual at all. One implementation
    // there covers every control, and draws above the content - so a control that clips its own children still shows it.
    private IAdornerHost AdornerHost()
    {
        for (IUIComponent node = this; node != null; node = node.VisualParent)
        {
            if (node is IAdornerHost host)
                return host;
        }

        // Nothing above: this element is hosted on the OVERLAY - a menu row, a drop-down's list - whose content has no
        // visual path back to the window at all. The popup recorded which window it was hosted in, so ask it; without
        // this the keyboard could move about inside an open popup with nothing to show for it.
        return Popup.HostOf(this) as IAdornerHost;
    }

    /// <summary>Leaving the tree gives up the focus. A control taken off screen - the page a tab swap replaced, a row a
    /// list stopped realizing - cannot go on holding the keyboard: Tab would keep walking that dead tree, and every stop
    /// on the way is invisible. The detach walks the whole subtree, so a focused descendant is covered too.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        FocusManager.Release(this);
    }

    protected virtual void OnMouseEnter(MouseEventArgs e)
    {
        IsMouseOver = true;
        Mouse.Cursor = Cursor;
    }

    protected virtual void OnMouseLeave(MouseEventArgs e)
    {
        IsMouseOver = false;
    }
    
    public bool IsMouseCaptured => Mouse.Captured == this;

    public bool CaptureMouse()
    {
        return Mouse.Capture(this);
    }

    public bool CaptureStylus()
    {
        throw new NotImplementedException();
    }

    public bool Focus()
    {
        return FocusManager.Focus(this);
    }
    
    public void ReleaseMouseCapture()
    {
        if (IsMouseCaptured)
        {
            Mouse.Capture(null);
        }
    }

    public void ReleaseStylusCapture()
    {
        throw new NotImplementedException();
    }

    protected override void OnRenderCompleted()
    {
        base.OnRenderCompleted();
        if (!_isLoaded)
        {
            _isLoaded = true; 
            RaiseEvent( new RoutedEventArgs(LoadedEvent, this));
        }
    }
}