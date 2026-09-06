using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Ticks the one toggle whose ConverterParameter matches the bound enum value, and sets that value back when it
/// is ticked - which is how a ROW OF BUTTONS drives a single choice: every button binds the same property and names its
/// own value. Works for any enum, by name.
/// <para>Un-ticking sets nothing. One of the choices is always in force, so a button that turned itself off would leave
/// the property holding a value no button shows.</para></summary>
public class EnumToBoolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value != null
        && parameter is string wanted
        && string.Equals(value.ToString(), wanted, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string wanted) return AdamantiumProperty.UnsetValue;

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return enumType.IsEnum && Enum.TryParse(enumType, wanted, true, out var parsed)
            ? parsed
            : AdamantiumProperty.UnsetValue;
    }
}
