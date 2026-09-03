using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>
/// One soft band around (or inside) a shape's outline, as the renderer wants it: an offset, how far it stays at full
/// strength, how far it fades over, and a colour. Both an <see cref="Aura"/> and a <see cref="Shadow"/> bake down to
/// this - the two are different WORDS for the author, and the same arithmetic underneath.
/// <para>An immutable value, captured on the RECORD thread. The renderer must never read the live
/// <see cref="Aura"/>/<see cref="Shadow"/> objects: those are edited from the loop thread, and reading them at draw
/// time is the same seam that once froze brushes and geometry at record time.</para>
/// </summary>
public readonly struct HaloBand
{
    public HaloBand(Vector2F offset, float spread, float softness, Vector4F color, bool inner)
    {
        Offset = offset;
        Spread = spread;
        Softness = softness;
        Color = color;
        Inner = inner;
    }

    /// <summary>Where the band is thrown, in logical pixels. Zero for an aura, which has no direction.</summary>
    public Vector2F Offset { get; }

    /// <summary>Pixels of FULL strength past the outline before the fade starts.</summary>
    public float Spread { get; }

    /// <summary>Pixels the band fades out over. Zero would be a hard-edged silhouette, so the bake keeps it above zero.</summary>
    public float Softness { get; }

    /// <summary>Straight-alpha RGBA with the author's Opacity already folded into .w.</summary>
    public Vector4F Color { get; }

    /// <summary>Band drawn INSIDE the outline instead of outside it.</summary>
    public bool Inner { get; }

    /// <summary>How far past the outline this band reaches - what the drawn quad has to be grown by to hold it.</summary>
    public float Reach => Spread + Softness + System.Math.Max(System.Math.Abs(Offset.X), System.Math.Abs(Offset.Y));

    /// <summary>No band at all - one with no width to it. Note what this does NOT ask: whether it is currently
    /// TRANSPARENT.
    /// <para>A band's alpha decides whether it paints; its geometry decides whether it EXISTS. A transparent band still
    /// occupies a record, and that is the whole point of the distinction: a glow switched on by a trigger would
    /// otherwise be a change in how many records the element owns, and a patch can only rewrite records that are
    /// already there - so switching it on cost the frame a walk of the scene, or, where the element wore no other band,
    /// silently did not draw at all. Held as a record from the start, the same switch is an ordinary recolour.</para>
    /// <para>The cost is one instance per element that OWNS an aura or a shadow while it is invisible, which is a set
    /// that already pays for the ones it shows; an element with neither carries no bands at all and is untouched.</para>
    /// </summary>
    public bool IsEmpty => (Spread + Softness) <= 0.0f;
}
