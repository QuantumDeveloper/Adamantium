namespace Adamantium.Game.Core;

/// <summary>
/// The operating system's windows and message loop, for a universe that opens its own window.
/// </summary>
public interface IWindowingPlatform
{
    /// <summary>
    /// Opens a window with the given client size and makes an output of it.
    /// </summary>
    UniverseOutput CreateWindow(uint width, uint height);

    /// <summary>
    /// Runs the message loop until <paramref name="token"/> is cancelled.
    /// </summary>
    void Run(CancellationToken token);
}
