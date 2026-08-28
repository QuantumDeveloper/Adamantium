using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Resources;

/// <summary>What a palette entry is served AS.</summary>
public enum PaletteEntryKind
{
    /// <summary>A brush - what nearly everything painting with the palette wants.</summary>
    Brush,

    /// <summary>A raw colour. A gradient STOP takes a colour, not a brush, so the two edge-fade and two shimmer
    /// tokens have always been colours; served as brushes they simply would not resolve, and the surfaces that use
    /// them would paint nothing at all.</summary>
    Color,
}

/// <summary>One palette entry of a theme variant: the resource key, and the colour this variant gives it.
/// <code>&lt;PaletteColor Key="SolidBackgroundFillColorBase" Color="#202020"/&gt;</code>
/// <code>&lt;PaletteColor Key="EdgeFadeColor" Color="#E6161616" As="Color"/&gt;</code></summary>
public class PaletteColor
{
    public PaletteColor() { }

    public PaletteColor(string key, Color color, PaletteEntryKind kind = PaletteEntryKind.Brush)
    {
        Key = key;
        Color = color;
        As = kind;
    }

    public string Key { get; set; }

    public Color Color { get; set; }

    /// <summary>Brush by default, because that is what nearly every key is. Stated explicitly for the few that are
    /// consumed as colours - it cannot be inferred from the name, since <c>SolidBackgroundFillColorBase</c> is a brush
    /// and <c>EdgeFadeColor</c> is not.</summary>
    public PaletteEntryKind As { get; set; } = PaletteEntryKind.Brush;

    public override string ToString() => $"{Key} = {Color} ({As})";
}

/// <summary>A variant's palette, written in markup as child elements and read in code by key.
/// <para>One collection with an indexer rather than a collection AND a dictionary: the same fact in two containers is
/// the shape that produced the worst defect of the theme work so far (a brush's owners lived in a map and in an event's
/// subscriber list, and keeping the two in step cost an O(subscribers) walk per element).</para></summary>
[MarkupItem(ItemType = typeof(PaletteColor), ItemProperty = nameof(PaletteColor.Color))]
public class PaletteColorCollection : TrackingCollection<PaletteColor>
{
    /// <summary>Read or write an entry by key - <c>colors["Background"] = color</c> - so code needs no ceremony to say
    /// what markup says with an element.</summary>
    public Color this[string key]
    {
        get => TryGet(key, out var color) ? color : default;
        set
        {
            foreach (var entry in this)
            {
                if (!string.Equals(entry.Key, key, System.StringComparison.OrdinalIgnoreCase)) continue;
                entry.Color = value;
                return;
            }

            Add(new PaletteColor(key, value));
        }
    }

    public bool TryGet(string key, out Color color)
    {
        foreach (var entry in this)
        {
            if (!string.Equals(entry.Key, key, System.StringComparison.OrdinalIgnoreCase)) continue;
            color = entry.Color;
            return true;
        }

        color = default;
        return false;
    }

    public bool ContainsKey(string key) => TryGet(key, out _);

    public IEnumerable<string> Keys
    {
        get
        {
            foreach (var entry in this) yield return entry.Key;
        }
    }
}
