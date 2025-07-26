namespace Adamantium.UI.Core.Controls;

public interface IWindowInternals
{
    void SetHandle(IntPtr handle);
    
    void SetSurface(IntPtr surfaceHandle);
    
    void OnSourceInitialized();
    
    void SetIsActive(bool isActive);
}