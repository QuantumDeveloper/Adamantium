using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

/// <summary>What the desktop is showing behind our window on one monitor: the picture, how it is laid out, and the
/// colour behind it. <see cref="File"/> is null when the desktop is a plain colour - which is a real answer, not a
/// failure, and a material then tints <see cref="Background"/>.
///
/// <para>A RECORD, so "did the wallpaper change" is one comparison of two answers rather than a pile of remembered
/// fields. <see cref="Revision"/> is what makes that comparison honest under a slideshow: Windows Spotlight rewrites
/// the SAME cache path with a new picture, so a change detected by path alone would be missed every time.</para></summary>
public sealed record WallpaperInfo(Uri File, WallpaperFit Fit, Color Background, Rect MonitorBounds, DateTime Revision)
{
    /// <summary>What a platform returns when it cannot answer at all - no picture, no monitor. Distinguished from a
    /// plain-colour desktop by <see cref="MonitorBounds"/> being empty.</summary>
    public static readonly WallpaperInfo None = new(null, WallpaperFit.Fill, Colors.Transparent, default, default);

    /// <summary>Whether this says anything usable. A material asks before deciding between the wallpaper and its
    /// fallback - see the note on <see cref="DesktopWallpaper"/>.</summary>
    public bool IsKnown => MonitorBounds is { Width: > 0, Height: > 0 };
}
