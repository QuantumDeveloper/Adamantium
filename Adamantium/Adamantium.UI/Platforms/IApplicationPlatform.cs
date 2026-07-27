using System;
using System.Threading;
using Adamantium.Mathematics;

namespace Adamantium.UI.Platforms;

public interface IApplicationPlatform
{
    void Run(CancellationToken token);

    bool IsOnUIThread { get; }

    void Signal();

    event Action Signaled;

    /// <summary>The native handle of the window the OS considers TOPMOST at a physical screen point, or zero when the
    /// platform cannot answer. Only the OS knows the real z-order - walking our own window list cannot, so with
    /// overlapping windows it would pick whichever comes first, and would claim a hit even when another application's
    /// window covers ours. Click-through windows (our drag ghost) are never reported.</summary>
    IntPtr WindowFromScreenPoint(Vector2 point);
}