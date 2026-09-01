using System;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Collections;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Opacity tab: does EVERY drawing family fade by the same amount?
///
/// <para>The engine fades a subtree by writing ONE number into a transform-table slot and letting each shader read it,
/// instead of re-baking every element's colour. A family whose pass does not read that slot has to carry the opacity
/// CHAIN in its baked colour instead, and the two are easy to get wrong in opposite directions: apply both and the
/// element fades twice, apply neither and it does not fade at all. Neither shows up on a single shape - only against a
/// neighbour that got it right.</para>
///
/// <para>Hence the instrument: the SAME strip of swatches twice, one inside a container this slider fades and one
/// beside it at full opacity. Every pair must move apart together; the family that fades too little (or too much) is
/// the one that reads its alpha differently from the rest. The clipping tab has an opacity slider too, but its pieces
/// are scattered and draggable - fine for corners, useless for comparing brightness.</para></summary>
[ViewModel]
public partial class OpacityViewModel : TabPageViewModel
{
    public OpacityViewModel() : base("Opacity") { }

    /// <summary>Fades the CONTAINER - an ancestor's opacity, which is the case that travels through the slot.</summary>
    [Bindable] private double _containerOpacity = 0.5;

    /// <summary>Fades one swatch inside that container ON TOP of the container's own fade. The product is what should
    /// reach the screen; a family that reads the slot AND keeps the chain in its colour shows here as too dark.</summary>
    [Bindable] private double _elementOpacity = 1;

    /// <summary>The MESH family's swatch - tessellated triangles rather than a shader-side shape, which is a different
    /// path to the same question. Sized to the swatch so the piece and its bounds agree.</summary>
    public PointsCollection Star { get; } = MakeStar(84, 56);

    // Ten points on a circle, alternating the full radius and the 0.382 that reads as a star, then fitted into the box.
    private static PointsCollection MakeStar(double width, double height)
    {
        const double innerRatio = 0.382;
        var unit = new Vector2[10];
        var min = new Vector2(double.MaxValue, double.MaxValue);
        var max = new Vector2(double.MinValue, double.MinValue);
        for (var i = 0; i < unit.Length; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var radius = i % 2 == 0 ? 1.0 : innerRatio;
            unit[i] = new Vector2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            min = Vector2.Min(min, unit[i]);
            max = Vector2.Max(max, unit[i]);
        }

        var scale = Math.Min(width / (max.X - min.X), height / (max.Y - min.Y));
        var points = new Vector2[unit.Length];
        for (var i = 0; i < unit.Length; i++)
        {
            points[i] = new Vector2((unit[i].X - min.X) * scale, (unit[i].Y - min.Y) * scale);
        }
        return new PointsCollection(points);
    }
}
