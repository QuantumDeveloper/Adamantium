using Adamantium.Win32;
using System;

namespace Adamantium.UI.Windows
{
    public delegate IntPtr WndProcHook(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam, ref bool handled);
}
