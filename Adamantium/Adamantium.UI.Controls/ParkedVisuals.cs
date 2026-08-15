using System;
using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// The framework's ONE store of visuals kept between visits - what <c>x:KeepAlive</c> asks for, and where an
/// <c>x:Load</c> slot will put what it is not showing. It owns the whole of it: parking (so the renderer keeps what it
/// built), the key each visual waits under, and the eviction that stops a cache from being a leak.
/// <para>Several things replace a visual - a ContentPresenter swapping a data-templated body, a navigation adapter
/// swapping a resolved view - and each used to grow a dictionary of its own. They differ only in what the KEY is, so
/// that is all they pass in.</para>
/// </summary>
public static class ParkedVisuals
{
    private static int _limit = 20;

    private static readonly Dictionary<object, Entry> _kept = new();

    // Insertion order of the evictable ones, oldest first - what "the oldest is let go" means without a timestamp.
    private static readonly List<object> _evictable = [];

    /// <summary>How many <see cref="NavigationCacheMode.Enabled"/> visuals are kept before the oldest is let go.
    /// <see cref="NavigationCacheMode.Required"/> ones are never counted and never evicted - that is the difference
    /// between the two answers.
    /// <para>The default suits an ordinary application; it is a framework knob on purpose, because only the application
    /// knows how much memory it can spend to have views come back instantly. Raise it if there is memory to spare,
    /// lower it if there is not - lowering takes effect at once, the excess is let go on assignment rather than at the
    /// next navigation.</para></summary>
    public static int Limit
    {
        get => _limit;
        set
        {
            _limit = Math.Max(0, value);
            Evict();
        }
    }

    /// <summary>What the visual asked for, or <see cref="NavigationCacheMode.Disabled"/> when it asked for nothing.</summary>
    public static NavigationCacheMode ModeOf(IUIComponent visual) =>
        visual is IFundamentalUIComponent fundamental ? fundamental.KeepAlive : NavigationCacheMode.Disabled;

    /// <summary>True when this visual is worth keeping at all - the question every caller asks before letting go.</summary>
    public static bool ShouldKeep(IUIComponent visual) => ModeOf(visual) != NavigationCacheMode.Disabled;

    /// <summary>Park <paramref name="root"/> and remember it under <paramref name="key"/>. The caller still does the
    /// removing - a presenter removes its child, an adapter clears its Content - because only it knows what "remove"
    /// means for it; this marks the subtree FIRST, so that removal reads as "coming back" and not as "thrown away".</summary>
    public static void Keep(object key, IUIComponent root, TemplateResult built = null, DataTemplate template = null,
        Mathematics.Size hostSize = default)
    {
        if (key == null || root == null) return;

        // What the world looked like when it left, so the return can ask ONE question instead of revalidating six
        // thousand nodes: same window, same theme?
        var world = new World(root.RootVisual, Core.Resources.ThemeManager.Version);

        ParkedSubtree.Park(root);
        _kept[key] = new Entry(root, built, template, ModeOf(root), hostSize, world);

        if (ModeOf(root) != NavigationCacheMode.Required)
        {
            _evictable.Remove(key);
            _evictable.Add(key);
            Evict();
        }
    }

    /// <summary>Takes the visual kept under <paramref name="key"/>, if any. It is NOT unparked here: the caller has to
    /// put it back in the tree first, and unparking before that would tell the renderer it is live while it is nowhere.
    /// Use <see cref="ParkedSubtree.Unpark"/> once it is attached.</summary>
    public static bool TryTake(object key, IUIComponent host, out IUIComponent root, out TemplateResult built,
        out DataTemplate template, out Mathematics.Size hostSize)
    {
        root = null;
        built = null;
        template = null;
        hostSize = default;

        if (key == null || !_kept.Remove(key, out var entry)) return false;

        _evictable.Remove(key);
        root = entry.Root;
        built = entry.Built;
        template = entry.Template;
        hostSize = entry.HostSize;

        // Nothing changed about where it comes back to, so everything the attach walk would recompute per node already
        // holds the right value - the return may skip it. A different window or a theme swap in between means it may not.
        // Against the HOST's window, not the parked root's: a parked root is out of the tree, so its own RootVisual is
        // null and comparing it always answered "changed" - the cheap path could never be taken at all.
        IsUnchanged = entry.World == new World(host?.RootVisual, Core.Resources.ThemeManager.Version);
        return true;
    }

    /// <summary>Whether the visual the last <see cref="TryTake"/> handed back comes home to the same window and theme it
    /// left. Read straight after taking it, before it is attached.</summary>
    public static bool IsUnchanged { get; private set; }

    /// <summary>Drops everything kept, destroying what was built from a template. For app shutdown and for tests, which
    /// must not inherit another test's cache.</summary>
    public static void Clear()
    {
        foreach (var entry in _kept.Values)
        {
            entry.Built?.Destroy();
        }

        _kept.Clear();
        _evictable.Clear();
    }

    private static void Evict()
    {
        while (_evictable.Count > Limit)
        {
            var oldest = _evictable[0];
            _evictable.RemoveAt(0);
            if (_kept.Remove(oldest, out var entry)) entry.Built?.Destroy();
        }
    }

    private readonly record struct Entry(IUIComponent Root, TemplateResult Built, DataTemplate Template,
        NavigationCacheMode Mode, Mathematics.Size HostSize, World World);

    // The two things a parked subtree's per-node state depends on. Compared as a whole so adding a third is one edit.
    private readonly record struct World(IRootVisualComponent RootVisual, int ThemeVersion);
}
