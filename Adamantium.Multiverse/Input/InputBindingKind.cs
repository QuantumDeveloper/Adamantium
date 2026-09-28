namespace Adamantium.Multiverse.Input;

/// <summary>
/// How a binding makes a value of its controls.
/// </summary>
public enum InputBindingKind
{
    /// <summary>One control's value.</summary>
    Control,

    /// <summary>The positive control minus the negative one.</summary>
    Axis,

    /// <summary>Right minus left, up minus down.</summary>
    Vector,

    /// <summary>Two axes, X and Y.</summary>
    Stick
}
