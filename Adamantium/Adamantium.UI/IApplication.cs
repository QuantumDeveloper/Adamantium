using Adamantium.UI.Controls;
using System;
using System.Collections.Generic;

namespace Adamantium.UI;

public interface IUIApplication
{
    IWindow MainWindow { get; set; }

    IReadOnlyList<IWindow> Windows { get; }

    IThemeManager ThemeManager { get; }

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
