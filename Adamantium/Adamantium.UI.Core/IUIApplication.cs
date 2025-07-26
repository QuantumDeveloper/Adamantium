using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core;

public interface IUIApplication
{
    IWindow MainWindow { get; set; }
    
    IWindow ActiveWindow { get; }

    IReadOnlyList<IWindow> Windows { get; }

    IThemeManager ThemeManager { get; }
    
    IGraphicsContext GraphicsContext { get; }
    
    IDispatcher Dispatcher { get; }
    
    IUIContext UIContext { get; }

    void AddWindow(IWindow window);
    
    void RemoveWindow(IWindow window);
    
    void SetActiveWindow(IWindow window);
    
    void InactivateWindow(IWindow window);

    public void ExecuteOnUIThread(Action action);

    public Task ExecuteOnUIThreadAsync(Action action);

    /// <summary>
    /// Enables or disables fixed framerate
    /// </summary>
    Boolean IsFixedTimeStep { get; set; }

    /// <summary>
    /// Gets or set time step for limitation of rendering frequency
    /// <remarks>value must be in seconds</remarks>
    /// </summary>
    public Double TimeStep { get; }

    /// <summary>
    /// Desired number of frames per second
    /// </summary>
    public UInt32 DesiredFPS { get; set; }
    
    public bool DisableRendering { get; set; }
}
