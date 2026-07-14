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
    // A SNAPSHOT of the shaped text taken when this payload is built. The TextBlock reuses ONE TextLayout instance and
    // re-shapes it in place, so the old and new payloads share the same TextLayout reference - a reference (or any
    // TextLayout-property) comparison can't see the change. The immutable string snapshot can: it differs whenever a
    // recycled container is rebound to another item, even one whose text is the same length (same DesiredSize). Without
    // this, such a rebind never rebuilt the glyph buffer -> the GPU kept drawing the previous item's text (jumbled list).
    public string Text { get; } = textLayout?.Text;

    // The LIVE brushes, read through their immutable snapshots - see RectanglePayload.
    private readonly Brush _foreground = foreground?.ForRendering();
    private readonly Brush _background = background?.ForRendering();
    private readonly Brush _stroke = stroke?.ForRendering();

    public Brush Foreground => _foreground?.Snapshot;
    public Brush Background => _background?.Snapshot;
    public Brush Stroke => _stroke?.Snapshot;

    public override int GetHashCode()
    {
        return HashCode.Combine(TextRenderingParameters, DesiredSize, TextLayout, Foreground, Background, Stroke);
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not TextPayload payload) return true;

        return DesiredSize != payload.DesiredSize || Text != payload.Text || TextLayout != payload.TextLayout ||
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