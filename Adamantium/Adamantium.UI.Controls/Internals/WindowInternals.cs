using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;

namespace Adamantium.UI.Controls.Internals;

internal static class WindowInternals
{
    public static void SetHandle(this IWindow window, IntPtr windowHandle)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.SetHandle(windowHandle);
        }
    }

    public static void RaiseWindowMoving(this IWindow window)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.RaiseWindowMoving();
        }
    }

    public static void RaiseWindowMoveCompleted(this IWindow window)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.RaiseWindowMoveCompleted();
        }
    }

    public static void SetSurfaceHandle(this IWindow window, IntPtr surfaceHandle)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.SetSurface(surfaceHandle);
        }
    }
    
    public static void OnSourceInitialized(this IWindow window)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.OnSourceInitialized();
        }
    }

    public static void SetIsActive(this IWindow window, bool isActive)
    {
        if (window is IWindowInternals windowBase)
        {
            windowBase.SetIsActive(isActive);
        }
    }
}