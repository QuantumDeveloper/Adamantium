using System;
using System.Collections.Generic;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Default <see cref="IWindowShellRegistry"/>. Seeds <c>"default"</c> with a plain <see cref="Window"/>; an app
/// can register custom window shells or override the default.</summary>
public sealed class WindowShellRegistry : IWindowShellRegistry
{
    private readonly Dictionary<string, Func<IWindow>> _shells = new();

    public WindowShellRegistry()
    {
        DefaultKey = "default";
        _shells[DefaultKey] = () => new Window();
    }

    public string DefaultKey { get; set; }

    public void Register(string key, Func<IWindow> factory) => _shells[key] = factory;
    public void Register<TWindow>(string key) where TWindow : class, IWindow, new() => _shells[key] = () => new TWindow();

    public IWindow Create(string key)
    {
        key ??= DefaultKey;
        if (_shells.TryGetValue(key, out var factory)) return factory();
        if (_shells.TryGetValue(DefaultKey, out var fallback)) return fallback();
        return new Window();
    }
}
