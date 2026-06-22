namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// Frame-based media (e.g. an animated image) that the designer advances from its own virtual clock during a live
/// preview, instead of the real-time timer it uses at runtime. Implementors register with
/// <see cref="DesignTimeMediaClock"/> while <see cref="Design.IsDesignMode"/> is set.
/// </summary>
public interface IDesignTimeAnimatedMedia
{
    /// <summary>Advance playback by <paramref name="deltaSeconds"/> of virtual time. Returns false when there is nothing
    /// left to play (so the clock can drop it); a looping medium returns true indefinitely.</summary>
    bool AdvanceDesignTime(double deltaSeconds);
}
