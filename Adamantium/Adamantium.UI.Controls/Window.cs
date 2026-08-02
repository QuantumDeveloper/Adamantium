using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

public class Window : WindowBase
{
    public override IntPtr SurfaceHandle { get; internal set; }
    public override IntPtr Handle { get; internal set; }

    public override Vector2 PointToClient(Vector2 point)
    {
        return ScreenToClient(point);
    }

    public override Vector2 PointToScreen(Vector2 point)
    {
        return ClientToScreen(point);
    }

    public override void Show()
    {
        // A window built in code has no OS window yet, and showing one is exactly the moment it needs one - so attach it
        // to the running application and initialize it HERE instead of making every caller remember to. Leaving that to
        // callers meant `new Window { ... }.Show()` - the thing a window API is expected to mean - silently did nothing:
        // no handle, so the guard below returned and no window ever appeared. A caller that wants the window live before
        // it is shown still calls AttachContextAndInitialize itself; this only fills in the gap.
        if (Handle == IntPtr.Zero && UIContext == null && UIAppContext.Current?.UIContext is { } context)
        {
            AttachContextAndInitialize(context);
        }

        if (Handle == IntPtr.Zero)
            return;

        VerifyAccess();
        if (Renderer is not { FirstFrameProcessed: true })
        {
            WindowWorkerService.SetTitle(Title);
            ShouldDisplayWindow = true;
        }
        else
        {
            var firstDisplay = ShouldDisplayWindow;   // this Show is the deferred FIRST display of the window
            ShouldDisplayWindow = false;
            WindowWorkerService.ShowWindow(State);
            // Pull the window to the foreground when it first appears (ShowWindow alone doesn't steal focus from the
            // launching foreground window). Applies to the main window (OnStartup) and any window opened from a VM.
            if (firstDisplay) Activate();
        }
    }
        
    public override void Close()
    {
        // Cancels a DEFERRED show, exactly as Hide does. A window closed before it drew its first frame still had
        // "display me later" standing, and the renderer carried it out after the first Present - so the window came up
        // AFTER being closed and stayed there, owned by nobody.
        // Measured on restoring a layout that has floating panels: the old windows are closed and the new ones opened in
        // the same pass, and the ones closed while still pending simply reappeared - a fresh pair of stray windows on
        // every restore.
        ShouldDisplayWindow = false;
        IsClosed = true;
        OnClosed();
    }

    public override void Hide()
    {
        // Cancels a DEFERRED show as well. Show() on a window that has not drawn its first frame only asks to be shown
        // later (ShouldDisplayWindow, carried out by the renderer after the first Present); left standing, that request
        // outlived the Hide and put the window back up by itself.
        // Measured on the docking compass: an overlay shown and hidden inside one drag came back after the first frame
        // and stayed - a topmost, captionless window over the docking area and over the floating windows, which is what
        // "the control froze" was. The window said it was hidden; the OS said otherwise.
        ShouldDisplayWindow = false;
        WindowWorkerService.HideWindow();
    }
}