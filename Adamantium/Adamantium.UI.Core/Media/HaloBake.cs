using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>
/// Turns an element's <see cref="Aura"/> and <see cref="Shadow"/> into the bands the renderer draws. THE one place the
/// two public vocabularies meet the single internal one.
/// <para>Deliberately a LIST, though today it never holds more than two: what the renderer draws is "N bands", not "an
/// aura and a shadow". That is what leaves room for an elevation preset - one number expanding into the several bands a
/// real penumbra needs - without the public API ever growing a list for authors to hand-tune.</para>
/// </summary>
public static class HaloBake
{
    /// <summary>The bands for one element, or null when it wears none. Called on the RECORD thread.</summary>
    public static HaloBand[] From(Aura aura, Shadow shadow)
    {
        if (aura == null && shadow == null) return null;

        // SHADOW first, then aura - the order they are drawn in. A shadow falls on what lies BEHIND the element, while an
        // aura is light coming off the element itself, so the aura is the nearer of the two and belongs on top. Baked the
        // other way round, a dark shadow sits over the glow and eats it.
        List<HaloBand> bands = null;
        Add(ref bands, FromShadow(shadow));
        Add(ref bands, FromAura(aura));
        return bands?.ToArray();
    }

    private static void Add(ref List<HaloBand> bands, HaloBand band)
    {
        if (band.IsEmpty) return;
        bands ??= [];
        bands.Add(band);
    }

    // An aura has no direction, and its Radius is the whole reach - it is the SOFTNESS, with Spread the solid rim the
    // fade starts outside of. (A shadow splits the same distance differently, which is why they are baked apart.)
    private static HaloBand FromAura(Aura aura)
    {
        if (aura == null) return default;
        return new HaloBand(Vector2F.Zero, (float)System.Math.Max(0.0, aura.Spread),
            (float)System.Math.Max(0.0, aura.Radius), Premultiplied(aura.Color, aura.Opacity), aura.Inner);
    }

    // A shadow's Spread may be NEGATIVE - a shadow that only peeks out from under one edge - so it is not clamped the
    // way an aura's rim is; only the softness has to stay positive, or the band would be a hard silhouette.
    private static HaloBand FromShadow(Shadow shadow)
    {
        if (shadow == null) return default;
        return new HaloBand(new Vector2F((float)shadow.OffsetX, (float)shadow.OffsetY), (float)shadow.Spread,
            (float)System.Math.Max(0.0, shadow.BlurRadius), Premultiplied(shadow.Color, shadow.Opacity), shadow.Inner);
    }

    private static Vector4F Premultiplied(Color color, double opacity)
    {
        var c = color.ToVector4();
        c.W *= (float)System.Math.Clamp(opacity, 0.0, 1.0);
        return c;
    }
}
