using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.Navigation;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Resources;

namespace Adamantium.XamlTests;

// Shared no-op IUIApplication for tests that only need a live UIAppContext (a resolver + a no-op theme).
// ResourceManager is settable so a test can inject a real dictionary; every other member is inert.
internal sealed class FakeApp(IDependencyResolver resolver) : IUIApplication
{
    public IUIContext UIContext { get; } = new FakeContext(resolver);
    public IWindow MainWindow { get; set; }
    public IWindow ActiveWindow => null;
    public IReadOnlyList<IWindow> Windows => Array.Empty<IWindow>();
    public INavigationService Navigation => null;
    public IResourceManager ResourceManager { get; set; }
    public IThemeManager ThemeManager => null;
    public IGraphicsContext GraphicsContext => null;
    public IDispatcher Dispatcher => null;
    public void AddWindow(IWindow window) { }
    public void RemoveWindow(IWindow window) { }
    public void SetActiveWindow(IWindow window) { }
    public void InactivateWindow(IWindow window) { }
    public void ExecuteOnUIThread(Action action) => action();
    public Task ExecuteOnUIThreadAsync(Action action) { action(); return Task.CompletedTask; }
    public bool IsFixedTimeStep { get; set; }
    public double TimeStep => 0;
    public uint DesiredFPS { get; set; }
    public bool DisableRendering { get; set; }
}
