using System;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Collections;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Clipping tab: what a container does to content that reaches past it.
///
/// <para>A scissor is a RECTANGLE, so a rounded container used to cut its content off squarely - the corner of a card
/// went sharp exactly where the card is round. The rounded part of the clip is done by the shaders instead, from a shape
/// carried in a transform-table slot, and this stand is where that is visible: the strip inside deliberately runs past
/// the container on every side, so every pixel of the cut is on show.</para></summary>
[ViewModel]
public partial class ClippingViewModel : TabPageViewModel
{
    public ClippingViewModel() : base("Clipping") { }

    [Bindable] private double _clipRadius = 28;
    [Bindable] private bool _clipOn = true;

    /// <summary>Clips by the container's OWN corners, or by an explicit radius of its own. The two answer different
    /// questions - "cut me the way I look" against "cut me like this" - and the second is the reason
    /// UIComponent.ClipCornerRadius exists rather than the renderer simply reading CornerRadius.</summary>
    [Bindable] private bool _clipRadiusExplicit;

    [Bindable] private double _explicitRadius = 8;

    /// <summary>The MESH case: a star has no closed form for a shader to clip against, so its rounded corners can only
    /// come from the clip slot. Sized to FIT the piece that carries it - a polygon draws at the coordinates it is given
    /// and does not scale into its slot, so points past the slot are drawn but sit outside the piece's bounds, which is
    /// what the mouse goes by: the star was there to see and dead to a press everywhere except its bounding box.</summary>
    public PointsCollection BigStar { get; } = Star(160, 140);

    // Ten points on a circle, alternating the full radius and the 0.382 of it that reads as a star; first point straight
    // up. One radius for both axes - scaling x and y apart turns a star into a splat - then fitted into the box.
    private static PointsCollection Star(double width, double height)
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
