namespace Adamantium.UI.Core;

public interface IWindowWorkerService
{
    public void SetWindow(IWindow window);

    public void SetTitle(string title);

    public void ShowWindow(WindowState windowState);

    public void HideWindow();

    /// <summary>Bring the window to the foreground (un-minimizing it first if needed). Used to activate a newly opened
    /// window and to focus an already-open single-instance window on a repeat request.</summary>
    public void Activate();

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
    public void SetRelativeMouseMode(bool enabled, Adamantium.Mathematics.Vector2 restoreScreen);

    /// <summary>Start an OS-driven move of the window from the current cursor (custom-chrome caption drag). Runs the
    /// native modal move loop, so Aero Snap and snap-to-edges work. Call from a title bar's mouse-down. No-op on
    /// platforms without a native move loop.</summary>
    public void BeginMoveDrag();

    public IUIContext UIContext { get; }
}