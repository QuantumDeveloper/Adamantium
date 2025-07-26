using System;

namespace Adamantium.UI.Platforms.Windows;

public delegate IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled);