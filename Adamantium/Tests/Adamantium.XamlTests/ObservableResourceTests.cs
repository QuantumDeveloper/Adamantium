using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// {ObservableResource} is the LIVE, tree-scoped keyed-resource marker: unlike {ResourceReference} (resolved once), it
/// re-resolves when the resource set changes (a theme swap, or a dictionary loaded/unloaded), which the ResourceManager
/// signals once per layout pass via <see cref="ResourceManager.FlushResourceChanges"/>. This drives that real path.
/// </summary>
[TestFixture]
public class ObservableResourceTests
{
    private ResourceManager _rm;
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer());
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void FreshResources()
    {
        // A fresh ResourceManager per test: the context is process-global, so without this the Global provider would
        // accumulate each test's dictionaries and leak values across tests.
        _rm = new ResourceManager();
        _app.ResourceManager = _rm;
        // UIAppContext.Initialize is idempotent (Current ??= app), so if another XamlTests fixture initialized first, our
        // Initialize was a no-op and Current points at ITS app (ResourceManager == null). Force ours for our tests - the
        // other fixtures use only a no-op ThemeContext + resolver, which ours provides too, so this doesn't disturb them.
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    [Test]
    public void ObservableResource_ReResolves_WhenResourceSetChanges()
    {
        var owner = new Border();
        _rm.AddSource(owner, typeof(ResourcesV1), ResourceScope.Global);

        // Connect the live marker to a property; it resolves immediately.
        var button = new Button();
        new ObservableResource("AccentColor").Apply(button, "Content");
        Assert.That(button.Content, Is.EqualTo("RED"), "initial resolve");

        // Swap the dictionary (as a theme swap would) and flush - the live marker must pick up the new value.
        _rm.RemoveSources(owner);
        _rm.AddSource(owner, typeof(ResourcesV2), ResourceScope.Global);
        _rm.FlushResourceChanges();

        Assert.That(button.Content, Is.EqualTo("BLUE"), "live: re-resolved after the resource set changed");
    }

    [Test]
    public void ObservableResource_TransientMiss_DoesNotClobberLastValue()
    {
        var owner = new Border();
        _rm.AddSource(owner, typeof(ResourcesV1), ResourceScope.Global);

        var button = new Button();
        new ObservableResource("AccentColor").Apply(button, "Content");
        Assert.That(button.Content, Is.EqualTo("RED"));

        // Remove the source (nothing resolves the key now) and flush: a transient miss must NOT null out the property.
        _rm.RemoveSources(owner);
        _rm.FlushResourceChanges();
        Assert.That(button.Content, Is.EqualTo("RED"), "a resolve miss keeps the last good value, never clobbers to null");
    }

    private sealed class ResourcesV1 : ResourceDictionary
    {
        protected override void OnInitialize() => Add("AccentColor", "RED");
    }

    private sealed class ResourcesV2 : ResourceDictionary
    {
        protected override void OnInitialize() => Add("AccentColor", "BLUE");
    }

    // ---- minimal fakes: only ResourceManager is exercised (a real instance); the rest are no-ops ----

    private sealed class FakeApp(IDependencyResolver resolver) : IUIApplication
    {
        public IUIContext UIContext { get; } = new FakeContext(resolver);
        public IWindow MainWindow { get; set; }
        public IWindow ActiveWindow => null;
        public IReadOnlyList<IWindow> Windows => Array.Empty<IWindow>();
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

    private sealed class FakeContext(IDependencyResolver resolver) : IUIContext
    {
        public T Resolve<T>(string name = "") => resolver.Resolve<T>(name);
        public object Resolve(Type type, string name = "") => resolver.Resolve(type, name);
        public IThemeContext ThemeContext { get; } = new FakeThemeContext();
        public IUIApplication UIApplication => null;
    }

    private sealed class FakeThemeContext : IThemeContext
    {
        public void ApplyCurrentTheme(IFundamentalUIComponent control) { }
        public void ApplyStyles(IFundamentalUIComponent component) { }
        public void ApplyExternalStyles(IFundamentalUIComponent control, params Style[] styles) { }
    }
}
