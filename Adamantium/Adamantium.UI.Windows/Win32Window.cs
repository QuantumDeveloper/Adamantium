using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.Win32;

namespace Adamantium.UI.Windows
{
    public class Win32Window : Window, IWindow
    {
        internal static string DefaultClassName { get; private set; } = "Adamantium Window";
        public override IntPtr Handle { get; internal set; }

        public override int ClientWidth { get; set; }
        public override int ClientHeight { get; set; }

        public override bool IsClosed { get; protected set; }
        internal bool IsLocked = false;


        private WindowWorker windowWorker;

        public Win32Window()
        {
            windowWorker = new WindowWorker();
        }

        public override void Show()
        {
            if (Handle == IntPtr.Zero)
            {
                windowWorker.SetWindow(this);
            }
        }

        public override  void Close()
        {
            IsClosed = true;
            OnClosed();
        }

        internal void OnSourceInitialized()
        {
            SourceInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void OnClosed()
        {
            var closingArgs = new WindowClosingEventArgs();
            Closing?.Invoke(this, closingArgs);
            if (!closingArgs.Cancel)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }

        public override event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public override event EventHandler<WindowClosingEventArgs> Closing;
        public override event EventHandler<EventArgs> Closed;
        public event EventHandler<EventArgs> SourceInitialized;

        internal void OnClientSizeChanged(SizeChangedEventArgs e)
        {
            ClientSizeChanged?.Invoke(this, e);
        }

        public bool IsActive { get; internal set; }

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
