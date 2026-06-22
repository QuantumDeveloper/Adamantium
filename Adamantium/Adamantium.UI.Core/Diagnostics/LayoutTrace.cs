using System;

namespace Adamantium.UI.Core.Diagnostics;

/// <summary>
/// Opt-in layout/update trace. OFF by default (the <see cref="Log"/> guard is a single bool check, so there is no
/// overhead in production). A test or debug session sets <see cref="Enabled"/> + <see cref="Sink"/> to capture the
/// measure / arrange / update calls and pinpoint a dynamic-relayout bug without guessing.
/// </summary>
public static class LayoutTrace
{
    public static bool Enabled;
    public static Action<string> Sink;

    public static void Log(string message)
    {
        if (Enabled) Sink?.Invoke(message);
    }
}
