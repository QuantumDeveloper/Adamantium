using Adamantium.Mathematics;

namespace Adamantium.Game.Input;

/// <summary>
/// Every connected gamepad, polled once per frame. A gamepad belongs to the machine, not to an output, so there is
/// one of these per platform and each output's input reads from it.
/// </summary>
public class GamepadHub
{
    private const int MaxGamepadsCount = 8;
    private const float LeftThumbDeadZone = 0.2f;
    private const float RightThumbDeadZone = 0.2f;

    private readonly HashSet<GamepadButton>[] downGamepadButtons;
    private readonly HashSet<GamepadButton>[] pressedGamepadButtons;
    private readonly HashSet<GamepadButton>[] releasedGamepadButtons;
    private readonly HashSet<GamepadButton>[] currentGamepadButtons;

    private readonly HashSet<GamepadButton> supportedGamepadButtons =
    [
        GamepadButton.A,
        GamepadButton.B,
        GamepadButton.X,
        GamepadButton.Y,
        GamepadButton.Back,
        GamepadButton.Start,
        GamepadButton.LeftThumb,
        GamepadButton.RightThumb,
        GamepadButton.LeftShoulder,
        GamepadButton.RightShoulder,
        GamepadButton.DpadLeft,
        GamepadButton.DpadRight,
        GamepadButton.DpadUp,
        GamepadButton.DpadDown
    ];

    private Gamepad[] gamepads;
    private GamepadState[] gamepadStates;

    public GamepadHub(IGamepadFactory gamepadFactory)
    {
        downGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
        pressedGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
        releasedGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
        currentGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];

        for (int i = 0; i < MaxGamepadsCount; i++)
        {
            downGamepadButtons[i] = [];
            pressedGamepadButtons[i] = [];
            releasedGamepadButtons[i] = [];
            currentGamepadButtons[i] = [];
        }

        gamepads = gamepadFactory.GetConnectedGamepads();
        gamepadStates = new GamepadState[MaxGamepadsCount];
    }

    public bool IsButtonDown(int gamepadIndex, GamepadButton button)
    {
        return downGamepadButtons[gamepadIndex].Contains(button);
    }

    public bool IsButtonPressed(int gamepadIndex, GamepadButton button)
    {
        return pressedGamepadButtons[gamepadIndex].Contains(button);
    }

    public bool IsButtonReleased(int gamepadIndex, GamepadButton button)
    {
        return releasedGamepadButtons[gamepadIndex].Contains(button);
    }

    public GamepadState GetState(int gamepadIndex)
    {
        return gamepadStates[gamepadIndex];
    }

    public void Update()
    {
        lock (gamepadStates)
        {
            for (int i = 0; i < MaxGamepadsCount; i++)
            {
                pressedGamepadButtons[i].Clear();
                releasedGamepadButtons[i].Clear();
                currentGamepadButtons[i].Clear();
                gamepadStates[i].IsConnected = false;
            }

            for (var i = 0; i < gamepads.Length; i++)
            {
                var gamepad = gamepads[i];
                var state = gamepad.GetState();
                ClampDeadZone(ref state);
                gamepadStates[i] = state;
            }

            for (int i = 0; i < gamepadStates.Length; ++i)
            {
                foreach (var supportedGamepadButton in supportedGamepadButtons)
                {
                    if (!gamepadStates[i].IsConnected)
                    {
                        continue;
                    }

                    var state = gamepadStates[i];
                    if (state.Buttons.HasFlag(supportedGamepadButton))
                    {
                        if (!downGamepadButtons[i].Contains(supportedGamepadButton))
                        {
                            downGamepadButtons[i].Add(supportedGamepadButton);
                            pressedGamepadButtons[i].Add(supportedGamepadButton);
                        }
                        currentGamepadButtons[i].Add(supportedGamepadButton);
                    }
                }

                foreach (var button in downGamepadButtons[i])
                {
                    if (!currentGamepadButtons[i].Contains(button))
                    {
                        releasedGamepadButtons[i].Add(button);
                    }
                }

                foreach (var button in releasedGamepadButtons[i])
                {
                    downGamepadButtons[i].Remove(button);
                }
            }
        }
    }

    private void ClampDeadZone(ref GamepadState state)
    {
        var leftThumbNormalizedX = Math.Max(-1, state.LeftThumb.X / short.MaxValue);
        var leftThumbNormalizedY = Math.Max(-1, state.LeftThumb.Y / short.MaxValue);

        var absLeftThumbNormalizedX = Math.Abs(leftThumbNormalizedX);
        var absLeftThumbNormalizedY = Math.Abs(leftThumbNormalizedY);

        var leftThumbX = absLeftThumbNormalizedX < LeftThumbDeadZone
            ? 0
            : (absLeftThumbNormalizedX - LeftThumbDeadZone) * (leftThumbNormalizedX / absLeftThumbNormalizedX);

        var leftThumbY = absLeftThumbNormalizedY < LeftThumbDeadZone
            ? 0
            : (absLeftThumbNormalizedY - LeftThumbDeadZone) * (leftThumbNormalizedY / absLeftThumbNormalizedY);

        var rightThumbNormalizedX = Math.Max(-1, state.RightThumb.X / short.MaxValue);
        var rightThumbNormalizedY = Math.Max(-1, state.RightThumb.Y / short.MaxValue);

        var absRightThumbNormalizedX = Math.Abs(rightThumbNormalizedX);
        var absRightThumbNormalizedY = Math.Abs(rightThumbNormalizedY);

        var rightThumbX = absRightThumbNormalizedX < RightThumbDeadZone
            ? 0
            : (absRightThumbNormalizedX - RightThumbDeadZone) * (rightThumbNormalizedX / absRightThumbNormalizedX);

        var rightThumbY = absRightThumbNormalizedY < RightThumbDeadZone
            ? 0
            : (absRightThumbNormalizedY - RightThumbDeadZone) * (rightThumbNormalizedY / absRightThumbNormalizedY);

        if (LeftThumbDeadZone > 0)
        {
            leftThumbX *= 1 / (1 - LeftThumbDeadZone);
            leftThumbY *= 1 / (1 - LeftThumbDeadZone);
        }

        if (RightThumbDeadZone > 0)
        {
            rightThumbX *= 1 / (1 - RightThumbDeadZone);
            rightThumbY *= 1 / (1 - RightThumbDeadZone);
        }

        state.LeftThumb = new Vector2F(leftThumbX, leftThumbY);
        state.RightThumb = new Vector2F(rightThumbX, rightThumbY);

        var normalizedLeftTrigger = state.LeftTrigger / 255;
        var normalizedRightTrigger = state.RightTrigger / 255;

        state.LeftTrigger = normalizedLeftTrigger;
        state.RightTrigger = normalizedRightTrigger;
    }
}
