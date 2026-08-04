using System;
using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Terse ancestor binding: <c>{Ancestor TitleBar, Foreground}</c> reads a property off the nearest ancestor of a given
/// type and pushes it to the target property. The modern replacement for WPF's verbose
/// <c>{Binding RelativeSource={RelativeSource FindAncestor, AncestorType=...}}</c>. Positional args are the ancestor
/// TYPE then the source PATH; the rest are named. Matching is is-a (a base type / interface matches any subtype),
/// consistent with the style selectors. The lookup walks the VISUAL tree by default (template parts and item content
/// are visual — not logical — children of their control, so that's where the owning control actually lives); set
/// <see cref="Logical"/> to walk the logical tree instead. The connection is LIVE: it re-resolves whenever the target
/// (re)attaches to the tree — a template rebuild, a re-parent, a virtualization recycle — so it never goes stale the
/// way WPF's did.
/// </summary>
public class Ancestor : MarkupExtension
{
    /// <summary>The ancestor type to search for (first positional). is-a: a base type / interface matches subtypes.</summary>
    [DefaultProperty]
    public Type AncestorType { get; set; }

    /// <summary>The property to read off the resolved ancestor (second positional), e.g. <c>Foreground</c>.</summary>
    public string Path { get; set; }

    /// <summary>Skip this many matching ancestors before binding (0 = nearest). Clearer than WPF's 1-based AncestorLevel.</summary>
    public int Skip { get; set; }

    /// <summary>Optional: only match an ancestor whose x:Name equals this.</summary>
    public string Name { get; set; }

    /// <summary>Optional search boundary: stop (and resolve to nothing) if an ancestor of this type is reached first.</summary>
    public Type Stop { get; set; }

    /// <summary>Walk the LOGICAL tree instead of the visual one. Default false (visual).</summary>
    public bool Logical { get; set; }

    public BindingMode Mode { get; set; } = BindingMode.OneWay;

    /// <summary>Optional converter applied to the source value (and back on a TwoWay write), like a {Binding} converter.</summary>
    public IValueConverter Converter { get; set; }

    public object ConverterParameter { get; set; }

    /// <summary>Value used when the ancestor / path can't be resolved (WPF FallbackValue).</summary>
    public object FallbackValue { get; set; }

    /// <summary>Value used when the resolved value is null (falls back to <see cref="FallbackValue"/> if unset).</summary>
    public object TargetNullValue { get; set; }

    /// <summary>Creates the live expression on <paramref name="target"/>'s <paramref name="propertyName"/> and registers
    /// it so it re-establishes on tree changes. Mirrors <c>ThemeResource.Apply</c> / the codegen emission for bindings.</summary>
    public AncestorBindingExpression Apply(IAdamantiumComponent target, string propertyName,
        ValuePriority priority = ValuePriority.Binding)
    {
        var expression = new AncestorBindingExpression(target, target.GetProperty(propertyName), this) { Priority = priority };
        BindingEngine.Register(expression);
        return expression;
    }

    public override object ProvideObject(MarkupContext context)
    {
        if (context?.TargetObject is IAdamantiumComponent target && !string.IsNullOrEmpty(context.TargetPropertyName))
            Apply(target, context.TargetPropertyName);
        return this;
    }
}
