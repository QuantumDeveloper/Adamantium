using System;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

public delegate IntPtr HandleMessage(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled);