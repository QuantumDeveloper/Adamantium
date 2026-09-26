namespace Adamantium.Multiverse.Input;

/// <summary>
/// Whose symbols are printed on a gamepad's face buttons, for showing the player the right prompt.
/// </summary>
public enum GamepadFace
{
    /// <summary>Not known: show the button names.</summary>
    Unknown,

    /// <summary>A, B, X, Y.</summary>
    Xbox,

    /// <summary>Cross, circle, square, triangle.</summary>
    PlayStation,

    /// <summary>B, A, Y, X - Nintendo's letters, each on the place of the other Xbox letter.</summary>
    Nintendo
}
