namespace Adamantium.Multiverse.Input;

/// <summary>
/// One control of one device: a key, a mouse button or axis, a gamepad button or axis. Written as text it reads
/// "Key:W", "Mouse:Right", "Mouse:Wheel", "Gamepad:A", "Gamepad:LeftStickX".
/// </summary>
public readonly record struct InputControl(InputControlKind Kind, uint Code)
{
    private const string KeyDevice = "Key";
    private const string MouseDevice = "Mouse";
    private const string GamepadDevice = "Gamepad";

    public static InputControl Key(Keys key)
    {
        return new InputControl(InputControlKind.Key, (uint)key);
    }

    public static InputControl Mouse(MouseButton button)
    {
        return new InputControl(InputControlKind.MouseButton, (uint)button);
    }

    public static InputControl Mouse(MouseAxis axis)
    {
        return new InputControl(InputControlKind.MouseAxis, (uint)axis);
    }

    public static InputControl Gamepad(GamepadButton button)
    {
        return new InputControl(InputControlKind.GamepadButton, (uint)button);
    }

    public static InputControl Gamepad(GamepadAxis axis)
    {
        return new InputControl(InputControlKind.GamepadAxis, (uint)axis);
    }

    /// <summary>Whether the value stays within -1..1. Mouse motion and wheel do not.</summary>
    public bool IsBounded => Kind != InputControlKind.MouseAxis;

    public override string ToString()
    {
        return Kind switch
        {
            InputControlKind.Key => $"{KeyDevice}:{(Keys)Code}",
            InputControlKind.MouseButton => $"{MouseDevice}:{(MouseButton)Code}",
            InputControlKind.MouseAxis => $"{MouseDevice}:{(MouseAxis)Code}",
            InputControlKind.GamepadButton => $"{GamepadDevice}:{(GamepadButton)Code}",
            InputControlKind.GamepadAxis => $"{GamepadDevice}:{(GamepadAxis)Code}",
            _ => string.Empty
        };
    }

    /// <summary>Reads the text <see cref="ToString"/> writes.</summary>
    /// <exception cref="FormatException">The text names no control.</exception>
    public static InputControl Parse(string text)
    {
        if (TryParse(text, out var control))
        {
            return control;
        }

        throw new FormatException($"'{text}' is not an input control: expected Key:W, Mouse:Right, Gamepad:A and the like.");
    }

    public static bool TryParse(string text, out InputControl control)
    {
        control = default;
        var separator = text?.IndexOf(':') ?? -1;
        if (separator <= 0)
        {
            return false;
        }

        var device = text[..separator];
        var name = text[(separator + 1)..];
        switch (device)
        {
            case KeyDevice when Enum.TryParse<Keys>(name, out var key) && Enum.IsDefined(key):
                control = Key(key);
                return true;
            case MouseDevice when Enum.TryParse<MouseButton>(name, out var button) && Enum.IsDefined(button):
                control = Mouse(button);
                return true;
            case MouseDevice when Enum.TryParse<MouseAxis>(name, out var mouseAxis) && Enum.IsDefined(mouseAxis):
                control = Mouse(mouseAxis);
                return true;
            case GamepadDevice when Enum.TryParse<GamepadButton>(name, out var gamepadButton) &&
                                    Enum.IsDefined(gamepadButton) && gamepadButton != GamepadButton.None:
                control = Gamepad(gamepadButton);
                return true;
            case GamepadDevice when Enum.TryParse<GamepadAxis>(name, out var gamepadAxis) && Enum.IsDefined(gamepadAxis):
                control = Gamepad(gamepadAxis);
                return true;
            default:
                return false;
        }
    }
}
