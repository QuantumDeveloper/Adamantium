namespace Adamantium.UI.Core;

public interface IWindowWorkerService
{
    public void SetWindow(IWindow window);

    public void SetTitle(string title);

    /// <summary>Move the OS window so its top-left sits at these SCREEN coordinates. Backs <c>Window.Left</c>/<c>Top</c>,
    /// which used to be managed values nobody acted on - assigning them changed a number and left the window where it
    /// was. Size and z-order are untouched, and the window is not activated: moving a window is not focusing it.</summary>
    public void SetPosition(double left, double top);

    /// <summary>Resize the OS window so its CLIENT area becomes this big, in logical (DIP) units. Backs
    /// <c>Window.ClientWidth</c>/<c>ClientHeight</c>, which - unlike Left/Top - used to be managed values nobody acted
    /// on: assigning them changed a number and left the window the size it was created. Position and z-order are
    /// untouched, and the window is not activated.</summary>
    public void SetSize(double clientWidth, double clientHeight);

    /// <summary>Re-apply the window's overlay traits - topmost, click-through, transparency - to the live OS window.
    /// Called whenever one of them changes, so setting <c>Topmost</c> on an open window actually raises it instead of
    /// changing a number nobody reads again.</summary>
    public void UpdateOverlayTraits();

    public void ShowWindow(WindowState windowState);

    public void HideWindow();

    /// <summary>Bring the window to the foreground (un-minimizing it first if needed). Used to activate a newly opened
    /// window and to focus an already-open single-instance window on a repeat request.</summary>
    public void Activate();

    /// <summary>Raise the window above the others WITHOUT giving it focus.
    /// <para>The distinction matters during a drag: taking the foreground makes the OS revoke the mouse capture the
    /// drag runs on (Win32 answers with WM_CAPTURECHANGED), which cancels the gesture the user is in the middle of.
    /// Raising the z-order alone leaves the capture - and the drag - intact, which is how dragging onto a window that
    /// sits behind is supposed to work.</para></summary>
    public void RaiseWithoutActivation();

    /// <summary>Acquire (true) or release (false) the OS-level mouse capture for this window, so a press-drag keeps
    /// receiving move/up even when the pointer leaves the window. Platform-specific (Win32 SetCapture/ReleaseCapture).
    /// The shared logic that decides WHEN to call this lives in MouseDevice.SyncOsMouseCapture, so every platform gets
    /// the same behaviour.</summary>
    public void SetMouseCapture(bool capture);

    /// <summary>Enter (true) or leave (false) RELATIVE mouse mode: the OS cursor is hidden and held centred, and each
    /// physical move is turned into a synthesized <c>RawMouseMove</c> delta (unbounded even at the window edge) for a
    /// hosted game's mouse-look. Replaces OS raw input. On leave, <paramref name="restoreScreen"/> is where the caller
    /// (the panel) wants the cursor to reappear, in SCREEN coordinates - so it comes back where it vanished, not at the
    /// re-centre. Driven by <c>RenderTargetPanel</c> per its <c>MouseLookMode</c>; no-op on platforms without it.</summary>
    public void SetRelativeMouseMode(bool enabled, PixelPoint restoreScreen);

    /// <summary>Start an OS-driven move of the window from the current cursor (custom-chrome caption drag). Runs the
    /// native modal move loop, so Aero Snap and snap-to-edges work. Call from a title bar's mouse-down. No-op on
    /// platforms without a native move loop.</summary>
    public void BeginMoveDrag();

    public IUIContext UIContext { get; }
}