using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.UI.Controls
{
    public interface IWindow : IRootVisual
    {

        event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        event EventHandler<EventArgs> Loaded;
        event EventHandler<EventArgs> Closing;
        event EventHandler<EventArgs> Closed;
    }
}
