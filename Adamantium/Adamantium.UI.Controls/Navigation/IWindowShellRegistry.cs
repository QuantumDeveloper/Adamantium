using System;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Maps a shell key to a window factory, so window navigation can open a CUSTOM window (your own styles/chrome)
/// and load the content view into it. The framework registers <c>"default"</c> = a themed <see cref="Window"/>; an app
/// registers its own <see cref="Window"/> subclasses under keys (or overrides <c>"default"</c>).</summary>
public interface IWindowShellRegistry
{
    string DefaultKey { get; set; }
    void Register(string key, Func<IWindow> factory);
    void Register<TWindow>(string key) where TWindow : class, IWindow, new();

    /// <summary>Create the window shell for <paramref name="key"/> (null = default); falls back to the default shell.</summary>
    IWindow Create(string key);
}
