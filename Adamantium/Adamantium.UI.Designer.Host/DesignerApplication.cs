using Adamantium.UI.Universes;

namespace Adamantium.UI.Designer.Host;

/// <summary>
/// Minimal headless application for the designer host: its constructor bootstraps the engine (DI container,
/// graphics device service, resource factory, graphics context, theme manager, UI context, and
/// <c>UIAppContext.Current</c>) without ever calling <c>Run()</c> - so there is no main loop and no native window.
/// Derives from <see cref="MultiverseApplication"/> (not just UIApplication) so the design-time preview can host the
/// engine: it gains the registered <c>IUniverseService</c>. Universes are driven manually by the session (one snapshot of
/// N frames per preview) rather than by a live loop - the designer renders on demand to a PNG.
/// </summary>
public class DesignerApplication : MultiverseApplication
{
}
