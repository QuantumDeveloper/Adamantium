namespace Adamantium.UI.Designer.Host;

/// <summary>
/// Minimal headless <see cref="UIApplication"/> for the designer host: its constructor bootstraps the engine
/// (DI container, graphics device service, resource factory, graphics context, theme manager, UI context, and
/// <c>UIAppContext.Current</c>) without ever calling <c>Run()</c> - so there is no game loop and no native window.
/// </summary>
public class DesignerApplication : UIApplication
{
}
