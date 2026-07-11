using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Controls.Base;
using Adamantium.ProceduralGeometry;

namespace Adamantium.UI.Controls;

/// <summary>
/// One tile of a 3D flip-tile board (see <see cref="TilesHost"/>): a solid rounded FRONT that TILTS toward the pointer,
/// and on click FLIPS 180° around Y to reveal its normalised fragment (<see cref="SourceU"/>..<see cref="SourceVH"/>) of
/// a photo shared by the whole board. The tile is its own render MOTION NODE: its quad bakes ONCE in tile-local space
/// and every tilt/flip frame only rewrites its transform-table matrix (a 3D rotation with perspective) - the tile STAYS
/// in the instanced SDF batch while rotating, which an axis-aligned world bake could not do at all.
/// </summary>
public class FlipTile : Control
{
    public static readonly AdamantiumProperty FrontBrushProperty = AdamantiumProperty.Register(nameof(FrontBrush),
        typeof(Brush), typeof(FlipTile), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PhotoProperty = AdamantiumProperty.Register(nameof(Photo),
        typeof(ImageSource), typeof(FlipTile), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    // Normalised (0..1) fragment of the photo this tile shows on its back - four doubles so plain {Binding}s from the
    // tile item compose it (no multi-value converter needed).
    public static readonly AdamantiumProperty SourceUProperty = AdamantiumProperty.Register(nameof(SourceU),
        typeof(double), typeof(FlipTile), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsRender));
    public static readonly AdamantiumProperty SourceVProperty = AdamantiumProperty.Register(nameof(SourceV),
        typeof(double), typeof(FlipTile), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsRender));
    public static readonly AdamantiumProperty SourceUWProperty = AdamantiumProperty.Register(nameof(SourceUW),
        typeof(double), typeof(FlipTile), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsRender));
    public static readonly AdamantiumProperty SourceVHProperty = AdamantiumProperty.Register(nameof(SourceVH),
        typeof(double), typeof(FlipTile), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>Back side shown (flipped)? Toggled by a click; settable from <see cref="TilesHost"/>. The flip ANIMATES.</summary>
    public static readonly AdamantiumProperty IsFlippedProperty = AdamantiumProperty.Register(nameof(IsFlipped),
        typeof(bool), typeof(FlipTile), new PropertyMetadata(false, OnIsFlippedChanged));

    /// <summary>Extra delay (seconds) before the flip animation starts - a flip-all wave staggers tiles by index.</summary>
    public static readonly AdamantiumProperty FlipDelayProperty = AdamantiumProperty.Register(nameof(FlipDelay),
        typeof(double), typeof(FlipTile), new PropertyMetadata(0.0));

    private readonly Transform _transform;
    private bool _showBack;    // which face the CURRENT rotation shows (crosses at 90°/270°)
    private bool _clickFlip;   // this flip came from a direct click -> no wave stagger, flip NOW

    public FlipTile()
    {
        // Own motion node from birth: the whole point is per-tile matrices. RotationCenter follows the size (set
        // in OnRender); perspective gives the 3D depth look.
        IsRenderMotionNode = true;
        _transform = new Transform { Perspective = 900 };
        RenderTransform = _transform;
        _transform.PropertyChanged += OnTransformChanged;
    }

    public Brush FrontBrush { get => GetValue<Brush>(FrontBrushProperty); set => SetValue(FrontBrushProperty, value); }
    public ImageSource Photo { get => GetValue<ImageSource>(PhotoProperty); set => SetValue(PhotoProperty, value); }
    public double SourceU { get => GetValue<double>(SourceUProperty); set => SetValue(SourceUProperty, value); }
    public double SourceV { get => GetValue<double>(SourceVProperty); set => SetValue(SourceVProperty, value); }
    public double SourceUW { get => GetValue<double>(SourceUWProperty); set => SetValue(SourceUWProperty, value); }
    public double SourceVH { get => GetValue<double>(SourceVHProperty); set => SetValue(SourceVHProperty, value); }
    public bool IsFlipped { get => GetValue<bool>(IsFlippedProperty); set => SetValue(IsFlippedProperty, value); }
    public double FlipDelay { get => GetValue<double>(FlipDelayProperty); set => SetValue(FlipDelayProperty, value); }

    private static void OnIsFlippedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // The FIRST ever assignment arrives with OldValue = UnsetValue. Gating on that swallowed the first real flip
        // (a fresh tile's first click / the board's first sweep): the STATE flipped but no animation ran - the board
        // lagged one whole flip behind. Compare the EFFECTIVE old value instead (Unset reads as the default, false).
        var was = e.OldValue is bool oldFlipped && oldFlipped;
        var now = (bool)e.NewValue;
        if (a is FlipTile tile && was != now)
            tile.AnimateFlip(now);
    }

    private void AnimateFlip(bool flipped)
    {
        // FlipDelay is the board's WAVE stagger (set by TilesHost before a sweep); a direct click flips immediately.
        var delay = _clickFlip ? 0.0 : FlipDelay;
        _clickFlip = false;
        _transform.BeginAnimation(Transform.RotationYProperty, new DoubleAnimation
        {
            From = _transform.RotationY,
            To = flipped ? 180 : 0,
            Duration = TimeSpan.FromSeconds(0.45),
            Delay = TimeSpan.FromSeconds(delay),
            Easing = new CubicEasing()
        });
    }

    // Face swap at the 90°/270° silhouette (where the tile is edge-on): re-record this ONE tile's geometry (a cheap
    // in-place patch). The rotation itself never re-records - it only rewrites this tile's transform-table slot.
    private void OnTransformChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != Transform.RotationYProperty) return;
        var angle = ((_transform.RotationY % 360) + 360) % 360;
        var back = angle is > 90 and < 270;
        if (back == _showBack) return;
        _showBack = back;
        InvalidateRender(false);
    }

    /// <summary>Field tilt (driven by <see cref="TilesHost"/> - the board tilts EVERY tile toward the cursor, angle
    /// growing with distance): sets both rotations directly; each set rewrites only this tile's transform-table slot.
    /// Skipped while flipped/flipping so the field never fights the flip animation.</summary>
    public void SetFieldTilt(double rotationX, double rotationY)
    {
        if (IsFlipped || _showBack) return;
        _transform.RotationX = rotationX;
        _transform.RotationY = rotationY;
    }

    /// <summary>Ease the field tilt back to rest (the cursor left the board). FillBehavior.Stop RELEASES the properties
    /// when done - a held 0 at Animation priority would mask every later tilt write (the "tilts exactly once" bug).
    /// The LOCAL tilt values are zeroed FIRST (masked by the animation while it runs): Stop's release drops the
    /// property to its base value, and with the last tilt still there the tile eased to 0 then SNAPPED back tilted.</summary>
    public void EaseTiltBack()
    {
        if (IsFlipped || _showBack) return;
        var fromX = _transform.RotationX;
        var fromY = _transform.RotationY;
        _transform.RotationX = 0;
        _transform.RotationY = 0;
        _transform.BeginAnimation(Transform.RotationXProperty, new DoubleAnimation { From = fromX, To = 0, Duration = TimeSpan.FromSeconds(0.25), FillBehavior = FillBehavior.Stop });
        _transform.BeginAnimation(Transform.RotationYProperty, new DoubleAnimation { From = fromY, To = 0, Duration = TimeSpan.FromSeconds(0.25), FillBehavior = FillBehavior.Stop });
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        _clickFlip = true;
        IsFlipped = !IsFlipped;
    }

    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);
        var rect = new Rect(0, 0, RenderSize.Width, RenderSize.Height);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Keep the 3D pivot at the tile centre (cheap no-op sets once the size settles).
        _transform.RotationCenterX = rect.Width / 2;
        _transform.RotationCenterY = rect.Height / 2;

        var session = context.ForControl(this);
        var corners = new CornerRadius(6);
        // While the shared photo's ASYNC decode is still running, fall back to the front colour instead of an empty
        // quad (the render side skips a texture-less image; see BitmapSource.GetOrCreateTexture).
        var photoReady = Photo is not null && Photo is not BitmapImage { IsLoaded: false };
        if (_showBack && photoReady)
        {
            // The back is seen through a 180° rotation, so draw its fragment PRE-MIRRORED horizontally (negative UV
            // width) - the rotation mirrors it back and the photo reads correctly.
            var mirrored = new Rect(SourceU + SourceUW, SourceV, -SourceUW, SourceVH);
            session.DrawImage(Photo, null, rect, corners, mirrored);
        }
        else
        {
            session.DrawRectangle(FrontBrush ?? Brushes.SlateGray, rect, corners);
        }
    }
}
