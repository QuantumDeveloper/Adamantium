using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// One root of a layout: a screen rectangle plus the tree inside it. The main window and every floating window are
/// EQUAL roots - a floating pane is not a special case, or every operation would fork into "in the main window" and
/// "in a floating one", and moving a pane between them would stop being a move and become interop.
/// <para>This rectangle is the ONLY absolute geometry in a layout. Everything below is fractions, which is what lets a
/// saved layout survive a different window size, a different monitor or a different resolution untouched.</para>
/// </summary>
public class DockingRoot
{
    public DockingRoot(PaneNode content, bool isMain = false)
    {
        Content = content;
        IsMain = isMain;
    }

    /// <summary>True for the root that lives in the application's main window (there is at most one).</summary>
    public bool IsMain { get; set; }

    /// <summary>Where the window sits, in SCREEN coordinates. Restoring checks it against the available screens - a
    /// window saved on a monitor that is no longer there has to come back somewhere visible.</summary>
    public Rect Bounds { get; set; }

    public PaneNode Content { get; set; }
}
