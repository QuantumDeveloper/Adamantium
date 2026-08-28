using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core;

public interface IUIContext
{
    T Resolve<T>(string name = "");
    
    object Resolve(Type type, string name = "");
    
    public IThemeEngine ThemeEngine { get; }
    
    public IUIApplication UIApplication { get; }
}