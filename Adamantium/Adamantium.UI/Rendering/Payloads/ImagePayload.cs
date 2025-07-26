using System;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering.Payloads;

public class ImagePayload(Brush filter, ImageSource image, Rect destinationRect, CornerRadius cornerRadius) : 
    IEquatable<ImagePayload>, IRenderCachePolicy
{
    public ImageSource Image { get; } = image;
    public Brush Filter { get; } = filter;
    public Rect DestinationRect { get; } = destinationRect;
    public CornerRadius CornerRadius { get; } = cornerRadius;

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not ImagePayload payload) return true;
        
        return DestinationRect != payload.DestinationRect || CornerRadius != payload.CornerRadius;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Image, Filter, DestinationRect, CornerRadius);
    }

    public bool Equals(ImagePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Image, other.Image) && Equals(Filter, other.Filter) && DestinationRect.Equals(other.DestinationRect) && CornerRadius.Equals(other.CornerRadius);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ImagePayload)obj);
    }
}