namespace Adamantium.UI.Core;

public interface IWindowWorkerService
{
    public void SetWindow(IWindow window);

    public void SetTitle(string title);

    public void ShowWindow(WindowState windowState);

    public void HideWindow();

    /// <summary>Acquire (true) or release (false) the OS-level mouse capture for this window, so a press-drag keeps
    /// receiving move/up even when the pointer leaves the window. Platform-specific (Win32 SetCapture/ReleaseCapture).
    /// The shared logic that decides WHEN to call this lives in MouseDevice.SyncOsMouseCapture, so every platform gets
    /// the same behaviour.</summary>
    public void SetMouseCapture(bool capture);

    public IUIContext UIContext { get; }
}