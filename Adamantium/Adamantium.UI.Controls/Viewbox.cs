using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>Scales its single <see cref="Decorator.Child"/> to fit the space it is given, honouring <see cref="Stretch"/>
/// and <see cref="StretchDirection"/> - the analog of WPF's Viewbox. The child is measured at its NATURAL (unconstrained)
/// size and drawn through a scale transform, so vector content stays crisp at any size. The Viewbox manages the child's
/// <see cref="UIComponent.RenderTransform"/> (as the content-transition/tab controls do), so don't set your own on the
/// direct child - wrap it if you need one.</summary>
public class Viewbox : Decorator
{
    public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
        typeof(Stretch), typeof(Viewbox), new PropertyMetadata(Stretch.Uniform,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty StretchDirectionProperty = AdamantiumProperty.Register(nameof(StretchDirection),
        typeof(StretchDirection), typeof(Viewbox), new PropertyMetadata(StretchDirection.Both,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    private readonly Transform _scale = new();   // one reused transform - avoids allocating (and re-promoting) each arrange

    /// <summary>How the child is resized to fill the Viewbox. Default <see cref="Stretch.Uniform"/>.</summary>
    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Whether the child may be scaled up, down, or both. Default <see cref="StretchDirection.Both"/>.</summary>
    public StretchDirection StretchDirection
    {
        get => GetValue<StretchDirection>(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child == null) return new Size(0, 0);

        // Measure the child unconstrained for its natural size, then report the size it will occupy once scaled to fit.
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = child.DesiredSize;
        var (sx, sy) = ComputeScale(availableSize, natural);
        return new Size(natural.Width * sx, natural.Height * sy);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child == null) return finalSize;

        var natural = child.DesiredSize;
        var (sx, sy) = ComputeScale(finalSize, natural);

        // Lay the child out at its natural size at the origin, and scale it there (top-left origin) via the render transform.
        if (!ReferenceEquals(child.RenderTransform, _scale)) child.RenderTransform = _scale;
        child.RenderTransformOrigin = default;   // (0,0): scale about the top-left, so the child grows into the slot
        _scale.ScaleX = sx;
        _scale.ScaleY = sy;
        child.Arrange(new Rect(new Size(natural.Width, natural.Height)));

        return new Size(natural.Width * sx, natural.Height * sy);
    }

    // The per-axis scale that maps the content size into the available size for the current Stretch, then clamped by
    // StretchDirection. Mirrors Image.CalculateScaling (WPF's Viewbox.ComputeScaleFactor).
    private (double X, double Y) ComputeScale(Size available, Size content)
    {
        var scaleX = 1.0;
        var scaleY = 1.0;

        var hasWidth = !double.IsPositiveInfinity(available.Width);
        var hasHeight = !double.IsPositiveInfinity(available.Height);

        if (Stretch != Stretch.None && (hasWidth || hasHeight))
        {
            scaleX = content.Width == 0 ? 0.0 : available.Width / content.Width;
            scaleY = content.Height == 0 ? 0.0 : available.Height / content.Height;

            if (!hasWidth) scaleX = scaleY;            // an unconstrained axis follows the constrained one (keep aspect)
            else if (!hasHeight) scaleY = scaleX;
            else
            {
                switch (Stretch)
                {
                    case Stretch.Uniform: scaleX = scaleY = Math.Min(scaleX, scaleY); break;
                    case Stretch.UniformToFill: scaleX = scaleY = Math.Max(scaleX, scaleY); break;
                    case Stretch.Fill: break;   // independent x/y - fills, aspect not preserved
                }
            }

            switch (StretchDirection)
            {
                case StretchDirection.UpOnly: scaleX = Math.Max(1.0, scaleX); scaleY = Math.Max(1.0, scaleY); break;
                case StretchDirection.DownOnly: scaleX = Math.Min(1.0, scaleX); scaleY = Math.Min(1.0, scaleY); break;
                case StretchDirection.None: scaleX = scaleY = 1.0; break;
                case StretchDirection.Both: break;
            }
        }

        return (scaleX, scaleY);
    }
}
