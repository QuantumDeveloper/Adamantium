using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;
using Adamantium.UI.Events;
using Adamantium.UI.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI;

public abstract class WindowBase : ContentControl, IWindow
{
    private IWindowRenderer _renderer;
    protected IWindowWorkerService WindowWorkerService { get; }

    public IWindowRenderer DefaultRenderer { get; set; }

    public IWindowRenderer Renderer
    {
        get => _renderer;
        set
        {
            if (value != _renderer)
            {
                OnRendererChanged(_renderer, value);
                _renderer = value;
            }
            
        }
    }

    private void OnRendererChanged(IWindowRenderer oldRenderer, IWindowRenderer newRenderer)
    {
        var args = new WindowRendererChangedEventArgs(oldRenderer, newRenderer);
        RendererChanged?.Invoke(this, args);
    }

    public IDependencyResolver Resolver => UIApplication.Current.Container;

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
        new PropertyMetadata(MSAALevel.X4, PropertyMetadataOptions.AffectsRender, MSAALevelChangedCallback));

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

    public DrawingContext GetDrawingContext()
    {
        if (Renderer != null)
        {
            return Renderer.DrawingContext;
        }

        if (DefaultRenderer != null)
            return DefaultRenderer.DrawingContext;

        throw new ArgumentException("Window does not contain renderer and could not return DrawingContext");
    }

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

    protected void OnClosed()
    {
        var closingArgs = new WindowClosingEventArgs();
        Closing?.Invoke(this, closingArgs);
        if (!closingArgs.Cancel)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public event EventHandler<WindowClosingEventArgs> Closing;
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<WindowRendererChangedEventArgs> RendererChanged;

    public event EventHandler<EventArgs> SourceInitialized;
}