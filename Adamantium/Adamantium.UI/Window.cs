using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.MacOS;
using Adamantium.UI.Windows;

namespace Adamantium.UI
{
    public abstract class Window : ContentControl, IWindow
    {
        public static Window New()
        {
            if (RuntimeInformation.IsOSPlatform((OSPlatform.Windows)))
            {
                return new Win32Window();
            }
            else if (RuntimeInformation.IsOSPlatform((OSPlatform.OSX)))
            {
                return new MacOSWindow();
            }
            
            throw new NotSupportedException($"Window for {RuntimeInformation.OSArchitecture} is not yet supported");
        }
        
        public static readonly RoutedEvent ClientSizeChangedEvent = EventManager.RegisterRoutedEvent("ClientSizeChanged",
            RoutingStrategy.Direct, typeof(SizeChangedEventHandler), typeof(Window));
        
        public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
            typeof(Double), typeof(Window), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
            typeof(Double), typeof(Window), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
            typeof(String), typeof(Window));

        public static readonly AdamantiumProperty ClientWidthProperty = AdamantiumProperty.Register(nameof(Width),
            typeof(Double), typeof(FrameworkComponent),
            new PropertyMetadata(Double.NaN,
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
                PropertyMetadataOptions.AffectsRender, ClientWidthChangedCallBack));

        public static readonly AdamantiumProperty ClientHeightProperty = AdamantiumProperty.Register(nameof(Height),
            typeof(Double), typeof(FrameworkComponent),
            new PropertyMetadata(Double.NaN,
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
                PropertyMetadataOptions.AffectsRender, ClientHeightChangedCallBack));
        
        private static void ClientWidthChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
        {
            if (!(adamantiumObject is FrameworkComponent o)) return;
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
            if (!(adamantiumObject is FrameworkComponent o)) return;
            if (e.OldValue == AdamantiumProperty.UnsetValue)
                return;
            
            var old = new Size(o.Width, (double)e.OldValue);
            var newSize = new Size(o.Width, (double)e.NewValue);
            var args = new SizeChangedEventArgs(old, newSize, false, true);
            args.RoutedEvent = ClientSizeChangedEvent;
            o?.RaiseEvent(args);
        }

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
        public abstract IntPtr SurfaceHandle { get; }
        
        public abstract IntPtr Handle { get; internal set; }
        public abstract bool IsClosed { get; protected set; }

        public abstract Point PointToClient(Point point);
        public abstract Point PointToScreen(Point point);
        public abstract void Show();
        public abstract void Close();
        public abstract void Hide();
        public abstract bool IsActive { get; internal set; }
        
        public abstract event EventHandler<WindowClosingEventArgs> Closing;
        public abstract event EventHandler<EventArgs> Closed;
        
        public event EventHandler<EventArgs> SourceInitialized;
        
        public event SizeChangedEventHandler ClientSizeChanged
        {
            add => AddHandler(ClientSizeChangedEvent, value);
            remove => RemoveHandler(ClientSizeChangedEvent, value);
        }
        
        internal void OnSourceInitialized()
        {
            SourceInitialized?.Invoke(this, EventArgs.Empty);
        }
    }
}