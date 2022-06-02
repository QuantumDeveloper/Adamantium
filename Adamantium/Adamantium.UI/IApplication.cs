using Adamantium.UI.Controls;

namespace Adamantium.UI;

public interface IApplication
{
    IWindow MainWindow { get; set; }

    WindowCollection Windows { get; }

    IThemeManager ThemeManager { get; }
}
