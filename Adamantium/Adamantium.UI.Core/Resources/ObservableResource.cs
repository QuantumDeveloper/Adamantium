using System.Runtime.CompilerServices;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// Markup extension <c>{ObservableResource Key}</c>: a LIVE, tree-scoped reference to a keyed resource. Unlike
/// <see cref="ResourceReference"/> - a one-shot lookup resolved once when the element attaches - this stays connected:
/// a theme swap, or a resource dictionary loaded/unloaded, flows straight to every control that references the key, with
/// no reload. The dictionary analog of <see cref="ThemeResource"/> (which tracks a single theme PROPERTY); backed by its
/// own <see cref="ObservableResourceExpression"/>.
/// </summary>
public class ObservableResource : MarkupExtension
{
    // Live expressions created by Apply, per target, keyed by "property@priority" + token - so a setter/trigger disposes
    // the exact expression it established without disturbing a same-property expression at another priority/token (a
    // template base under a hover-trigger override). Weak keys: never pins a control. Mirrors ThemeResource.
    private static readonly ConditionalWeakTable<IAdamantiumComponent, Dictionary<(string Slot, object Token), ObservableResourceExpression>> _applied = new();

    public ObservableResource()
    {
    }

    public ObservableResource(string key)
    {
        Key = key;
    }

    /// <summary>The resource key to resolve and track.</summary>
    [DefaultProperty]
    public string Key { get; set; }

    /// <summary>Connects a live <see cref="ObservableResourceExpression"/> from the keyed resource to
    /// <paramref name="propertyName"/> on <paramref name="target"/>. Used by codegen, setters and triggers.</summary>
    /// <remarks>Any component, not only one that lives in a tree. A GRADIENT STOP is the case that forced this: a
    /// theme's fade is a brush, a brush is not in the visual tree, and its stops could therefore only take the STATIC
    /// <c>{ResourceReference}</c> - resolved once, so every gradient in the application kept the colours of the variant
    /// it was built under while the solid fills around it followed. Resolution copes: a target with no tree resolves
    /// against Theme and Global, which is exactly where a palette colour lives.</remarks>
    public ObservableResourceExpression Apply(IAdamantiumComponent target, string propertyName,
        ValuePriority priority = ValuePriority.Template, object token = null)
    {
        var expression = new ObservableResourceExpression(target, target.GetProperty(propertyName), Key, priority, token);
        expression.EstablishConnection();

        var map = _applied.GetValue(target, static _ => new Dictionary<(string, object), ObservableResourceExpression>());
        var slot = (propertyName + "@" + priority, token);
        if (map.TryGetValue(slot, out var previous)) previous.CloseConnection();   // defensive: re-apply on the same slot
        map[slot] = expression;
        return expression;
    }

    /// <summary>Disposes the expression a setter/trigger established (closing its resource subscription) and clears the
    /// value it pushed - so leaving a trigger or detaching a style fully undoes an <c>{ObservableResource}</c>.</summary>
    public static void Remove(IAdamantiumComponent target, string propertyName, ValuePriority priority, object token = null)
    {
        if (!_applied.TryGetValue(target, out var map)) return;
        if (map.Remove((propertyName + "@" + priority, token), out var expression))
        {
            expression.CloseConnection();
            if (priority == ValuePriority.Trigger && token != null)
                target.ClearTriggerValue(target.GetProperty(propertyName), token);
            else
                target.ClearValue(propertyName, priority);
        }
    }

    public override object ProvideObject(MarkupContext context)
    {
        if (context?.TargetObject is IAdamantiumComponent target && !string.IsNullOrEmpty(context.TargetPropertyName))
            Apply(target, context.TargetPropertyName);
        return this;
    }
}
