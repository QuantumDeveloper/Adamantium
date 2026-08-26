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

    // Keyed by the content AND by the presenter that parked it. The content alone is not enough: one view model is shown
    // by more than one presenter at a time - a tab's body draws the page, and the tab's HEADER draws a label from the very
    // same view model. Keyed by content only, the header asked "is there a visual for this?" during a re-template and was
    // handed the whole PAGE, which it then hosted inside the header card. The strip measured itself to it (480,132 px
    // tall), the body's row collapsed to nothing, and the page was drawn across the tab headers - which is what a theme
    // swap looked like after visiting a tab.
    private readonly record struct Slot(object Owner, object Key);

    private static readonly Dictionary<Slot, Entry> _kept = new();

    // TEMP (leak hunt): parked subtrees held across swaps.
    public static int Count => _kept.Count;

    // Insertion order of the evictable ones, oldest first - what "the oldest is let go" means without a timestamp.
    private static readonly List<Slot> _evictable = [];

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
    public static void Keep(object owner, object key, IUIComponent root, TemplateResult built = null, DataTemplate template = null,
        Mathematics.Size hostSize = default)
    {
        if (owner == null || key == null || root == null) return;

        // What the world looked like when it left, so the return can ask ONE question instead of revalidating six
        // thousand nodes: same window, same theme?
        var world = new World(root.RootVisual, Core.Resources.ThemeManager.Version);

        var slot = new Slot(owner, key);
        ParkedSubtree.Park(root);
        _kept[slot] = new Entry(root, built, template, ModeOf(root), hostSize, world);

        if (ModeOf(root) != NavigationCacheMode.Required)
        {
            _evictable.Remove(slot);
            _evictable.Add(slot);
            Evict();
        }
    }

    /// <summary>Takes the visual kept under <paramref name="key"/>, if any. It is NOT unparked here: the caller has to
    /// put it back in the tree first, and unparking before that would tell the renderer it is live while it is nowhere.
    /// Use <see cref="ParkedSubtree.Unpark"/> once it is attached.</summary>
    public static bool TryTake(object owner, object key, IUIComponent host, out IUIComponent root, out TemplateResult built,
        out DataTemplate template, out Mathematics.Size hostSize)
    {
        root = null;
        built = null;
        template = null;
        hostSize = default;

        if (owner == null || key == null) return false;

        var slot = new Slot(owner, key);
        if (!_kept.Remove(slot, out var entry)) return false;

        _evictable.Remove(slot);
        root = entry.Root;
        built = entry.Built;
        template = entry.Template;
        hostSize = entry.HostSize;

        // Nothing changed about where it comes back to, so everything the attach walk would recompute per node already
        // holds the right value - the return may skip it. A different window or a theme swap in between means it may not.
        // Against the HOST's window, not the parked root's: a parked root is out of the tree, so its own RootVisual is
        // null and comparing it always answered "changed" - the cheap path could never be taken at all.
        var now = new World(host?.RootVisual, Core.Resources.ThemeManager.Version);
        IsUnchanged = entry.World == now;

        // Asked SEPARATELY from the above, because the two answers cost different things. A theme swap invalidates what
        // every node in the subtree WEARS, and that is only put right by re-applying styles - a parked subtree is out of
        // the tree when the swap happens, so the walk that re-themes the application never reaches it. Coming home to a
        // different WINDOW needs no such thing. Conflating them would either re-theme a subtree that has nothing wrong
        // with it or, as it did, hand back a whole tab still wearing the theme it was parked under.
        ThemeChanged = entry.World.ThemeVersion != now.ThemeVersion;
        return true;
    }

    /// <summary>Whether the visual the last <see cref="TryTake"/> handed back comes home to the same window and theme it
    /// left. Read straight after taking it, before it is attached.</summary>
    public static bool IsUnchanged { get; private set; }

    /// <summary>Whether the theme changed while the visual the last <see cref="TryTake"/> handed back was parked - so it
    /// is still wearing the previous one and has to be re-styled. Read straight after taking it.</summary>
    public static bool ThemeChanged { get; private set; }

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
