using System;
using Adamantium.MacOS;
using Adamantium.Mathematics;
using Adamantium.UI.Windows;

namespace Adamantium.UI.MacOS
{
    public class MacOSWindow : Window
    {
        public override IntPtr Handle { get; internal set; }

        public override bool IsClosed { get; protected set; }

        public override IntPtr SurfaceHandle => MacOSInterop.GetViewPtr(Handle);
        //public event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public override event EventHandler<WindowClosingEventArgs> Closing;
        public override event EventHandler<EventArgs> Closed;
        
        internal MacOSWindowWorker WindowWorker { get; }

        public MacOSWindow()
        {
            WindowWorker = new MacOSWindowWorker();
        }

        public override void Close()
        {
            
        }

        public override void Hide()
        {
            
        }

        public override bool IsActive { get; internal set; }

        public override Point PointToClient(Point point)
        {
            return new Point();
        }

        public override Point PointToScreen(Point point)
        {
            return new Point();
        }

        public override void Show()
        {
            if (Handle == IntPtr.Zero)
            {
                WindowWorker.SetWindow(this);
            }
        }
    }
}