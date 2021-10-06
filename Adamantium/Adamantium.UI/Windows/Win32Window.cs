using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.Win32;

namespace Adamantium.UI.Windows
{
    public class Win32Window : Window
    {
        public override IntPtr Handle { get; internal set; }

        private Win32WindowWorker win32WindowWorker;

        public Win32Window()
        {
            win32WindowWorker = new Win32WindowWorker();
        }

        public override void Show()
        {
            if (Handle == IntPtr.Zero)
            {
                VerifyAccess();
                win32WindowWorker.SetWindow(this);
            }
        }

        public override IntPtr SurfaceHandle => Handle;
        //public override event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        //public override event EventHandler<WindowClosingEventArgs> Closing;
        //public override event EventHandler<EventArgs> Closed;

        public override bool IsActive { get; internal set; }

        public override  Point PointToClient(Point point)
        {
            return ScreenToClient(point);
        }

        public override Point PointToScreen(Point point)
        {
            return ClientToScreen(point);
        }

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

        public override void Hide()
        {
            
        }
    }

}
