using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The window the compass lives in: above everything, transparent to input, never focused.
/// <para>A WINDOW rather than something inside the docking area, because during a drag the thing being dragged is
/// itself a window - and nothing living inside another window can be drawn on top of that. This is the only reason the
/// compass is not simply a panel in the area it points at.</para>
/// <para>See-through per pixel: the indicators are rounded and antialiased, and their edges have to blend with whatever
/// is behind them rather than with a background of their own.</para>
/// </summary>
public class DockCompassWindow : Window
{
    public DockCompassWindow()
    {
        UseTransparentComposition = true;
        Topmost = true;
        // No frame and nothing to grab: an overlay has no caption, no buttons and no resize borders. Set here rather
        // than left to the template, because these decide the NATIVE window, not what is drawn inside it.
        ResizeMode = WindowResizeMode.NoResize;
        ShowWindowBorder = false;    // a shape floating over other windows, not a window with a frame
        TransparentToInput = true;   // it is a read-out of a gesture, never a thing to click
        // Not activating also keeps it out of the task bar and Alt-Tab: the platform worker turns it into a tool window.
        ActivateOnShow = false;      // taking focus mid-drag would end the drag it is there to serve
    }

}


