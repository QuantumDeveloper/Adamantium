namespace Adamantium.UI.Core;

public class UIAppContext
{
    public static IUIApplication Current { get; private set; }
    
    public static IWindowPlatformService PlatformService { get; private set; }

    public static void Initialize(IUIApplication app, IWindowPlatformService platformService)
    {
        Current ??= app;
        PlatformService ??= platformService;
    }
}