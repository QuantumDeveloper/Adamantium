﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Data;
using Adamantium.UI.Input;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Windows.Input;

namespace Adamantium.UI;

public class FundamentalUIComponent : AdamantiumComponent, IFundamentalUIComponent
{
    private IFundamentalUIComponent parent;
    private TrackingCollection<IFundamentalUIComponent> logicalChildren;
    
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(MeasurableComponent), new PropertyMetadata(String.Empty));
    
    public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
        typeof(object), typeof(MeasurableComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));
    
    private static void DataContextChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumObject as MeasurableComponent;
        o?.DataContextChanged?.Invoke(o, e);
    }
    
    
    public Style Style { get; }
    public object DataContext { get; set; }
    public IFundamentalUIComponent LogicalParent => parent;
    
    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase)
    {
        return null;
    }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;
   
    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren => LogicalChildrenCollection.AsReadOnly();
    
    protected TrackingCollection<IFundamentalUIComponent> LogicalChildrenCollection
    {
        get
        {
            if (logicalChildren == null)
            {
                var list = new TrackingCollection<IFundamentalUIComponent>();
                LogicalChildrenCollection = list;
            }
            return logicalChildren;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (logicalChildren != value && logicalChildren != null)
            {
                logicalChildren.CollectionChanged -= LogicalChildrenCollectionChanged;
            }

            logicalChildren = value;
            logicalChildren.CollectionChanged += LogicalChildrenCollectionChanged;
        }

    }
    
    public String Name
    {
        get => GetValue<String>(NameProperty);
        set => SetValue(NameProperty, value);
    }
    
    private void LogicalChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Remove:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Replace:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Reset:
                throw new NotSupportedException("Reset should not be signalled on LogicalChildren collection");
        }
    }

    private void SetLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            element.SetParent(this);
        }
    }

    private void ClearLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            if (element.LogicalParent == this)
            {
                element.SetParent(null);
            }
        }
    }
    
    /// <summary>
    /// Sets the control's logical parent.
    /// </summary>
    /// <param name="logicalParent">The parent.</param>
    private void SetParent(IFundamentalUIComponent logicalParent)
    {
        var old = LogicalParent;

        if (logicalParent != old)
        {
            if (old != null && logicalParent != null)
            {
                throw new InvalidOperationException("The Control already has a parent.Parent Element is: " + LogicalParent);
            }

            // TODO: define do we actually need InheritanceParent proprety
            //InheritanceParent = parent;
            parent = logicalParent;

            /*
            var root = FindStyleRoot(old);

            if (root != null)
            {
               var e = new LogicalTreeAttachmentEventArgs(root);
               OnDetachedFromLogicalTree(e);
            }

            root = FindStyleRoot(this);

            if (root != null)
            {
               var e = new LogicalTreeAttachmentEventArgs(root);
               OnAttachedToLogicalTree(e);
            }

            RaisePropertyChanged(ParentProperty, old, _parent, BindingPriority.LocalValue);
            */
        }
    }


    protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    { }

    protected virtual void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    { }

    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;

}

public class UIComponent : FundamentalUIComponent, IInputComponent
{
    private Size renderSize;
    
    protected bool sizeChanged;
    protected Size previousRenderSize;

    #region Adamantium properties
    
    
    
    public static readonly AdamantiumProperty RenderTransformProperty =
        AdamantiumProperty.Register(nameof(RenderTransform), typeof(Transform), typeof(UIComponent));
    
    public static readonly AdamantiumProperty LayoutTransformProperty =
        AdamantiumProperty.Register(nameof(LayoutTransform), typeof(Transform), typeof(UIComponent));

    public static readonly AdamantiumProperty StyleProperty =
        AdamantiumProperty.Register(nameof(Style), typeof(Style), typeof(UIComponent),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, StyleChangedCallback));

    private static void StyleChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is IUIComponent component && e.NewValue != null)
        {
            component.Style.Attach(component);
        }
    }

    public static readonly AdamantiumProperty LocationProperty = AdamantiumProperty.Register(nameof(Location),
        typeof (Vector2), typeof (UIComponent), new PropertyMetadata(Vector2.Zero));

    public static readonly AdamantiumProperty VisibilityProperty = AdamantiumProperty.Register(nameof(Visibility),
        typeof(Visibility), typeof(UIComponent),
        new PropertyMetadata(Visibility.Visible,
            PropertyMetadataOptions.BindsTwoWayByDefault | 
            PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender));
      
    public static readonly AdamantiumProperty IsFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsFocused),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsHitTestVisibleProperty =
        AdamantiumProperty.Register(nameof(IsHitTestVisible),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ClipToBoundsProperty = AdamantiumProperty.Register(nameof(ClipToBounds),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty IsEnabledProperty = AdamantiumProperty.Register(nameof(IsEnabled),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
        typeof(Double), typeof(UIComponent),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.Register(nameof(AllowDrop),
        typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    public static readonly AdamantiumProperty FocusableProperty = AdamantiumProperty.Register(nameof(Focusable),
        typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    public static readonly AdamantiumProperty IsMouseOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseOver),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsMouseDirectlyOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseDirectlyOver),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsKeyboardFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsKeyboardFocused),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty UidProperty = AdamantiumProperty.Register(nameof(Uid),
        typeof(String), typeof(UIComponent), new PropertyMetadata(String.Empty));

    public static readonly AdamantiumProperty CursorProperty = AdamantiumProperty.Register(nameof(Cursor),
        typeof(Cursor), typeof(UIComponent), new PropertyMetadata(Cursor.Default));

    #endregion


    #region Routed events

    public static readonly RoutedEvent LoadedEvent = EventManager.RegisterRoutedEvent("Loaded",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));
    
    public static readonly RoutedEvent UnloadedEvent = EventManager.RegisterRoutedEvent("Unloaded",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent InitializedEvent = EventManager.RegisterRoutedEvent("Initialized",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent TextInputEvent = EventManager.RegisterRoutedEvent("TextInput",
        RoutingStrategy.Bubble, typeof(TextInputEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewTextInputEvent = 
        EventManager.RegisterRoutedEvent("PreviewTextInput",
            RoutingStrategy.Tunnel, typeof(TextInputEventHandler), typeof(UIComponent));
        
    public static readonly RoutedEvent SizeChangedEvent = 
        EventManager.RegisterRoutedEvent("SizeChangedEvent",
            RoutingStrategy.Bubble, typeof(TextInputEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseLeftButtonDown",
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


    #region Events
    
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

    public event SizeChangedEventHandler SizeChanged
    {
        add => AddHandler(MeasurableComponent.SizeChangedEvent, value);
        remove => RemoveHandler(MeasurableComponent.SizeChangedEvent, value);
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
        remove => AddHandler(Mouse.MouseMoveEvent, value);
    }

    public event MouseWheelEventHandler MouseWheel
    {
        add => AddHandler(Mouse.MouseWheelEvent, value);
        remove => AddHandler(Mouse.MouseWheelEvent, value);
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
        add => AddHandler(TextInputEvent, value);
        remove => RemoveHandler(TextInputEvent, value);
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
        add => AddHandler(PreviewTextInputEvent, value);
        remove => RemoveHandler(PreviewTextInputEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseDoubleClick
    {
        add => AddHandler(Mouse.PreviewMouseDoubleClickEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseDoubleClickEvent, value);
    }
    
    public event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;

    #endregion


    #region Properties
    
    public Vector2 Location
    {
        get => GetValue<Vector2>(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    public Visibility Visibility
    {
        get => GetValue<Visibility>(VisibilityProperty);
        set => SetValue(VisibilityProperty, value);
    }

    public Cursor Cursor
    {
        get => GetValue<Cursor>(CursorProperty);
        set => SetValue(CursorProperty, value);
    }

    public Boolean ClipToBounds
    {
        get => GetValue<Boolean>(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public Double Opacity
    {
        get => GetValue<Double>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public bool IsEnabled
    {
        get => GetValue<Boolean>(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public Boolean AllowDrop
    {
        get => GetValue<Boolean>(AllowDropProperty);
        set => SetValue(AllowDropProperty, value);
    }

    public Boolean Focusable
    {
        get => GetValue<Boolean>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
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

    public String Uid
    {
        get => GetValue<String>(UidProperty);
        private set => SetValue(UidProperty, value);
    }

    public Boolean IsFocused
    {
        get => GetValue<Boolean>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }

    public Boolean IsHitTestVisible
    {
        get => GetValue<Boolean>(IsHitTestVisibleProperty);
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    #endregion


    private readonly Dictionary<RoutedEvent, List<EventSubscription>> eventHandlers =
        new Dictionary<RoutedEvent, List<EventSubscription>>();

    public UIComponent()
    {
        VisualChildrenCollection = new TrackingCollection<IUIComponent>();
        VisualChildrenCollection.CollectionChanged += VisualChildrenCollectionChanged;
    }

    static UIComponent()
    {
        FocusManager.GotFocusEvent.RegisterClassHandler<UIComponent>(new RoutedEventHandler(GotFocusHandler));
        FocusManager.LostFocusEvent.RegisterClassHandler<UIComponent>(new RoutedEventHandler(LostFocusHandler));
        Mouse.MouseEnterEvent.RegisterClassHandler<UIComponent>(new MouseEventHandler(MouseEnterHandler));
        Mouse.MouseLeaveEvent.RegisterClassHandler<UIComponent>(new MouseEventHandler(MouseLeaveHandler));
        Mouse.PreviewMouseDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseDownHandler));
        Mouse.MouseDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseDownHandler));
        Mouse.PreviewMouseUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseUpHandler));
        Mouse.MouseUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseUpHandler));
        Mouse.RawMouseDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseDownHandler));
        Mouse.RawMouseUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseUpHandler));
        Mouse.MouseMoveEvent.RegisterClassHandler<UIComponent>(new MouseEventHandler(MouseMoveHandler));
        Mouse.RawMouseMoveEvent.RegisterClassHandler<UIComponent>(new RawMouseEventHandler(RawMouseMoveHandler));

        PreviewMouseLeftButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonDownHandler));
        PreviewMouseLeftButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonUpHandler));
        MouseLeftButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseLeftButtonDownHandler));
        MouseLeftButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseLeftButtonUpHandler));

        PreviewMouseRightButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonDownHandler));
        PreviewMouseRightButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonUpHandler));
        MouseRightButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseRightButtonDownHandler));
        MouseRightButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseRightButtonUpHandler));

        PreviewMouseMiddleButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonDownHandler));
        PreviewMouseMiddleButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonUpHandler));
        MouseMiddleButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseMiddleButtonDownHandler));
        MouseMiddleButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(MouseMiddleButtonUpHandler));

        RawMouseLeftButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseLeftButtonDownHandler));
        RawMouseLeftButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseLeftButtonUpHandler));
        RawMouseRightButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseRightButtonDownHandler));
        RawMouseRightButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseRightButtonUpHandler));
        RawMouseMiddleButtonDownEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonDownHandler));
        RawMouseMiddleButtonUpEvent.RegisterClassHandler<UIComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonUpHandler));


        SizeChangedEvent.RegisterClassHandler<UIComponent>(new SizeChangedEventHandler(SizeChangedHandler));
    }

    private static void SizeChangedHandler(object sender, SizeChangedEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnSizeChanged(e);
    }

    private static void GotFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnGotFocus(e);
    }

    private static void LostFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnLostFocus(e);
    }

    private static void MouseEnterHandler(object sender, MouseEventArgs e)
    {
        if (sender is UIComponent ui && !ui.IsMouseOver)
        {
            ui.OnMouseEnter(e);
        }
    }

    private static void MouseLeaveHandler(object sender, MouseEventArgs e)
    {
        if (sender is UIComponent ui && ui.IsMouseOver)
        {
            ui.OnMouseLeave(e);
        }
    }

    private static void PreviewMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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
            ui.RaiseEvent(args);
        }
    }

    private static void PreviewMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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
            ui.RaiseEvent(args);
        }
    }

    private static void MouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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

            ui.RaiseEvent(args);
        }
    }

    private static void MouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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

            ui.RaiseEvent(args);
        }
    }

    private static void RawMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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
                ui.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIComponent ui)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
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
                ui.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseMoveHandler(object sender, UnboundMouseEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseMove(ui, e);
    }

    private static void MouseMoveHandler(object sender, MouseEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseMove(ui, e);
    }

    private static void PreviewMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseLeftButtonDown(ui, e);
    }

    private static void PreviewMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseLeftButtonUp(ui, e);
    }

    private static void MouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseLeftButtonDown(ui, e);
    }

    private static void MouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseLeftButtonUp(ui, e);
    }

    private static void PreviewMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseRightButtonDown(ui, e);
    }

    private static void PreviewMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseRightButtonUp(ui, e);
    }

    private static void MouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseRightButtonDown(ui, e);
    }

    private static void MouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseRightButtonUp(ui, e);
    }

    private static void PreviewMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseMiddleButtonDown(ui, e);
    }

    private static void PreviewMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnPreviewMouseMiddleButtonUp(ui, e);
    }

    private static void MouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseMiddleButtonDown(ui, e);
    }

    private static void MouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnMouseMiddleButtonUp(ui, e);
    }

    private static void RawMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseLeftButtonDown(ui, e);
    }

    private static void RawMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseLeftButtonUp(ui, e);
    }

    private static void RawMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseRightButtonDown(ui, e);
    }

    private static void RawMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseRightButtonUp(ui, e);
    }

    private static void RawMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseMiddleButtonDown(ui, e);
    }

    private static void RawMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as UIComponent;
        ui?.OnRawMouseMiddleButtonUp(ui, e);
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

    protected virtual void OnSizeChanged(SizeChangedEventArgs e)
    {
            
    }

    protected virtual void OnGotFocus(RoutedEventArgs e)
    {
        IsFocused = e.OriginalSource == this;
    }

    protected virtual void OnLostFocus(RoutedEventArgs e)
    {
        IsFocused = false;
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

    public bool IsGeometryValid { get; protected set; }

    public Size RenderSize
    {
        get => Visibility == Visibility.Collapsed ? Size.Zero : renderSize;
        set
        {
            if (renderSize != value)
            {
                previousRenderSize = renderSize;
                sizeChanged = true;
            }
            renderSize = value;
        }
    }

    public void InvalidateRender()
    {
        IsGeometryValid = false;
    }

    /// <summary>
    /// Tests whether a control's size can be changed by a layout pass.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>True if the control's size can change; otherwise false.</returns>
    private static bool IsResizable(MeasurableComponent control)
    {
        return Double.IsNaN(control.Width) || Double.IsNaN(control.Height);
    }

    public void Render(DrawingContext context)
    {
        if (!IsGeometryValid)
        {
            OnRender(context);
            IsGeometryValid = true;
        }
    }

    /// <summary>
    /// Tests whether any of a <see cref="Rect"/>'s properties include negative values, a NaN or Infinity.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <returns>True if the rect is invalid; otherwise false.</returns>
    protected static bool IsInvalidRect(Rect rect)
    {
        return rect.Width < 0 || rect.Height < 0 ||
               Double.IsInfinity(rect.X) || Double.IsInfinity(rect.Y) ||
               Double.IsInfinity(rect.Width) || Double.IsInfinity(rect.Height) ||
               Double.IsNaN(rect.X) || Double.IsNaN(rect.Y) ||
               Double.IsNaN(rect.Width) || Double.IsNaN(rect.Height);
    }

    /// <summary>
    /// Tests whether any of a <see cref="Size"/>'s properties include negative values, a NaN or Infinity.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>True if the size is invalid; otherwise false.</returns>
    protected static bool IsInvalidSize(Size size)
    {
        return size.Width < 0 || size.Height < 0 ||
               Double.IsInfinity(size.Width) || Double.IsInfinity(size.Height) ||
               Double.IsNaN(size.Width) || Double.IsNaN(size.Height);
    }

    /// <summary>
    /// Ensures neither component of a <see cref="Size"/> is negative.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The non-negative size.</returns>
    protected static Size NonNegative(Size size)
    {
        return new Size(Math.Max(size.Width, 0), Math.Max(size.Height, 0));
    }

    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo = false)
    {
        if (routedEvent == null)
        {
            throw new ArgumentNullException(nameof(routedEvent));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (eventHandlers)
        {
            List<EventSubscription> subscriptions = null;
            if (!eventHandlers.ContainsKey(routedEvent))
            {
                subscriptions = new List<EventSubscription>();
                eventHandlers.Add(routedEvent, subscriptions);
            }
            else
            {
                subscriptions = eventHandlers[routedEvent];
            }
            var sub = new EventSubscription
            {
                Handler = handler,
                HandledEeventToo = handledEventsToo,
            };
            subscriptions.Add(sub);
        }

    }

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

    public void RaiseEvent(RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(e.RoutedEvent);

        e.Source ??= this;
        e.OriginalSource ??= this;

        if (e.RoutedEvent != null)
        {
            if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Direct)
            {
                RaiseDirectEvent(e);
            }
            else if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Bubble)
            {
                RaiseBubbleEvent(e);
            }

            else if (e.RoutedEvent.RoutingStrategy == RoutingStrategy.Tunnel)
            {
                RaiseTunnelEvent(e);
            }
        }
    }

    private void RaiseDirectEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        e.RoutedEvent.InvokeClassHandlers(this, e);

        lock (eventHandlers)
        {
            if (eventHandlers.ContainsKey(e.RoutedEvent))
            {
                var handlersList = eventHandlers[e.RoutedEvent];
                foreach (var handler in handlersList)
                {
                    if (!e.Handled || handler.HandledEeventToo)
                    {
                        handler.Handler.DynamicInvoke(this, e);
                    }
                }
            }
        }
    }

    private void RaiseBubbleEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        foreach (var uiComponent in GetBubbleEventRoute())
        {
            var element = (UIComponent)uiComponent;
            e.Source = element;
            element.RaiseDirectEvent(e);
        }
    }

    private void RaiseTunnelEvent(RoutedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        foreach (var uiComponent in GetTunnelEventRoute())
        {
            var element = (UIComponent)uiComponent;
            e.Source = element;
            element.RaiseDirectEvent(e);
        }
    }

    public IEnumerable<IUIComponent> GetBubbleEventRoute()
    {
        var element = this;
        while (element != null)
        {
            yield return element;
            element = (UIComponent)element.LogicalParent;
        }
    }

    public IEnumerable<IUIComponent> GetTunnelEventRoute()
    {
        return GetBubbleEventRoute().Reverse();
    }

    public void RemoveHandler(RoutedEvent routedEvent, Delegate handler)
    {
        if (routedEvent == null)
        {
            throw new ArgumentNullException(nameof(routedEvent));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (eventHandlers)
        {
            if (eventHandlers.ContainsKey(routedEvent))
            {
                var list = eventHandlers[routedEvent];
                list.RemoveAll(x => x.Handler == handler);
            }
        }
    }

    public void ReleaseMouseCapture()
    {
        throw new NotImplementedException();
    }

    public void ReleaseStylusCapture()
    {
        throw new NotImplementedException();
    }

    private void VisualChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (UIComponent visual in e.NewItems)
                {
                    visual.SetVisualParent(this);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (UIComponent visual in e.OldItems)
                {
                    visual.SetVisualParent(null);
                }
                break;
        }
    }

    public Rect Bounds { get; set; }

    public Rect ClipRectangle { get; internal set; }

    public Vector2 ClipPosition { get; set; }

    public IUIComponent VisualParent { get; private set; }

    public Int32 ZIndex { get; set; }

    public Transform RenderTransform
    {
        get => GetValue<Transform>(RenderTransformProperty);
        set => SetValue(RenderTransformProperty, value);
    }
    
    public Transform LayoutTransform
    {
        get => GetValue<Transform>(LayoutTransformProperty);
        set => SetValue(LayoutTransformProperty, value);
    }
    
    public IReadOnlyCollection<IUIComponent> GetVisualDescendants()
    {
        return VisualChildren;
    }

    public IReadOnlyCollection<IUIComponent> VisualChildren => VisualChildrenCollection.AsReadOnly();

    protected TrackingCollection<IUIComponent> VisualChildrenCollection { get; private set; }

    protected void AddVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Add(child);
    }
    
    protected void RemoveVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Remove(child);
    }

    protected void RemoveVisualChildren()
    {
        VisualChildrenCollection.Clear();
    }

    public bool IsAttachedToVisualTree { get; private set; }

    public Style Style
    {
        get => GetValue<Style>(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    protected void SetVisualParent(IUIComponent parent)
    {
        if (VisualParent == parent)
        {
            return;
        }

        var old = VisualParent;
        VisualParent = parent;

        if (IsAttachedToVisualTree)
        {
            var root = (this as IRootVisualComponent) ?? old.GetSelfAndVisualAncestors().OfType<IRootVisualComponent>().FirstOrDefault();
            var e = new VisualTreeAttachmentEventArgs(root);
            DetachedFromVisualTree(e);
        }

        if (VisualParent is IRootVisualComponent || VisualParent?.IsAttachedToVisualTree == true)
        {
            var root =  this.GetVisualAncestors().OfType<IRootVisualComponent>().FirstOrDefault();
            var e = new VisualTreeAttachmentEventArgs(root);
            AttachedToVisualTree(e);
        }

        OnVisualParentChanged(old, parent);
    }

    private void AttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        IsAttachedToVisualTree = true;

        OnAttachedToVisualTree(e);

        // TODO: check if we need to call AttachedToVisualTree in chain
        if (VisualChildren.Count > 0)
        {
            foreach (UIComponent visual in VisualChildren)
            {
                visual.AttachedToVisualTree(e);
            }
        }
    }

    private void DetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        IsAttachedToVisualTree = false;

        OnDetachedFromVisualTree(e);

        // TODO: check if we need to call DetachedFromVisualTree in chain
        if (VisualChildren.Count > 0)
        {
            foreach (UIComponent visual in VisualChildren)
            {
                visual.DetachedFromVisualTree(e);
            }
        }
    }

    protected virtual void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
         
    }

    protected virtual void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
         
    }

    protected void OnVisualParentChanged(IUIComponent oldParent, IUIComponent newParent)
    {
        VisualParentChanged?.Invoke(this, new VisualParentChangedEventArgs(oldParent, newParent));
    }

    protected virtual void OnRender(DrawingContext context)
    {
    }
}