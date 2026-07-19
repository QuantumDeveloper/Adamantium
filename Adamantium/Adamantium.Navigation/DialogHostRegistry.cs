using System.Collections.Generic;

namespace Adamantium.Navigation;

/// <summary>Default <see cref="IDialogHostRegistry"/>. <see cref="DialogHostKind.Default"/> resolves to
/// <see cref="DialogHostKind.Overlay"/>; an unregistered kind falls back to the overlay host.</summary>
public sealed class DialogHostRegistry : IDialogHostRegistry
{
    private const DialogHostKind DefaultKind = DialogHostKind.Overlay;

    private readonly Dictionary<DialogHostKind, IDialogHost> _hosts = new();

    public void Register(DialogHostKind kind, IDialogHost host) => _hosts[kind] = host;

    public IDialogHost Get(DialogHostKind kind)
    {
        var resolved = kind == DialogHostKind.Default ? DefaultKind : kind;
        if (_hosts.TryGetValue(resolved, out var host)) return host;
        return _hosts.TryGetValue(DefaultKind, out var fallback) ? fallback : null;
    }

    public IDialogHost Default => Get(DialogHostKind.Default);
}
