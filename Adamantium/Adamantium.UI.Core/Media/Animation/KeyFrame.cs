using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// One stop of an <see cref="Animation"/>: the property values (as Setters) the animation should reach at
/// <see cref="Cue"/> - the position within an iteration, 0..1 (0 = start, 1 = end). Reuses <c>Setter</c>
/// (property name + value), so one keyframe can drive several properties at once.
/// </summary>
public class KeyFrame
{
    /// <summary>Position within an iteration, 0..1 ("0.5" or "50%" in markup; a double converts implicitly in code).</summary>
    public Cue Cue { get; set; }

    /// <summary>The values this keyframe sets (CSS @keyframes-style: <c>Property</c> = <c>Value</c>). Markup content,
    /// so setters are written directly inside &lt;KeyFrame&gt;.</summary>
    [Content]
    public SetterCollection Setters { get; } = new();
}
