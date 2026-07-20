using System.Threading.Tasks;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>Convenience entry points for showing an <see cref="OverlayWindow"/> on a parent window's overlay.</summary>
public static class OverlayWindowExtensions
{
    /// <summary>Shows the window on this host's overlay and completes with its result when it closes.</summary>
    public static Task<object> ShowOverlayWindowAsync(this IPopupHost host, OverlayWindow window)
        => OverlayWindowManager.GetFor(host).ShowAsync(window);

    /// <summary>Shows the window on this host's overlay without awaiting its result.</summary>
    public static void ShowOverlayWindow(this IPopupHost host, OverlayWindow window)
        => OverlayWindowManager.GetFor(host).Show(window);
}
