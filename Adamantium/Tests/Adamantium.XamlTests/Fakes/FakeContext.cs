using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.XamlTests;

internal sealed class FakeContext(IDependencyResolver resolver) : IUIContext
{
    public T Resolve<T>(string name = "") => resolver.Resolve<T>(name);
    public object Resolve(Type type, string name = "") => resolver.Resolve(type, name);
    public IThemeEngine ThemeEngine { get; } = new FakeThemeEngine();
    public IUIApplication UIApplication => null;
}
