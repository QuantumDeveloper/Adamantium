using System.Runtime.CompilerServices;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// Markup extension <c>{ThemeResource Key}</c>: a live reference to a brush property of the ACTIVE theme (its
/// accent/focus colours). Unlike <see cref="ResourceReference"/> - a one-time lookup in the brush dictionary - this
/// stays connected: changing the theme's accent/focus at runtime flows straight to every control that references it,
/// with no theme reload. Structured like <see cref="TemplateBinding"/> (a markup extension backed by its own
/// expression), but its source is the global current theme rather than a templated parent.
/// </summary>
public class ThemeResource : MarkupExtension
{
    // Live expressions created by Apply, per target, keyed by "property@priority". Lets a setter/trigger dispose the
    // exact expression it established (Remove) without disturbing a same-property expression at another priority - e.g.
    // a template's base {ThemeResource} sitting under a hover trigger's override. Weak keys: never pins a control.
    private static readonly ConditionalWeakTable<IFundamentalUIComponent, Dictionary<(string Slot, object Token), ThemeResourceExpression>> _applied = new();

    public ThemeResource()
    {
    }

    public ThemeResource(string key)
    {
        Key = key;
    }

    /// <summary>The name of the theme property to track (e.g. AccentFillColorDefault).</summary>
    [DefaultProperty]
    public string Key { get; set; }

    /// <summary>Connects a live <see cref="ThemeResourceExpression"/> from the theme's <see cref="Key"/> property to
    /// <paramref name="propertyName"/> on <paramref name="target"/>. Used by codegen, setters and the runtime loader.</summary>
    public ThemeResourceExpression Apply(IFundamentalUIComponent target, string propertyName,
        ValuePriority priority = ValuePriority.Template, object token = null)
    {
        var expression = new ThemeResourceExpression(target, target.GetProperty(propertyName), Key, priority, token);
        expression.EstablishConnection();

        var map = _applied.GetValue(target, static _ => new Dictionary<(string, object), ThemeResourceExpression>());
        // Keyed per token so two trigger {ThemeResource}s on the same part property (a checkbox's accent under its
        // checked+hover accent-secondary) each keep their own live connection and stack, instead of one closing the other.
        var slot = (propertyName + "@" + priority, token);
        if (map.TryGetValue(slot, out var previous)) previous.CloseConnection();   // defensive: re-apply on the same slot
        map[slot] = expression;
        return expression;
    }

    /// <summary>Disposes the expression a setter/trigger established (closing its theme subscription) and clears the
    /// value it pushed - so leaving a trigger or detaching a style fully undoes a <c>{ThemeResource}</c>.</summary>
    public static void Remove(IFundamentalUIComponent target, string propertyName, ValuePriority priority, object token = null)
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
        if (context?.TargetObject is IFundamentalUIComponent target && !string.IsNullOrEmpty(context.TargetPropertyName))
            Apply(target, context.TargetPropertyName);
        return this;
    }
}
