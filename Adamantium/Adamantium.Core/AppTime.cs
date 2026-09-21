using System;

namespace Adamantium.Core;

/// <summary>
/// Contains game frame time, total time and FPS
/// </summary>
public struct AppTime
{
    /// <summary>
    /// Number of frames passed since game start
    /// </summary>
    public ulong FramesCount { get; set; }

    /// <summary>
    /// current FPS
    /// </summary>
    public float Fps { get; set; }

    /// <summary>
    /// Total application time
    /// </summary>
    public TimeSpan TotalTime { get; set; }

    /// <summary>
    /// Time of one frame in seconds
    /// </summary>
    public double FrameTime { get; set; }
}