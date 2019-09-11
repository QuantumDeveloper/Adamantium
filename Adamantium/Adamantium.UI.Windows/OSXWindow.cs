using System;
using System.Collections.ObjectModel;
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

        public override event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public override event EventHandler<WindowClosingEventArgs> Closing;
        public override event EventHandler<EventArgs> Closed;

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
            
        }
    }
}