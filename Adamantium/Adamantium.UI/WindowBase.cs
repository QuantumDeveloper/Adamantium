using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI
{
    public abstract class WindowBase : ContentControl, IWindow
    {
        protected IWindowWorkerService WindowWorkerService { get; }
        
        public WindowBase()
        {
            WindowWorkerService = IWindowWorkerService.GetWorker();
        }
        
        public static readonly RoutedEvent ClientSizeChangedEvent = EventManager.RegisterRoutedEvent("ClientSizeChanged",
            RoutingStrategy.Direct, typeof(SizeChangedEventHandler), typeof(WindowBase));

        public static readonly RoutedEvent MSAALevelChangedEvent = EventManager.RegisterRoutedEvent("MSAALevelChanged",
            RoutingStrategy.Direct, typeof(MSAALeveChangedHandler), typeof(WindowBase));
        
        public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
            typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
            typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
            typeof(String), typeof(WindowBase));

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
            new PropertyMetadata( Engine.Graphics.MSAALevel.X4, PropertyMetadataOptions.AffectsRender, MSAALevelChanged));

        private static void MSAALevelChanged(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
        {
            if (!(adamantiumComponent is WindowBase component)) return;

            var args = new MSAALevelChangedEventArgs((MSAALevel)e.NewValue);
            args.RoutedEvent = MSAALevelChangedEvent;
            component.RaiseEvent(args);
        }

        private static void ClientWidthChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
        {
            if (!(adamantiumObject is WindowBase o)) return;
            Size old = default;
            if (e.OldValue == AdamantiumProperty.UnsetValue)
                return;
            
            old.Width = (double) e.OldValue;
            old.Height = o.Height;
            
            var newSize = new Size((double)e.NewValue, o.Height);
            var args = new SizeChangedEventArgs(old, newSize, true, false);
            args.RoutedEvent = ClientSizeChangedEvent;
            o.RaiseEvent(args);
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

        public abstract Point PointToClient(Point point);
        public abstract Point PointToScreen(Point point);
                
        internal Point ScreenToClient(Point p)
        {
            var point = new NativePoint((int)p.X, (int)p.Y);
            Win32Interop.ScreenToClient(Handle, ref point);
            return point;
        }

        internal Point ClientToScreen(Point p)
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
        
        internal void OnSourceInitialized()
        {
            SourceInitialized?.Invoke(this, EventArgs.Empty);
            InvalidateMeasure();
        }

        public abstract void Render();
        
        public void Update()
        {
            var stack = new Stack<IVisualComponent>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                var control = stack.Pop();

                UpdateControl(control);

                foreach (var visual in control.GetVisualDescends())
                {
                    stack.Push(visual as FrameworkComponent);
                }
            }
        }

        private void UpdateControl(IVisualComponent visualComponent)
        {
            var control = (FrameworkComponent)visualComponent;
            if (!control.IsMeasureValid)
            {
                if (!Double.IsNaN(control.Width) && !Double.IsNaN(control.Height))
                {
                    Size s = new Size(control.Width, control.Height);
                    control.Measure(s);
                }
                else if (Double.IsNaN(control.Width) && !Double.IsNaN(control.Height))
                {
                    control.Measure(new Size(Double.PositiveInfinity, control.Height));
                }
                else if (!Double.IsNaN(control.Width) && Double.IsNaN(control.Height))
                {
                    control.Measure(new Size(control.Width, Double.PositiveInfinity));
                }
                else
                {
                    control.Measure(Size.Infinity);
                }
            }

            if (!control.IsArrangeValid)
            {
                control.Arrange(new Rect(control.DesiredSize));
            }

            if (control.Parent != null)
            {
                control.Location = control.Bounds.Location + control.Parent.Location;
                control.ClipPosition = control.ClipRectangle.Location + control.Parent.Location;
            }
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
}