using System.Runtime.InteropServices;
using Adamantium.UI.Controls;
using Adamantium.UI.Windows;

namespace Adamantium.UI
{
    public abstract class Window : ContentControl, IWindow
    {
        public static IWindow New()
        {
            if (RuntimeInformation.IsOSPlatform((OSPlatform.Windows)))
            {
                return new Win32Window();
            }
            else if (RuntimeInformation.IsOSPlatform((OSPlatform.OSX)))
            {
                return new OSXWindow();
            }
        }
    }
}