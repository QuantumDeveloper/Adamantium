using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Windows;

namespace Adamantium.UI
{
    public abstract class Window : ContentControl, IWindow
    {
        public static IWindow New()
        {
            if (RuntimeInformation.IsOSPlatform((OSPlatform.Windows)))
            {
                return new Win32Window();
            }
            else if (RuntimeInformation.IsOSPlatform((OSPlatform.OSX)))
            {
                return new OSXWindow();
            }
            
            throw new NotSupportedException("This ");
        }

        public abstract Point PointToClient(Point point);
        public abstract Point PointToScreen(Point point);
        public abstract void Show();
        public abstract void Close();
        public abstract void Hide();
        public abstract IntPtr Handle { get; internal set; }
        public abstract bool IsClosed { get; protected set; }
        public abstract int ClientWidth { get; set; }
        public abstract int ClientHeight { get; set; }
        public abstract event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public abstract event EventHandler<WindowClosingEventArgs> Closing;
        public abstract event EventHandler<EventArgs> Closed;
    }
}