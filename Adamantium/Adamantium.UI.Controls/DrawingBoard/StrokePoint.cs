using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One point of a stroke: where the pen was, and how hard it was pressed.
/// <para>Pressure is here from the start and is 1 until something supplies it - there is no stylus in the engine yet, so
/// nothing does. Recorded as a known gap rather than left out: adding a field to every point of every stroke afterwards
/// is a different job from having had it.</para></summary>
public readonly struct StrokePoint(Vector2F at, float pressure = 1f)
{
    /// <summary>Where the pen was, as an offset from the stroke's own <see cref="StrokeItem.Origin"/>.</summary>
    public Vector2F At { get; } = at;

    public float Pressure { get; } = pressure;
}
