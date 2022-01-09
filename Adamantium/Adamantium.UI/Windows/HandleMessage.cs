using Adamantium.Win32;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.UI.Windows;

public delegate IntPtr HandleMessage(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled);