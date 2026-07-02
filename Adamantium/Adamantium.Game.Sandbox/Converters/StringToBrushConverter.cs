using System;
using System.Globalization;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Converts a colour string (e.g. "#3B82F6") into a <see cref="Brush"/> so a <c>{Binding}</c> whose source is a
/// string can target a Brush property such as <c>Border.Background</c>. A plain binding does no string-&gt;Brush type
/// conversion, so without this the bound tile stays unpainted. Uses the same <see cref="TypeParser"/> the AUML parser
/// uses for literal brush values, so bound and literal colours resolve identically.</summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? TypeParser.Parse<Brush>(s) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as SolidColorBrush)?.Color.ToString();
}
