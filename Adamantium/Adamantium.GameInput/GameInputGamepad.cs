using Adamantium.GameInput.Interop;
using Adamantium.Mathematics;
using Adamantium.Multiverse.Input;
using EngineGamepad = Adamantium.Multiverse.Input.Gamepad;
using EngineState = Adamantium.Multiverse.Input.GamepadState;
using NativeState = Adamantium.GameInput.Interop.GameInputGamepadState;

namespace Adamantium.GameInput;

internal sealed unsafe class GameInputGamepad : EngineGamepad
{
    private static readonly (GameInputGamepadButtons Native, GamepadButton Engine)[] Buttons =
    [
        (GameInputGamepadButtons.A, GamepadButton.A),
        (GameInputGamepadButtons.B, GamepadButton.B),
        (GameInputGamepadButtons.X, GamepadButton.X),
        (GameInputGamepadButtons.Y, GamepadButton.Y),
        (GameInputGamepadButtons.LeftShoulder, GamepadButton.LeftShoulder),
        (GameInputGamepadButtons.RightShoulder, GamepadButton.RightShoulder),
        (GameInputGamepadButtons.View, GamepadButton.Back),
        (GameInputGamepadButtons.Menu, GamepadButton.Start),
        (GameInputGamepadButtons.LeftThumbstick, GamepadButton.LeftThumb),
        (GameInputGamepadButtons.RightThumbstick, GamepadButton.RightThumb),
        (GameInputGamepadButtons.DpadUp, GamepadButton.DpadUp),
        (GameInputGamepadButtons.DpadDown, GamepadButton.DpadDown),
        (GameInputGamepadButtons.DpadLeft, GamepadButton.DpadLeft),
        (GameInputGamepadButtons.DpadRight, GamepadButton.DpadRight),
        (GameInputGamepadButtons.PaddleRight1, GamepadButton.Paddle1),
        (GameInputGamepadButtons.PaddleRight2, GamepadButton.Paddle2),
        (GameInputGamepadButtons.PaddleLeft1, GamepadButton.Paddle3),
        (GameInputGamepadButtons.PaddleLeft2, GamepadButton.Paddle4)
    ];

    private readonly object sync = new();
    private readonly GameInputContextT context;
    private readonly string name;
    private readonly GamepadFace face;
    private readonly GamepadButton supportedButtons;
    private GameInputDeviceT device;
    private float lowFrequency;
    private float highFrequency;
    private float leftTrigger;
    private float rightTrigger;

    public GameInputGamepad(GameInputContextT context, GameInputDeviceT device)
    {
        this.context = context;
        this.device = device;

        var wrapper = new GameInputDevice(device);
        name = wrapper.DeviceName() ?? string.Empty;
        wrapper.DeviceInfo(out var info);
        face = ToFace(info.ALabel);
        supportedButtons = ToEngine(info.SupportedButtons) | ToEngine(info.SupportedSystemButtons);
    }

    public nint Handle => (nint)device.pointer;

    public override string Name => name;

    public override GamepadFace Face => face;

    public override GamepadButton SupportedButtons => supportedButtons;

    public override EngineState GetState()
    {
        lock (sync)
        {
            if (device.pointer == null)
            {
                return default;
            }

            NativeState reading = default;
            if (GameInputInterop.gameinputc_read_gamepad(context, device, &reading) == 0)
            {
                return new EngineState { IsConnected = true };
            }

            return new EngineState
            {
                IsConnected = true,
                Buttons = ToEngine(reading.buttons) | ToEngine(GameInputInterop.gameinputc_system_buttons(device)),
                LeftThumb = new Vector2F(reading.leftThumbstickX, reading.leftThumbstickY),
                RightThumb = new Vector2F(reading.rightThumbstickX, reading.rightThumbstickY),
                LeftTrigger = reading.leftTrigger,
                RightTrigger = reading.rightTrigger
            };
        }
    }

    public override void SetVibration(float lowFrequency, float highFrequency)
    {
        lock (sync)
        {
            this.lowFrequency = lowFrequency;
            this.highFrequency = highFrequency;
            Rumble();
        }
    }

    public override void SetTriggerVibration(float left, float right)
    {
        lock (sync)
        {
            leftTrigger = left;
            rightTrigger = right;
            Rumble();
        }
    }

    public void Release()
    {
        lock (sync)
        {
            if (device.pointer == null)
            {
                return;
            }

            GameInputInterop.gameinputc_device_release(device);
            device = default;
        }
    }

    internal static GamepadButton ToEngine(GameInputGamepadButtons buttons)
    {
        var result = GamepadButton.None;
        foreach (var (native, engine) in Buttons)
        {
            if ((buttons & native) != 0)
            {
                result |= engine;
            }
        }

        return result;
    }

    internal static GamepadButton ToEngine(GameInputSystemButtons buttons)
    {
        var result = GamepadButton.None;
        if ((buttons & GameInputSystemButtons.Share) != 0)
        {
            result |= GamepadButton.Share;
        }

        if ((buttons & GameInputSystemButtons.Guide) != 0)
        {
            result |= GamepadButton.Guide;
        }

        return result;
    }

    internal static GamepadFace ToFace(GameInputLabel bottomButton)
    {
        return bottomButton switch
        {
            GameInputLabel.XboxA or GameInputLabel.LetterA => GamepadFace.Xbox,
            GameInputLabel.IconCross => GamepadFace.PlayStation,
            GameInputLabel.LetterB => GamepadFace.Nintendo,
            _ => GamepadFace.Unknown
        };
    }

    private void Rumble()
    {
        if (device.pointer == null)
        {
            return;
        }

        GameInputInterop.gameinputc_set_rumble(device, Clamp(lowFrequency), Clamp(highFrequency), Clamp(leftTrigger),
            Clamp(rightTrigger));
    }

    private static float Clamp(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }
}
