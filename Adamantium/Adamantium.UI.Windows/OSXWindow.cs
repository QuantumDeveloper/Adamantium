using System;
using System.Collections.ObjectModel;
using Adamantium.MacOS;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;

namespace Adamantium.UI.Windows
{
    public class OSXWindow : Window
    {
        public override IntPtr Handle { get; internal set; }

        public override bool IsClosed { get; protected set; }

        public override int ClientWidth { get; set; }
        public override int ClientHeight { get; set; }

        public override IntPtr SurfaceHandle => MacOSInterop.GetViewPtr(Handle);
        //public event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public override event EventHandler<WindowClosingEventArgs> Closing;
        public override event EventHandler<EventArgs> Closed;
        
        internal OSXWindowWorker WindowWorker { get; }

        public OSXWindow()
        {
            WindowWorker = new OSXWindowWorker();
        }

        public override void Close()
        {
            
        }

        public override void Hide()
        {
            
        }

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