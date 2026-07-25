using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A 4-corner BILINEAR (mesh) gradient: one colour per corner, smoothly blended across the fill. Beyond WPF
/// (which has no mesh gradient); evaluated per fragment in the SDF batch, so it stays crisp at any size. Reuses the
/// gradient batch - packed as gradient type 4 with the four corners in the first four stop slots; <see cref="GradientBrush"/>
/// stops / spread / interpolation are unused (the shader bilinearly blends the corners by the fragment's 0..1 uv).</summary>
public sealed class MeshGradientBrush : GradientBrush
{
    public MeshGradientBrush() { }

    public static readonly AdamantiumProperty TopLeftProperty = AdamantiumProperty.Register(nameof(TopLeft),
        typeof(Color), typeof(MeshGradientBrush), new PropertyMetadata(new Color(239, 68, 68, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty TopRightProperty = AdamantiumProperty.Register(nameof(TopRight),
        typeof(Color), typeof(MeshGradientBrush), new PropertyMetadata(new Color(59, 130, 246, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty BottomLeftProperty = AdamantiumProperty.Register(nameof(BottomLeft),
        typeof(Color), typeof(MeshGradientBrush), new PropertyMetadata(new Color(234, 179, 8, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty BottomRightProperty = AdamantiumProperty.Register(nameof(BottomRight),
        typeof(Color), typeof(MeshGradientBrush), new PropertyMetadata(new Color(34, 197, 94, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>Top-left corner colour.</summary>
    public Color TopLeft { get => GetValue<Color>(TopLeftProperty); set { if (IsFrozen) return; SetValue(TopLeftProperty, value); } }

    /// <summary>Top-right corner colour.</summary>
    public Color TopRight { get => GetValue<Color>(TopRightProperty); set { if (IsFrozen) return; SetValue(TopRightProperty, value); } }

    /// <summary>Bottom-left corner colour.</summary>
    public Color BottomLeft { get => GetValue<Color>(BottomLeftProperty); set { if (IsFrozen) return; SetValue(BottomLeftProperty, value); } }

    /// <summary>Bottom-right corner colour.</summary>
    public Color BottomRight { get => GetValue<Color>(BottomRightProperty); set { if (IsFrozen) return; SetValue(BottomRightProperty, value); } }

    protected override Brush CreateClone() =>
        new MeshGradientBrush { TopLeft = TopLeft, TopRight = TopRight, BottomLeft = BottomLeft, BottomRight = BottomRight, Opacity = Opacity };
}
