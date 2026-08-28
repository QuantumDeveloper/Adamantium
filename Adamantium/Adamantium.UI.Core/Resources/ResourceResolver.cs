using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Adamantium.UI.Core.Resources;

public static class ResourceResolver
{
    // Keyed weakly: remembering an ask must not keep the brush - or the element it will later be asked about - alive.
    private static readonly ConditionalWeakTable<IAdamantiumComponent, List<(string Property, string Key)>> _pending = new();

    public static T Resolve<T>(string literalName)
    {
        var currentTheme = UIAppContext.Current.ThemeManager.CurrentTheme;

        if (currentTheme == null)
        {
            throw new ResourceNotFoundException($"Current theme not found. Please, check correctness of theme initialization");
        }

        var resource = currentTheme.GetResource(literalName);

        if (resource == null)
        {
            throw new ResourceNotFoundException(
                $"Resource {literalName} is not found for theme: {currentTheme.Name}");
        }

        return (T)resource;
    }

    // A {ResourceReference} on a plain markup object - a template selector, a converter, anything the author writes as an
    // element but that is not part of the property system. There is no property store to defer into and no place in the
    // tree to be scoped from, so the key is resolved AT ONCE and flat: the Local scope first (the dictionary that
    // declared it is already registered by the time the object is built), then Theme, then Global.
    public static object ResolveNow(string key)
    {
        var resourceManager = UIAppContext.Current.ResourceManager;

        return resourceManager.FindResourceInScope(key, ResourceScope.Local) ?? resourceManager.FindResource(key);
    }

    // A {ResourceReference} used DIRECTLY on a component property (not via a Setter/trigger). The target is any
    // AdamantiumComponent, NOT only a UI element: a resource reference is a property-system feature, so it applies to a
    // GradientStop.Color, a Pen's brush, any animatable component - the same way WPF's Static/DynamicResource work on any
    // DependencyObject, not just UIElements. The target is not yet in the tree when its properties are assigned during
    // construction, so a Local resource can't be tree-scoped at that moment. Resolve the Theme/Global value immediately,
    // then - ONLY for a target that lives in the VISUAL tree - re-resolve tree-scoped once it attaches (the cascade brings
    // the full ancestor chain, incl. a Local resource's owner, and a nearer Local hit wins).
    /// <param name="priority">Where the reference was WRITTEN, expressed as a priority - the same one a literal in that
    /// position gets. Inside a ControlTemplate a part's value must be Template, so a trigger targeting that part can
    /// still change it; written directly on an element it is that element's own value, which is Local.
    /// <para>It has to be told, because this method cannot see where it was called from - and getting it wrong is
    /// silent: with Local, every trigger that has to change a metric-driven part simply stops working, which is how a
    /// vertical slider lost its handle, its fill and its direction all at once.</para></param>
    public static void SetDeferred(IAdamantiumComponent target, string property, string key,
        ValuePriority priority = ValuePriority.Local)
    {
        var resourceManager = UIAppContext.Current?.ResourceManager;

        var baseline = resourceManager?.FindResource(key);
        if (baseline != null)
            target.SetValue(property, baseline, priority);

        if (target is IUIComponent visual)
        {
            // Remembered as well as re-resolved on attach. Attaching is not the only thing that changes the answer:
            // an element inside a theme SCOPE resolves against that scope's theme, and the scope can be switched while
            // the element sits still - a preview pane changing its variant. Without the record there would be nothing
            // to ask again with, and the pane would keep the colours it happened to attach with.
            _pending.GetValue(target, static _ => []).Add((property, key));

            visual.AttachedToVisualTreeEvent += (_, _) =>
            {
                var scoped = resourceManager.FindResource(visual, key);
                if (scoped != null)
                    visual.SetValue(property, scoped, priority);
            };
            return;
        }

        // A NON-VISUAL target - a brush, a gradient stop - takes no part in the visual tree, so a resource declared on a
        // VIEW was simply never found and the brush painted nothing, silently. Remember the ask instead: the target
        // reaches a tree later, when an element takes it for one of its properties (see Resolve).
        _pending.GetValue(target, static _ => []).Add((property, key));
    }

    /// <summary>Whether this target is still waiting on a resource that only a tree can answer.</summary>
    public static bool HasPending(IAdamantiumComponent target) => _pending.TryGetValue(target, out _);

    /// <summary>Ask every <c>{ResourceReference}</c> in this subtree again, because the theme that answers them has
    /// changed - a scope was given a different theme or variant while the elements stood still.
    /// <para>Styles are re-applied by the ordinary re-theme, but a reference written straight onto an attribute is not
    /// a style: it was resolved once, when the element attached, and nothing would ever ask again. That is the
    /// difference between a scope that can be SWITCHED and one that can only be set before anything is shown.</para>
    /// <para>Walks the subtree, which is fine for what triggers it: a scope changing is an explicit, rare act, unlike
    /// the per-frame paths where a walk of this shape would be indefensible.</para></summary>
    public static void ReResolveSubtree(IUIComponent root)
    {
        if (root == null) return;

        ResolveThrough(root, root);
        foreach (var child in root.VisualChildren) ReResolveSubtree(child);
    }

    /// <summary>Answer a non-visual target's deferred resources from <paramref name="anchor"/>'s position in the tree.
    /// Called when the target finally has one. Entries are kept, not consumed: a theme swap re-resolves through the same
    /// anchor, and the ask is what stays true, not the answer.</summary>
    public static void Resolve(IAdamantiumComponent target, IUIComponent anchor)
    {
        if (anchor == null || !_pending.TryGetValue(target, out _))
        {
            return;
        }

        ResolveThrough(target, anchor);

        // And AGAIN when the anchor enters the tree: markup assigns a brush to its element BEFORE that element is added
        // to its parent, so the walk above starts from an element that has no ancestors yet.
        anchor.AttachedToVisualTreeEvent += (_, _) => ResolveThrough(target, anchor);
    }

    private static void ResolveThrough(IAdamantiumComponent target, IUIComponent anchor)
    {
        if (!_pending.TryGetValue(target, out var asks))
        {
            return;
        }

        var resourceManager = UIAppContext.Current?.ResourceManager;
        if (resourceManager == null)
        {
            return;
        }

        var resolvedAny = false;
        foreach (var ask in asks)
        {
            var scoped = resourceManager.FindResource(anchor, ask.Key);
            if (scoped == null)
            {
                continue;
            }

            target.SetValue(ask.Property, scoped);
            resolvedAny = true;
        }

        // RE-RECORD, not a paint re-bake: a texture is asked for while the unit is routed into a batch, so an ImageBrush
        // that got its source this way would otherwise stay empty for ever - the value there, nobody looking again.
        if (resolvedAny)
        {
            anchor.InvalidateRender(false);
        }
    }
}
