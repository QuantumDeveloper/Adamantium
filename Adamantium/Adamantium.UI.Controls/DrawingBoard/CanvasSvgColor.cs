using System;
using System.Globalization;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Reads the colors a drawing file states - the several spellings CSS allows, which is what an SVG carries.
/// <para>Null for one it cannot read, and that is deliberate: a color it does not understand is not black, and
/// guessing would repaint somebody's drawing while claiming to have opened it.</para></summary>
public static class CanvasSvgColor
{
    public static Color? Of(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) return null;

        var value = text.Trim();

        if (value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value[0] == '#') return Hex(value);

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return Channels(value);

        // A NAME. The engine's own table answers for every color CSS names, so there is no second list here to fall
        // behind it - but it answers by throwing, and a name out of somebody else's file is not an exceptional event.
        try
        {
            return Colors.Get(value);
        }
        catch (Exception e) when (e is System.Collections.Generic.KeyNotFoundException or FormatException
                                      or OverflowException)
        {
            return null;
        }
    }

    private static Color? Hex(string value)
    {
        var digits = value[1..];

        // THREE digits is each one doubled - #f00 is red, and read as-is it would be a very dark blue.
        if (digits.Length == 3 || digits.Length == 4)
        {
            var wide = String.Empty;

            foreach (var digit in digits) wide += new string(digit, 2);

            digits = wide;
        }

        if (digits.Length != 6 && digits.Length != 8) return null;

        if (!UInt32.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return null;
        }

        // #RRGGBBAA in CSS, which is the opposite end from the engine's ARGB.
        if (digits.Length == 8)
        {
            var alpha = packed & 0xFF;

            return Color.FromBgra(alpha << 24 | packed >> 8);
        }

        return Color.FromBgra(0xFF000000 | packed);
    }

    private static Color? Channels(string value)
    {
        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');

        if (open < 0 || close <= open) return null;

        var parts = value[(open + 1)..close]
            .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3) return null;

        var r = Channel(parts[0]);
        var g = Channel(parts[1]);
        var b = Channel(parts[2]);

        if (r < 0 || g < 0 || b < 0) return null;

        // The fourth is an ALPHA in 0..1, not a channel in 0..255 - the one place this spelling changes units halfway
        // through its own argument list.
        var a = 255.0;

        if (parts.Length > 3 &&
            Double.TryParse(parts[3].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha))
        {
            a = parts[3].EndsWith('%') ? alpha * 2.55 : alpha * 255;
        }

        return new Color((byte)r, (byte)g, (byte)b, (byte)Math.Clamp(a, 0, 255));
    }

    private static double Channel(string text)
    {
        var part = text.Trim();
        var percent = part.EndsWith('%');

        if (!Double.TryParse(percent ? part[..^1] : part, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var value))
        {
            return -1;
        }

        return Math.Clamp(percent ? value * 2.55 : value, 0, 255);
    }
}
