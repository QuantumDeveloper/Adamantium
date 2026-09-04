using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>Records what a trigger was TOLD, so a trigger that appears stuck can be told apart from one that is never
/// asked again. Writes to a file beside the application - a probe that depends on someone redirecting stdout loses the
/// one occurrence that mattered.
/// <para>Watches one property by name (ADAM_TRIGGER_PROBE, e.g. "IsMouseOver"); anything else costs one string
/// comparison.</para></summary>
public static class TriggerProbe
{
    private static readonly string Watched = Environment.GetEnvironmentVariable("ADAM_TRIGGER_PROBE");
    private static readonly object Gate = new();
    private static readonly List<string> Pending = new();

    public static readonly string LogPath =
        System.IO.Path.Combine(AppContext.BaseDirectory, "trigger-probe.log");

    public static void Note(ITrigger trigger, IAdamantiumComponent host, bool conditionMet, bool wasMet)
    {
        if (Watched == null || trigger is not PropertyTrigger property) return;
        if (!string.Equals(property.Property, Watched, StringComparison.OrdinalIgnoreCase)) return;

        // WHO, not just what: every hovered button and list row raises this same trigger, so a line without the host is
        // unreadable - which is exactly how the first version of this probe came out.
        var who = host == null ? "<none>" : $"{host.GetType().Name}#{host.GetHashCode() & 0xFFFF:x4}";

        var line = $"{DateTime.Now:HH:mm:ss.fff} {who} {property.Property}={property.Value} " +
                   $"told={conditionMet} was={wasMet}{(conditionMet == wasMet ? " (no edge)" : " EDGE")}";

        Write(line);
    }

    /// <summary>What a trigger managed to UNDO. It can be told correctly and still leave its value standing: with no
    /// record of having applied the setter there is nothing to take back, and the value stays on the part for good -
    /// which from the outside looks exactly like a trigger that never fired.</summary>
    public static void NoteRemoval(ITrigger trigger, IAdamantiumComponent host, ISetter setter, bool hadRecord,
        IAdamantiumComponent target)
    {
        if (Watched == null || trigger is not PropertyTrigger property) return;
        if (!string.Equals(property.Property, Watched, StringComparison.OrdinalIgnoreCase)) return;

        var who = host == null ? "<none>" : $"{host.GetType().Name}#{host.GetHashCode() & 0xFFFF:x4}";
        var what = setter is Setter s ? $"{s.TargetName}.{s.Property}" : setter?.GetType().Name;

        // WHAT THE PART READS AS once the trigger has taken its value back. "Removed with a record" and "back to the
        // template's value" are not the same statement, and the difference is the whole question here.
        var after = "?";
        if (target != null && setter is Setter named && !string.IsNullOrEmpty(named.Property))
        {
            var prop = AdamantiumPropertyMap.ResolveProperty(target.GetType(), named.Property);
            if (prop != null) after = target.GetValue(prop)?.ToString() ?? "<null>";
        }

        Write($"{DateTime.Now:HH:mm:ss.fff} {who} REMOVE {what} hadRecord={hadRecord} after={after}");
    }

    private static void Write(string line)
    {
        lock (Gate)
        {
            Pending.Add(line);
            if (Pending.Count < 20) return;

            try
            {
                System.IO.File.AppendAllLines(LogPath, Pending);
            }
            catch
            {
                // A probe that throws is worse than a probe that misses a line.
            }

            Pending.Clear();
        }
    }

    /// <summary>Writes whatever has not reached the file yet. Called when the application is closing down, so the last
    /// few transitions - which are usually the interesting ones - are not lost with the process.</summary>
    public static void Flush()
    {
        lock (Gate)
        {
            if (Pending.Count == 0) return;
            try
            {
                System.IO.File.AppendAllLines(LogPath, Pending);
            }
            catch
            {
            }

            Pending.Clear();
        }
    }
}
