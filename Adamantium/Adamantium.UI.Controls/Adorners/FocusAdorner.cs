using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// The focus ring: what the keyboard puts around the control it reached. It draws NOTHING of its own - its whole look is
/// a <c>ControlTemplate</c> from the theme (<c>FocusAdornerStyleSet</c>), or the focused control's own
/// <see cref="InputUIComponent.FocusVisualStyle"/> where one control wants a different ring. Colour, thickness, corner
/// and how far out it sits are all set THERE, so restyling the focus visual of an application never means touching code.
/// <para>An adorner rather than a piece of every control template, because a template is a thing that can be forgotten -
/// and a control whose template forgot it would silently have no focus visual at all. One ring on the layer covers every
/// control, needs no template edits, and is drawn ABOVE the content, so a control that clips its own children still
/// shows it.</para>
/// </summary>
public class FocusAdorner : Adorner
{
    public FocusAdorner(UIComponent adornedElement) : base(adornedElement)
    {
        UpdateCornerRadius();
    }

    // The ring wraps the whole element, so the adorner stage themes it and sizes it to the adorned bounds every frame.
    public override bool FillsAdornedBounds => true;

    /// <summary>How far OUTSIDE the control the ring sits. Outside, not on the border: a ring drawn ON the chrome reads
    /// as the control changing colour rather than as a mark of where the keyboard is. The theme sets it.</summary>
    public static readonly AdamantiumProperty OutsetProperty = AdamantiumProperty.Register(nameof(Outset),
        typeof(double), typeof(FocusAdorner), new PropertyMetadata(0.0, OnOutsetChanged));

    public double Outset
    {
        get => GetValue<double>(OutsetProperty);
        set => SetValue(OutsetProperty, value);
    }

    /// <summary>The ring stands off by exactly <see cref="Outset"/>, so that is what a viewport must let past its edge.</summary>
    public override double ClipStandoff => Outset;

    /// <summary>The rounding for the ring: the adorned control's own, GROWN by the outset so the ring stays parallel to
    /// the edge it follows - a square ring around a rounded button reads as a second, badly aligned control. A ring
    /// template <c>{TemplateBinding}</c>s this instead of restating a radius that would then fight the control's own.
    /// <para>UNIFORM, taken from the control's largest corner: a mixed radius (a tab, rounded on top and square below)
    /// puts the border on the geometry path built for thick, uneven frames, whose anti-aliasing fringe is as wide as a
    /// 2px ring is thick - the ring then comes out ragged along the rounded side. One radius keeps it on the analytic
    /// path, where the whole ring is reconstructed in a single instanced draw.</para></summary>
    public static readonly AdamantiumProperty AdornedCornerRadiusProperty = AdamantiumProperty.Register(
        nameof(AdornedCornerRadius), typeof(CornerRadius), typeof(FocusAdorner), new PropertyMetadata(default(CornerRadius)));

    public CornerRadius AdornedCornerRadius
    {
        get => GetValue<CornerRadius>(AdornedCornerRadiusProperty);
        private set => SetValue(AdornedCornerRadiusProperty, value);
    }

    // The radius follows the outset, so it is recomputed when the theme sets one - the template is applied and bound
    // before that setter runs, and a binding is exactly what carries the later value across.
    private static void OnOutsetChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
        ((FocusAdorner)a).UpdateCornerRadius();

    private void UpdateCornerRadius()
    {
        var radius = AdornedElement is Control control ? control.CornerRadius : default;

        // Corner FOR corner, not one radius for all four: a tab is round on top and square where it meets its strip,
        // and a ring that rounds the bottom too curves away into open space where the control has a straight edge.
        AdornedCornerRadius = new CornerRadius(
            Grow(radius.TopLeft), Grow(radius.TopRight), Grow(radius.BottomRight), Grow(radius.BottomLeft));
    }

    // A square corner stays square; a rounded one grows by exactly what the ring stands off by.
    private double Grow(double radius) => radius <= 0 ? 0 : radius + Outset;

    /// <summary>The ring's box: the control's painted bounds pushed out by <see cref="Outset"/>. The stage lays the
    /// template out to this, so the template itself is a plain box that fills what it is given.</summary>
    public override Rect AdornedBounds
    {
        get
        {
            var bounds = base.AdornedBounds;
            return new Rect(bounds.X - Outset, bounds.Y - Outset,
                bounds.Width + Outset * 2, bounds.Height + Outset * 2);
        }
    }
}
