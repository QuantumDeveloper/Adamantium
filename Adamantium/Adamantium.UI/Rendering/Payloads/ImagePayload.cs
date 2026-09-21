using System;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering.Payloads;

public class ImagePayload(Brush filter, ImageSource image, Rect destinationRect, CornerRadius cornerRadius, Rect? sourceUv = null, int? frameLayer = null) :
    IEquatable<ImagePayload>, IRenderCachePolicy
{
    // The LIVE brush, read through its immutable snapshot - see RectanglePayload.
    private readonly Brush _filter = filter?.ForRendering();

    public ImageSource Image { get; } = image;
    public Brush Filter => _filter?.Snapshot;
    public Rect DestinationRect { get; } = destinationRect;
    public CornerRadius CornerRadius { get; } = cornerRadius;

    /// <summary>Normalised (0..1) sub-rect of the image to sample - a mosaic tile shows just its fragment of one shared
    /// photo. Null = the whole image (the default, unchanged behaviour).</summary>
    public Rect? SourceUv { get; } = sourceUv;

    /// <summary>Which LAYER of the image's frame-array texture to sample, for an animation. Null = a plain single image.
    /// Advancing an animation changes only this, which is why it is deliberately absent from
    /// <see cref="RequiresBufferRebuild"/>: a new frame is one number handed to the shader, not new geometry.</summary>
    public int? FrameLayer { get; } = frameLayer;

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not ImagePayload payload) return true;

        // Rebuild when the image itself changes too, not just its rect/corner — an animated bitmap draws a different
        // frame (its own BitmapSource + texture) each tick, so without this the render unit keeps the first frame.
        return DestinationRect != payload.DestinationRect
               || CornerRadius != payload.CornerRadius
               || !Nullable.Equals(SourceUv, payload.SourceUv)
               // Gaining or losing a frame layer SWITCHES which texture is sampled - the frame array instead of the
               // single image - so the renderer has to be rebuilt. Only the layer's NUMBER is free to change; without
               // this the first frame (drawn before playback starts, hence layer-less) kept its single texture forever
               // and the animation stood still while the layer number kept advancing.
               || FrameLayer.HasValue != payload.FrameLayer.HasValue
               || !Equals(Image, payload.Image);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Image, Filter, DestinationRect, CornerRadius, SourceUv, FrameLayer);
    }

    public bool Equals(ImagePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Image, other.Image) && Equals(Filter, other.Filter) && DestinationRect.Equals(other.DestinationRect)
               && CornerRadius.Equals(other.CornerRadius) && Nullable.Equals(SourceUv, other.SourceUv)
               && Nullable.Equals(FrameLayer, other.FrameLayer);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ImagePayload)obj);
    }
}