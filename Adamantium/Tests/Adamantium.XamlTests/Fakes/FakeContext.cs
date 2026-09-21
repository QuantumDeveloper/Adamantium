using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.XamlTests;

internal sealed class FakeContext(IDependencyResolver resolver) : IUIContext
{
    public T Resolve<T>(string name = "") => resolver.Resolve<T>(name);
    public object Resolve(Type type, string name = "") => resolver.Resolve(type, name);
    // Settable: a test about what a THEME does to a control has to run the real engine (a ThemeManager is one), not the
    // no-op - with the stub in place the control is marked styled and never gets a template at all.
    public IThemeEngine ThemeEngine { get; set; } = new FakeThemeEngine();
    public IUIApplication UIApplication => null;
}
