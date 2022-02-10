using Adamantium.Win32;
using System;

namespace Adamantium.UI.Windows;

public delegate IntPtr HandleMessage(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled);