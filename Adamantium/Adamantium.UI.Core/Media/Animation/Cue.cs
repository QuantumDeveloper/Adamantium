using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// A keyframe's position within an iteration, normalised to 0..1 (0 = start, 1 = end). In markup it is written either
/// as a fraction (<c>Cue="0.5"</c>) or a percentage (<c>Cue="50%"</c>); in code a plain <see cref="double"/> converts
/// implicitly.
/// </summary>
[TypeParser(typeof(CueParser))]
public readonly struct Cue
{
    public Cue(double value) => Value = value;

    /// <summary>Position in 0..1.</summary>
    public double Value { get; }

    public static implicit operator Cue(double value) => new(value);
}
