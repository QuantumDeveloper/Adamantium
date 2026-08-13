namespace Adamantium.UI.Core.Media;

/// <summary>Where content sits HORIZONTALLY inside its tile when <see cref="Stretch"/> leaves room. Separate from
/// <see cref="HorizontalAlignment"/>, which also has a Stretch member - a brush states stretching on its own property,
/// so offering it twice could only contradict itself.</summary>
public enum AlignmentX
{
    Left,

    Center,

    Right
}
