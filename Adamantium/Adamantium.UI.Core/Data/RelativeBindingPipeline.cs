using System;
using System.Globalization;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Shared value plumbing for the relative-source bindings (<c>{Ancestor}</c> / <c>{Self}</c>): dotted-path walking off a
/// resolved root, then converter -> TargetNullValue/FallbackValue -> target-type coercion. Kept in one place so both
/// expressions behave like <see cref="BindingExpression"/> (same fallback semantics) without duplicating the logic.
/// </summary>
internal static class RelativeBindingPipeline
{
    /// <summary>Sentinel for "the path could not be resolved" (distinct from a resolved null).</summary>
    internal static readonly object Unset = new();

    /// <summary>Walk a dotted path off <paramref name="root"/>. The first segment is read from
    /// <paramref name="firstProperty"/> (the root's AdamantiumProperty, when it has one) else by reflection; the rest by
    /// reflection. Returns <see cref="Unset"/> if any hop is null or missing.</summary>
    internal static object Walk(IAdamantiumComponent root, AdamantiumProperty firstProperty, string[] segments)
    {
        if (root == null || segments.Length == 0) return Unset;

        object current = firstProperty != null
            ? root.GetValue(firstProperty)
            : root.GetType().GetProperty(segments[0])?.GetValue(root);

        for (var i = 1; i < segments.Length; i++)
        {
            if (current == null) return Unset;
            var pi = current.GetType().GetProperty(segments[i]);
            if (pi == null) return Unset;
            current = pi.GetValue(current);
        }
        return current;
    }

    /// <summary>The object that OWNS the leaf property of a dotted path (root for a single segment, else the last hop),
    /// so a caller can observe its INotifyPropertyChanged. Returns null if the chain breaks before the leaf.</summary>
    internal static object LeafOwner(IAdamantiumComponent root, AdamantiumProperty firstProperty, string[] segments)
    {
        if (segments.Length <= 1) return root;
        object current = firstProperty != null
            ? root.GetValue(firstProperty)
            : root.GetType().GetProperty(segments[0])?.GetValue(root);
        for (var i = 1; i < segments.Length - 1 && current != null; i++)
            current = current.GetType().GetProperty(segments[i])?.GetValue(current);
        return current;
    }

    /// <summary>Converter -> TargetNullValue/FallbackValue -> coerce. Returns <see cref="Unset"/> when nothing usable can
    /// be produced (the caller then leaves the target at its default rather than clobbering it).</summary>
    internal static object Produce(object raw, IValueConverter converter, object converterParameter, Type targetType,
        object fallback, object targetNullValue)
    {
        object value;
        if (ReferenceEquals(raw, Unset))
        {
            value = fallback;   // path didn't resolve -> WPF FallbackValue semantics
        }
        else
        {
            value = converter != null
                ? converter.Convert(raw, targetType, converterParameter, CultureInfo.CurrentCulture)
                : raw;
        }

        if (value == null) value = targetNullValue ?? fallback;
        if (value == null) return Unset;
        return TryCoerce(value, targetType, out var coerced) ? coerced : Unset;
    }

    /// <summary>Best-effort back-conversion for a TwoWay write.</summary>
    internal static object ConvertBack(object value, IValueConverter converter, object converterParameter, Type sourceType)
        => converter != null
            ? converter.ConvertBack(value, sourceType, converterParameter, CultureInfo.CurrentCulture)
            : value;

    // Pass-through when assignable, ToString for a string target, else Convert.ChangeType; false when it can't fit (the
    // caller then skips the assignment rather than push an incompatible value). Mirrors BindingExpression.TryCoerce.
    internal static bool TryCoerce(object value, Type targetType, out object result)
    {
        result = value;
        if (value == null || targetType == null || targetType.IsInstanceOfType(value)) return true;
        if (targetType == typeof(string)) { result = value.ToString(); return true; }
        try
        {
            result = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.CurrentCulture);
            return true;
        }
        catch { result = null; return false; }
    }
}
