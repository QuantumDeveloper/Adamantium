using System;
using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// The framework's one store of visuals kept between visits (<c>x:KeepAlive</c>): parking, the key each waits under,
/// and the eviction that keeps it from being a leak. Callers differ only in the key they pass.
/// </summary>
public static class ParkedVisuals
{
    private static int _limit = 20;

    // By owner as well as content: a tab's header and body show the same view model, and keyed by content alone the
    // header was handed the whole page.
    private readonly record struct Slot(object Owner, object Key);

    private static readonly Dictionary<Slot, Entry> _kept = new();

    // TEMP (leak hunt): parked subtrees held across swaps.
    public static int Count => _kept.Count;

    // Oldest first.
    private static readonly List<Slot> _evictable = [];

    /// <summary>How many <see cref="NavigationCacheMode.Enabled"/> visuals are kept before the oldest is let go;
    /// <see cref="NavigationCacheMode.Required"/> ones never count. Lowering it trims at once.</summary>
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

    /// <summary>Parks <paramref name="root"/> under <paramref name="key"/>. Call before removing it, so the removal reads
    /// as "coming back"; the caller does the removing.</summary>
    public static void Keep(object owner, object key, IUIComponent root, TemplateResult built = null, DataTemplate template = null,
        Mathematics.Size hostSize = default)
    {
        if (owner == null || key == null || root == null) return;

        // What the world looked like when it left, so the return asks one question instead of revalidating every node.
        var world = new World(root.RootVisual, Core.Resources.ThemeManager.Version,
            Core.Resources.ThemeManager.PaletteVersion);

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

    /// <summary>Takes the visual kept under <paramref name="key"/>, if any. Not unparked here: attach it first, then
    /// <see cref="ParkedSubtree.Unpark"/>.</summary>
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

        // Against the host's window: a parked root is out of the tree, and its own RootVisual is null.
        var now = new World(host?.RootVisual, Core.Resources.ThemeManager.Version,
            Core.Resources.ThemeManager.PaletteVersion);
        IsUnchanged = entry.World == now;

        // Asked separately: a theme swap needs a restyle, which another window does not.
        ThemeChanged = entry.World.ThemeVersion != now.ThemeVersion;
        return true;
    }

    /// <summary>Whether the visual the last <see cref="TryTake"/> handed back comes home to the same window and theme it
    /// left. Read straight after taking it, before it is attached.</summary>
    public static bool IsUnchanged { get; private set; }

    /// <summary>Whether the theme changed while the visual the last <see cref="TryTake"/> handed back was parked, so it
    /// has to be restyled. Read straight after taking it.</summary>
    public static bool ThemeChanged { get; private set; }

    /// <summary>Discards for good, whatever their mode, the visuals <paramref name="owner"/> kept for content it no
    /// longer <paramref name="holds"/> - a closed tab: nothing will come back for them.</summary>
    public static void ReleaseAbsent(object owner, Predicate<object> holds)
    {
        List<Slot> gone = null;
        foreach (var slot in _kept.Keys)
        {
            if (ReferenceEquals(slot.Owner, owner) && !holds(slot.Key))
            {
                (gone ??= []).Add(slot);
            }
        }

        if (gone == null)
        {
            return;
        }

        foreach (var slot in gone)
        {
            _kept.Remove(slot, out var entry);
            _evictable.Remove(slot);
            ParkedSubtree.Discard(entry.Root);
            entry.Built?.Destroy();
        }
    }

    /// <summary>Discards everything kept. For app shutdown and for tests, which must not inherit another test's cache.</summary>
    public static void Clear()
    {
        foreach (var entry in _kept.Values)
        {
            ParkedSubtree.Discard(entry.Root);
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
            if (_kept.Remove(oldest, out var entry))
            {
                ParkedSubtree.Discard(entry.Root);
                entry.Built?.Destroy();
            }
        }
    }

    private readonly record struct Entry(IUIComponent Root, TemplateResult Built, DataTemplate Template,
        NavigationCacheMode Mode, Mathematics.Size HostSize, World World);

    // PaletteVersion too: a variant switch recolors brushes without moving ThemeVersion, and a parked subtree cannot hear it.
    private readonly record struct World(IRootVisualComponent RootVisual, int ThemeVersion, int PaletteVersion);
}
