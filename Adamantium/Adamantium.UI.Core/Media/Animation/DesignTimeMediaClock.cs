using System.Collections.Generic;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// Design-time heartbeat for frame-based media (animated images). At runtime an animated <c>Image</c> advances itself
/// off a real <see cref="System.Timers.Timer"/>; in the headless designer there is no real clock, so animated media
/// register here and the live previewer ticks them once per captured frame - the same way <see cref="AnimationManager"/>
/// drives property animations. Mirrors that class: a fresh preview tree calls <see cref="Reset"/> so media bound to the
/// discarded tree don't linger in this shared static and get advanced against dead controls.
/// </summary>
public static class DesignTimeMediaClock
{
    private static readonly List<IDesignTimeAnimatedMedia> Active = new();

    /// <summary>True while any animated medium is registered - the live designer polls this to decide whether to keep ticking.</summary>
    public static bool HasActiveMedia => Active.Count > 0;

    public static void Register(IDesignTimeAnimatedMedia media)
    {
        if (!Active.Contains(media)) Active.Add(media);
    }

    public static void Unregister(IDesignTimeAnimatedMedia media) => Active.Remove(media);

    /// <summary>Drops every registered medium - the live designer calls this when it builds a fresh preview tree.</summary>
    public static void Reset() => Active.Clear();

    /// <summary>Advances every registered medium by <paramref name="deltaSeconds"/>; drops any that finished.</summary>
    public static void Tick(double deltaSeconds)
    {
        for (var i = Active.Count - 1; i >= 0; i--)
            if (!Active[i].AdvanceDesignTime(deltaSeconds))
                Active.RemoveAt(i);
    }
}
