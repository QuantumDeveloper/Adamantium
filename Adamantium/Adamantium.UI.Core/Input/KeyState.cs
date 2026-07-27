using System;

namespace Adamantium.UI.Core.Input;

/// <summary>Whether a key is held, and whether its lock is lit. Named per key, not per platform - every OS reports the
/// same two facts.</summary>
[Flags]
public enum KeyState
{
    Up = 0,
    Down = 1,
    Toggled = 2,
}
