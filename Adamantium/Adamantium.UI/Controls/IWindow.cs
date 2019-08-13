using Adamantium.EntityFramework.ComponentsBasics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.UI.Controls
{
    public interface IWindow : IRootVisual, IComponent
    {
        void Show();
        void Close();
        void Hide();

        IntPtr Handle { get; }

        public bool IsClosed { get; }

        event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        event EventHandler<EventArgs> Loaded;
        event EventHandler<EventArgs> Closing;
        event EventHandler<EventArgs> Closed;
    }
}
