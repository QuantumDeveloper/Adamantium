using System.Collections.Generic;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Services;

public interface IWindowPlatformService
{
    IWindow MainWindow { get; set; }
    
    IWindow ActiveWindow { get; }
    
    IReadOnlyList<IWindow> Windows { get; }
}