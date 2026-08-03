namespace Adamantium.UI.Controls;

/// <summary>What a <see cref="NumericUpDown"/> accepts as it is typed.</summary>
public enum NumericInputMode
{
    /// <summary>Whole numbers: the decimal separator is refused at the keystroke, so a fraction never reaches the value.</summary>
    Integers,

    /// <summary>Fractions allowed.</summary>
    Decimals
}
