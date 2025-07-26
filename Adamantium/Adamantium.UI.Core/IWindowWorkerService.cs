namespace Adamantium.UI.Core;

public interface IWindowWorkerService
{
    public void SetWindow(IWindow window);

    public void SetTitle(string title);

    public void ShowWindow(WindowState windowState);
    
    public void HideWindow();
    
    public IUIContext UIContext { get; }
}