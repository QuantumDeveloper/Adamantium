using System.Runtime.CompilerServices;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Central registry and entry point for data bindings - the WPF <c>BindingOperations</c> analog. Owns every active
/// <see cref="BindingExpression"/>, keyed by its target element (weakly, so it never keeps elements alive) and its
/// target property, so bindings can be inspected, refreshed or cleared programmatically rather than being buried as
/// private state inside each element.
/// </summary>
public static class BindingEngine
{
    // Weak element keys -> the element's live bindings by target property. Weak so the registry never pins an element.
    private static readonly ConditionalWeakTable<IFundamentalUIComponent, Dictionary<AdamantiumProperty, BindingExpressionBase>> _bindings = new();

    public static BindingExpressionBase SetBinding(IFundamentalUIComponent target, AdamantiumProperty targetProperty,
        BindingBase bindingBase)
    {
        var expression = BindingExpression.CreateBindingExpression(target, targetProperty, bindingBase);
        var map = _bindings.GetValue(target, static _ => new Dictionary<AdamantiumProperty, BindingExpressionBase>());
        if (map.TryGetValue(targetProperty, out var existing)) existing.CloseConnection();
        map[targetProperty] = expression;
        return expression;
    }

    public static BindingExpressionBase SetBinding(IFundamentalUIComponent target, string targetProperty,
        BindingBase bindingBase)
        => SetBinding(target, target.GetProperty(targetProperty), bindingBase);

    /// <summary>The live binding on a target property, or null.</summary>
    public static BindingExpressionBase GetBindingExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty)
        => _bindings.TryGetValue(target, out var map) && map.TryGetValue(targetProperty, out var e) ? e : null;

    /// <summary>Every live binding on an element.</summary>
    public static IReadOnlyCollection<BindingExpressionBase> GetBindings(IFundamentalUIComponent target)
        => _bindings.TryGetValue(target, out var map) ? map.Values.ToArray() : [];

    /// <summary>Re-resolves and re-applies every binding on an element - e.g. after its DataContext changed.</summary>
    public static void RefreshBindings(IFundamentalUIComponent target)
    {
        if (_bindings.TryGetValue(target, out var map))
            foreach (var e in map.Values) e.EstablishConnection();
    }

    public static void ClearBinding(IFundamentalUIComponent target, AdamantiumProperty targetProperty)
    {
        if (_bindings.TryGetValue(target, out var map) && map.Remove(targetProperty, out var e)) e.CloseConnection();
    }

    public static void ClearBindings(IFundamentalUIComponent target)
    {
        if (!_bindings.TryGetValue(target, out var map)) return;
        foreach (var e in map.Values) e.CloseConnection();
        map.Clear();
    }
}
