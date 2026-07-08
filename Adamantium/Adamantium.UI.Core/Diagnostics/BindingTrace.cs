using System;

namespace Adamantium.UI.Core.Diagnostics;

/// <summary>
/// Opt-in binding trace, mirroring <see cref="LayoutTrace"/>. OFF by default (the <see cref="Log"/> guard is a single
/// bool check, so no production overhead). A developer debugging a binding sets <see cref="Enabled"/> + <see cref="Sink"/>
/// to see, for example, an <c>{Ancestor}</c> that resolved no matching ancestor even though its target IS in the tree -
/// the failure WPF's RelativeSource swallowed silently. Structured status still lives on the expression
/// (<c>BindingExpressionBase.Status</c>) for always-on, zero-noise inspection.
/// </summary>
public static class BindingTrace
{
    public static bool Enabled;
    public static Action<string> Sink;

    public static void Log(string message)
    {
        if (Enabled) Sink?.Invoke(message);
    }
}
