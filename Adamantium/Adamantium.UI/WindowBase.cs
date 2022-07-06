using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI;

public abstract class WindowBase : ContentControl, IWindow
{
    protected IWindowWorkerService WindowWorkerService { get; }
    
    private IWindowRenderer renderer;
        
    public WindowBase()
    {
        WindowWorkerService = IWindowWorkerService.GetWorker();
        InitializeComponent();
    }

    protected virtual void InitializeComponent()
    {
    }

    public static readonly RoutedEvent ClientSizeChangedEvent = EventManager.RegisterRoutedEvent("ClientSizeChanged",
        RoutingStrategy.Direct, typeof(SizeChangedEventHandler), typeof(WindowBase));

    public static readonly RoutedEvent MSAALevelChangedEvent = EventManager.RegisterRoutedEvent("MSAALevelChanged",
        RoutingStrategy.Direct, typeof(MSAALeveChangedHandler), typeof(WindowBase));
        
    public static readonly RoutedEvent StateChangedEvent = EventManager.RegisterRoutedEvent("StateChanged",
        RoutingStrategy.Direct, typeof(StateChangedHandler), typeof(WindowBase));



    public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
    public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(String), typeof(WindowBase), new PropertyMetadata(String.Empty, TitleChangedCallback));

    public static readonly AdamantiumProperty ClientWidthProperty = AdamantiumProperty.Register(nameof(Width),
        typeof(Double), typeof(WindowBase),
        new PropertyMetadata(Double.NaN,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender, ClientWidthChangedCallBack));

    public static readonly AdamantiumProperty ClientHeightProperty = AdamantiumProperty.Register(nameof(Height),
        typeof(Double), typeof(WindowBase),
        new PropertyMetadata(Double.NaN,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender, ClientHeightChangedCallBack));
        
    public static readonly AdamantiumProperty MSAALevelProperty = AdamantiumProperty.Register(nameof(MSAALevel), 
        typeof(MSAALevel), typeof(WindowBase),
        new PropertyMetadata(Engine.Graphics.MSAALevel.X4, PropertyMetadataOptions.AffectsRender, MSAALevelChangedCallback));

    public static readonly AdamantiumProperty StateProperty = AdamantiumProperty.Register(nameof(State), 
        typeof(WindowState), typeof(WindowBase),
        new PropertyMetadata(WindowState.Normal, PropertyMetadataOptions.AffectsRender, StateChangedCallback));

    private static void TitleChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(a is WindowBase component)) return;

        if (component.WindowWorkerService != null)
        {
            var title = (string)e.NewValue;
            component.WindowWorkerService.SetTitle(title);
        }
    }

    private static void StateChangedCallback(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is WindowBase component)) return;

        var args = new StateChangedEventArgs((WindowState)e.NewValue);
        args.RoutedEvent = StateChangedEvent;
        component.RaiseEvent(args);
    }
        
    private static void MSAALevelChangedCallback(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is WindowBase component)) return;

        var args = new MSAALevelChangedEventArgs((MSAALevel)e.NewValue);
        args.RoutedEvent = MSAALevelChangedEvent;
        component.RaiseEvent(args);
    }

    private static void ClientWidthChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumObject is WindowBase component)) return;
        Size old = default;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        old.Width = (double) e.OldValue;
        old.Height = component.Height;
            
        var newSize = new Size((double)e.NewValue, component.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = ClientSizeChangedEvent;
        component.RaiseEvent(args);
    }
        
    private static void ClientHeightChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumObject is WindowBase component)) return;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        var old = new Size(component.Width, (double)e.OldValue);
        var newSize = new Size(component.Width, (double)e.NewValue);
        var args = new SizeChangedEventArgs(old, newSize, false, true);
        args.RoutedEvent = ClientSizeChangedEvent;
        component?.RaiseEvent(args);
    }
        
    internal bool IsLocked { get; set; }

    public Double Left
    {
        get => GetValue<Double>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }
        
    public Double Top
    {
        get => GetValue<Double>(TopProperty);
        set => SetValue(TopProperty, value);
    }
        
    public string Title
    {
        get => GetValue<string>(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public MSAALevel MSAALevel
    {
        get => GetValue<MSAALevel>(MSAALevelProperty);
        set => SetValue(MSAALevelProperty, value);
    }

    public WindowState State
    {
        get => GetValue<WindowState>(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public Double ClientWidth
    {
        get => GetValue<Double>(ClientWidthProperty);
        set => SetValue(ClientWidthProperty, value);
    }

    public Double ClientHeight
    {
        get => GetValue<Double>(ClientHeightProperty);
        set => SetValue(ClientHeightProperty, value);
    }
        
    // Pointer to the surface for rendering on this window
    public abstract IntPtr SurfaceHandle { get; internal set; }
        
    public abstract IntPtr Handle { get; internal set; }
    public bool IsClosed { get; protected set; }

    public abstract Vector2 PointToClient(Vector2 point);
    public abstract Vector2 PointToScreen(Vector2 point);
                
    internal Vector2 ScreenToClient(Vector2 p)
    {
        var point = new NativePoint((int)p.X, (int)p.Y);
        Win32Interop.ScreenToClient(Handle, ref point);
        return point;
    }

    internal Vector2 ClientToScreen(Vector2 p)
    {
        var point = new NativePoint((int)p.X, (int)p.Y);
        Win32Interop.ClientToScreen(Handle, ref point);
        return point;
    }

    public abstract void Show();
    public abstract void Close();
    public abstract void Hide();
        
    public abstract bool IsActive { get; internal set; }
        
    public event EventHandler<WindowClosingEventArgs> Closing;
    public event EventHandler<EventArgs> Closed;
        
    public event EventHandler<EventArgs> SourceInitialized;
        
    public event SizeChangedEventHandler ClientSizeChanged
    {
        add => AddHandler(ClientSizeChangedEvent, value);
        remove => RemoveHandler(ClientSizeChangedEvent, value);
    }
        
    public event MSAALeveChangedHandler MSAALevelChanged
    {
        add => AddHandler(MSAALevelChangedEvent, value);
        remove => RemoveHandler(MSAALevelChangedEvent, value);
    }

    public event StateChangedHandler StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    internal void OnSourceInitialized()
    {
        SourceInitialized?.Invoke(this, EventArgs.Empty);
        InvalidateMeasure();
    }

    public abstract void Render();

    public void Update()
    {
        ProcessVisualTree(this, UpdateComponent);
        ProcessVisualTree(this, UpdateComponentLocation);
    }

    private void ProcessVisualTree(IUIComponent component, Action<IUIComponent> processAction)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(component);
        while (stack.Count > 0)
        {
            var control = stack.Pop();
            
            processAction(control);

            foreach (var visual in control.GetVisualDescendants())
            {
                stack.Push(visual);
            }
        }
    }
        
    private void UpdateComponent(IUIComponent visualComponent)
    {
        var control = (MeasurableUIComponent)visualComponent;
        var parent = control.VisualParent as IMeasurableComponent;
        if (!control.IsMeasureValid)
        {
            if (control is IWindow wnd)
            {
                MeasureControl(control, wnd.ClientWidth, wnd.ClientHeight);
            }
            else
            {
                MeasureControl(control, control.Width, control.Height);
            }
        }
            
        if (!control.IsArrangeValid)
        {
            if (parent != null)
            {
                control.Arrange(new Rect(parent.DesiredSize));
            }
            else
            {
                control.Arrange(new Rect(control.DesiredSize));
            }
        }
            
        // if (control.Parent != null)
        // {
        //     control.Location = control.Bounds.Location + control.Parent.Location;
        //     control.ClipPosition = control.ClipRectangle.Location + control.Parent.Location;
        // }
    }

    private void UpdateComponentLocation(IUIComponent visualComponent)
    {
        if (visualComponent.LogicalParent != null)
        {
            visualComponent.Location = visualComponent.Bounds.Location + ((IUIComponent)(visualComponent.LogicalParent)).Location;
            visualComponent.ClipPosition = visualComponent.ClipRectangle.Location + ((IUIComponent)(visualComponent.LogicalParent)).Location;
        }
    }

    private void MeasureControl(IMeasurableComponent control, Double width, Double height)
    {
        if (!Double.IsNaN(width) && !Double.IsNaN(height))
        {
            Size s = new Size(width, height);
            control.Measure(s);
        }
        else if (Double.IsNaN(width) && !Double.IsNaN(height))
        {
            control.Measure(new Size(Double.PositiveInfinity, height));
        }
        else if (!Double.IsNaN(width) && Double.IsNaN(height))
        {
            control.Measure(new Size(width, Double.PositiveInfinity));
        }
        else
        {
            control.Measure(Size.Infinity);
        }
    }
    
    internal void SetRenderer(IWindowRenderer renderer)
    {
        this.renderer = renderer;
        renderer.SetWindow(this);
    }

    protected void OnClosed()
    {
        var closingArgs = new WindowClosingEventArgs();
        Closing?.Invoke(this, closingArgs);
        if (!closingArgs.Cancel)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}