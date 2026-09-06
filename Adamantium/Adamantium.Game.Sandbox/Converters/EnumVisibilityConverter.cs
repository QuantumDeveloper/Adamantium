using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Shows the one element whose ConverterParameter matches the bound enum value, and collapses the rest. Works
/// for ANY enum, by name: a stand that holds a rectangle, an ellipse and a star reveals the selected figure, and the same
/// converter reveals the selected brush family's panel. Written once rather than once per enum - the shape-specific
/// version it replaced was about to be copied for families, which is how two mechanisms for one job start.
/// <para>View-layer logic in a converter, not in the view-model.</para></summary>
public class EnumVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var wanted = parameter as string;
        var visible = value != null
                      && !string.IsNullOrEmpty(wanted)
                      && string.Equals(value.ToString(), wanted, StringComparison.OrdinalIgnoreCase);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
