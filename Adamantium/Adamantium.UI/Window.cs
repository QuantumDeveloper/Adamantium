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
        
        public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
            typeof(Double), typeof(Window), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
            typeof(Double), typeof(Window), new PropertyMetadata(0d));
        
        public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
            typeof(String), typeof(Window));

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
        
        // Pointer to the surface for rendering on this window
        public abstract IntPtr SurfaceHandle { get; }
        
        public abstract IntPtr Handle { get; internal set; }
        public abstract bool IsClosed { get; protected set; }
        public abstract int ClientWidth { get; set; }
        public abstract int ClientHeight { get; set; }
        
        public abstract Point PointToClient(Point point);
        public abstract Point PointToScreen(Point point);
        public abstract void Show();
        public abstract void Close();
        public abstract void Hide();
        
        public event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public abstract event EventHandler<WindowClosingEventArgs> Closing;
        public abstract event EventHandler<EventArgs> Closed;
        
        public event EventHandler<EventArgs> SourceInitialized;
        
        internal void OnSourceInitialized()
        {
            SourceInitialized?.Invoke(this, EventArgs.Empty);
        }
        
        internal void OnClientSizeChanged(SizeChangedEventArgs e)
        {
            ClientSizeChanged?.Invoke(this, e);
        }
    }
}