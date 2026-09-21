namespace Adamantium.UI.Core.Controls;

public interface IWindowInternals
{
    void SetHandle(IntPtr handle);
    
    void SetSurface(IntPtr surfaceHandle);
    
    void OnSourceInitialized();
    
    void SetIsActive(bool isActive);

    /// <summary>The OS is moving this window (a caption drag). Raised on every step of the platform's move loop.</summary>
    void RaiseWindowMoving();

    /// <summary>The OS move loop finished - the button came up and the window is where it will stay.</summary>
    void RaiseWindowMoveCompleted();
}