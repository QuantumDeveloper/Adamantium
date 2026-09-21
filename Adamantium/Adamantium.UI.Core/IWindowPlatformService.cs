namespace Adamantium.UI.Core;

public interface IWindowPlatformService
{
    IWindow MainWindow { get; set; }
    
    IWindow ActiveWindow { get; }
    
    IReadOnlyList<IWindow> Windows { get; }
    
    IWindowWorkerService GetWindowWorker(IUIContext uiContext);
}