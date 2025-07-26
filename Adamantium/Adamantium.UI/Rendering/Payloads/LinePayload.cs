using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class LinePayload(Vector2 lineStart, Vector2 lineEnd, Pen pen) : IEquatable<LinePayload>, IRenderCachePolicy
{
    public Vector2 LineStart { get; } = lineStart;

    public Vector2 LineEnd { get; } = lineEnd;

    public Pen Pen { get; } = pen;

    public override int GetHashCode()
    {
        return HashCode.Combine(LineStart, LineEnd, Pen);
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        return GetHashCode() != newState.GetHashCode();
    }

    public bool Equals(LinePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return LineStart.Equals(other.LineStart) && LineEnd.Equals(other.LineEnd) && Equals(Pen, other.Pen);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((LinePayload)obj);
    }
}