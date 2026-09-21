using Adamantium.Core.Collections;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

/// <summary>One theme PROPERTY a variant sets - the accent seed, a focus stroke.
/// <code>&lt;ThemeValue Property="AccentColor" Value="#005FB8"/&gt;</code></summary>
/// <remarks>
/// Theme properties are a separate channel from the palette and have to be, because <c>{ThemeResource AccentColor}</c>
/// resolves against the THEME OBJECT rather than any dictionary. A variant able to set only palette colours would
/// leave a light theme wearing the dark theme's accent - which is precisely what the two theme files differ by today,
/// besides their palettes.
/// <para>Typed as a brush rather than as <c>object</c>: every theme property that differs between variants is one, and
/// a typed value is one the markup parser can read from <c>"#005FB8"</c> without ceremony. If a non-brush theme
/// property ever needs to vary, that is the moment to widen this - not before.</para>
/// </remarks>
public class ThemeValue
{
    public ThemeValue() { }

    public ThemeValue(string property, Brush value)
    {
        Property = property;
        Value = value;
    }

    /// <summary>The theme property's name, as registered - <c>AccentColor</c>.</summary>
    public string Property { get; set; }

    public Brush Value { get; set; }

    public override string ToString() => $"{Property} = {Value}";
}

/// <summary>The theme properties one variant sets.</summary>
[MarkupItem(ItemType = typeof(ThemeValue), ItemProperty = nameof(ThemeValue.Value))]
public class ThemeValueCollection : TrackingCollection<ThemeValue>
{
    /// <summary>Read or write by property name, so code says in one line what markup says with an element.</summary>
    public Brush this[string property]
    {
        get
        {
            foreach (var entry in this)
                if (string.Equals(entry.Property, property, System.StringComparison.OrdinalIgnoreCase)) return entry.Value;
            return null;
        }
        set
        {
            foreach (var entry in this)
            {
                if (!string.Equals(entry.Property, property, System.StringComparison.OrdinalIgnoreCase)) continue;
                entry.Value = value;
                return;
            }

            Add(new ThemeValue(property, value));
        }
    }
}
