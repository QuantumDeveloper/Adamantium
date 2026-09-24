namespace Adamantium.Game.Core;

/// <summary>
/// When the universe's loop ends.
/// </summary>
public enum ShutDownMode
{
    /// <summary>
    /// When the main output is closed.
    /// </summary>
    OnMainWindowClosed,

    /// <summary>
    /// Only when <c>ShutDown</c> is called.
    /// </summary>
    OnExplicitShutDown,

    /// <summary>
    /// When the last output is closed.
    /// </summary>
    OnLastWindowClosed,
}
