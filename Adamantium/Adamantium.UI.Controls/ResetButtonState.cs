namespace Adamantium.UI.Controls;

/// <summary>When a <see cref="PropertyRow"/> offers its reset button - the one that puts a value back to what it was
/// before anybody touched it.</summary>
public enum ResetButtonState
{
    /// <summary>Always on the line, and dim until there is something to put back. The room it takes is the same either
    /// way, so a line does not change shape the moment a value stops being the default - which is what made a second
    /// press of a stepper land on a button that had just arrived under the hand. A button that is there and dim also
    /// says what it would do and why it cannot yet, where a blank gap says nothing.</summary>
    Always,

    /// <summary>Only on a line that holds something other than its default. Tidier - an untouched panel carries no
    /// buttons at all - at the cost of the line moving as it is used.</summary>
    WhenModified,

    /// <summary>Never. For a panel where putting a value back is not on offer, or is offered somewhere else.</summary>
    Never
}
