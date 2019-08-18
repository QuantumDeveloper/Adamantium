using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.Win32;

namespace Adamantium.UI.Windows
{
    public class Win32Window : ContentControl, IWindow
    {
        internal static string DefaultClassName { get; private set; } = "Adamantium Window";
        public IntPtr Handle { get; internal set; }

        public int ClientWidth { get; set; }
        public int ClientHeight { get; set; }

        public bool IsClosed { get; set; }
        internal bool IsLocked = false;


        private WindowWorker windowWorker;

        public Win32Window()
        {
            windowWorker = new WindowWorker();
        }

        public void Show()
        {
            if (Handle == IntPtr.Zero)
            {
                windowWorker.SetWindow(this);
            }
        }

        public void Close()
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

        public event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public event EventHandler<WindowClosingEventArgs> Closing;
        public event EventHandler<EventArgs> Closed;
        public event EventHandler<EventArgs> SourceInitialized;

        internal void OnClientSizeChanged(SizeChangedEventArgs e)
        {
            ClientSizeChanged?.Invoke(this, e);
        }

        public bool IsActive { get; internal set; }

        public Point PointToClient(Point point)
        {
            return ScreenToClient(point);
        }

        public Point PointToScreen(Point point)
        {
            return ClientToScreen(point);
        }

        internal Point ScreenToClient(Point p)
        {
            var point = new NativePoint((int)p.X, (int)p.Y);
            Interop.ScreenToClient(Handle, ref point);
            return point;
        }

        internal Point ClientToScreen(Point p)
        {
            var point = new NativePoint((int)p.X, (int)p.Y);
            Interop.ClientToScreen(Handle, ref point);
            return point;
        }

        public void Hide()
        {
            throw new NotImplementedException();
        }
    }

}
