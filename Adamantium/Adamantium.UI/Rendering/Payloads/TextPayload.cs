using System;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class TextPayload(
    TextRenderingParameters renderingParameters,
    Size desiredSize,
    TextLayout textLayout,
    Brush foreground,
    Brush background,
    Brush stroke) : IEquatable<TextPayload>, IRenderCachePolicy
{
    public TextRenderingParameters TextRenderingParameters { get; } = renderingParameters;
    public Size DesiredSize { get; } = desiredSize;
    public TextLayout TextLayout { get; } = textLayout;
    public Brush Foreground { get; } = foreground;
    public Brush Background { get; } = background;
    public Brush Stroke { get; } = stroke;

    public override int GetHashCode()
    {
        return HashCode.Combine(TextRenderingParameters, DesiredSize, TextLayout, Foreground, Background, Stroke);
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not TextPayload payload) return true;

        return DesiredSize != payload.DesiredSize || TextLayout != payload.TextLayout ||
               TextRenderingParameters != payload.TextRenderingParameters;
    }

    public bool Equals(TextPayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(TextRenderingParameters, other.TextRenderingParameters) &&
               DesiredSize.Equals(other.DesiredSize) && Equals(TextLayout, other.TextLayout) &&
               Equals(Foreground, other.Foreground) && Equals(Background, other.Background) &&
               Equals(Stroke, other.Stroke);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TextPayload)obj);
    }
}