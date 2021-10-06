using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.UI
{
    public class WindowClosingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }
}
