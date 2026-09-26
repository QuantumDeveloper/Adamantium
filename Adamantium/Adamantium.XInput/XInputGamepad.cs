using Adamantium.Mathematics;
using Adamantium.Multiverse.Input;
using EngineGamepad = Adamantium.Multiverse.Input.Gamepad;
using EngineButton = Adamantium.Multiverse.Input.GamepadButton;

namespace Adamantium.XInput;

internal sealed class XInputGamepad : EngineGamepad
{
    private static readonly (GamepadButton XInput, EngineButton Engine)[] Buttons =
    [
        (GamepadButton.A, EngineButton.A),
        (GamepadButton.B, EngineButton.B),
        (GamepadButton.X, EngineButton.X),
        (GamepadButton.Y, EngineButton.Y),
        (GamepadButton.LeftShoulder, EngineButton.LeftShoulder),
        (GamepadButton.RightShoulder, EngineButton.RightShoulder),
        (GamepadButton.Back, EngineButton.Back),
        (GamepadButton.Start, EngineButton.Start),
        (GamepadButton.LeftThumb, EngineButton.LeftThumb),
        (GamepadButton.RightThumb, EngineButton.RightThumb),
        (GamepadButton.DpadUp, EngineButton.DpadUp),
        (GamepadButton.DpadDown, EngineButton.DpadDown),
        (GamepadButton.DpadLeft, EngineButton.DpadLeft),
        (GamepadButton.DpadRight, EngineButton.DpadRight)
    ];

    public XInputGamepad(XBoxController controller)
    {
        Controller = controller;
    }

    public XBoxController Controller { get; }

    public bool IsConnected { get; set; }

    public override GamepadFace Face => GamepadFace.Xbox;

    public override GamepadState GetState()
    {
        return ToEngine(Controller.GetState().Gamepad);
    }

    public override void SetVibration(float lowFrequency, float highFrequency)
    {
        Controller.SetVibration(new Vibration
        {
            LeftMotorSpeed = ToMotorSpeed(lowFrequency),
            RightMotorSpeed = ToMotorSpeed(highFrequency)
        });
    }

    internal static GamepadState ToEngine(Gamepad reading)
    {
        return new GamepadState
        {
            IsConnected = true,
            Buttons = ToEngine(reading.Buttons),
            LeftThumb = new Vector2F(ToAxis(reading.LeftThumbX), ToAxis(reading.LeftThumbY)),
            RightThumb = new Vector2F(ToAxis(reading.RightThumbX), ToAxis(reading.RightThumbY)),
            LeftTrigger = reading.LeftTrigger / 255f,
            RightTrigger = reading.RightTrigger / 255f
        };
    }

    internal static EngineButton ToEngine(GamepadButton buttons)
    {
        var result = EngineButton.None;
        foreach (var (xinput, engine) in Buttons)
        {
            if ((buttons & xinput) != 0)
            {
                result |= engine;
            }
        }

        return result;
    }

    internal static float ToAxis(short value)
    {
        return value < 0 ? value / 32768f : value / 32767f;
    }

    internal static ushort ToMotorSpeed(float speed)
    {
        return (ushort)MathF.Round(Math.Clamp(speed, 0f, 1f) * ushort.MaxValue);
    }
}
