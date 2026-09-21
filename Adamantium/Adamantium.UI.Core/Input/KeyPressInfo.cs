namespace Adamantium.UI.Core.Input;

/// <summary>
/// What the OS reported ALONGSIDE a key event: what the key was doing before, and how the press repeats. The platform
/// decodes this out of its own message format (on Windows, the bit-packed LPARAM of WM_KEYDOWN) so nothing above has to
/// know that format existed.
/// </summary>
public struct KeyPressInfo
{
    /// <summary>Was the key already down before this event? That, plus the key being down now, is what makes a repeat.</summary>
    public KeyState PreviousState { get; set; }

    public KeyState CurrentState { get; set; }

    /// <summary>When the key went down, on the same clock the raw event's timestamp uses (milliseconds since boot).</summary>
    public uint PressTime { get; set; }

    /// <summary>How many repeats the OS coalesced into this one event.</summary>
    public int RepeatCount { get; set; }

    public bool IsRepeated => PreviousState == KeyState.Down && CurrentState == KeyState.Down;
}
