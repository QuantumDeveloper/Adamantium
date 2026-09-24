using Adamantium.Core;
using Adamantium.Mathematics;

namespace Adamantium.Game.Core.Input;

/// <summary>
/// Input of one <see cref="UniverseOutput"/>: its keyboard and pointer, fed by the output directly, plus the gamepads -
/// which only the active output reports.
/// </summary>
public class InputWormhole
{
    private readonly HashSet<Keys> downKeys = [];
    private readonly HashSet<Keys> pressedKeys = [];
    private readonly HashSet<Keys> releasedKeys = [];
    private readonly ButtonState[] mouseButtons = new ButtonState[5];

    private readonly UniverseOutput output;
    private readonly GamepadHub gamepads;

    private Vector2F absolutePosition;
    private bool isPointerHeld;
    private Vector2F rawMouseDelta;
    private int mouseWheelDelta;

    public InputWormhole(UniverseOutput output, GamepadHub gamepads)
    {
        this.output = output;
        this.gamepads = gamepads;
    }

    internal void OnKeyboardInput(KeyboardInput e)
    {
        lock (KeyboadInputs)
        {
            KeyboadInputs.Add(e);
        }
    }

    internal void OnMouseInput(MouseInput e)
    {
        lock (MouseInputs)
        {
            MouseInputs.Add(e);
        }
    }

    internal List<KeyboardInput> KeyboadInputs { get; } = [];

    internal List<MouseInput> MouseInputs { get; } = [];

    /// <summary>
    /// Whether the output takes the keyboard. Keys are still tracked while it is off, so none sticks when it comes back.
    /// </summary>
    public bool IsKeyboardEnabled { get; set; } = true;

    /// <summary>
    /// Whether the output takes the mouse: buttons, wheel and motion.
    /// </summary>
    public bool IsMouseEnabled { get; set; } = true;

    /// <summary>
    /// Whether the output takes the gamepads. Even when on, only the output receiving the keyboard reports them.
    /// </summary>
    public bool IsGamepadEnabled { get; set; } = true;

    /// <summary>Whether <see cref="RelativePosition"/> can answer at all: while the surface is off screen there is no
    /// point in its coordinates to answer with.</summary>
    public bool CanLocatePointer => output.IsVisible;

    public Vector2F RawMouseDelta => IsMouseEnabled ? rawMouseDelta : Vector2F.Zero;

    public bool IsKeyDown(Keys key)
    {
        return IsKeyboardEnabled && downKeys.Contains(key);
    }

    public bool IsKeyPressed(Keys key)
    {
        return IsKeyboardEnabled && pressedKeys.Contains(key);
    }

    public bool IsKeyReleased(Keys key)
    {
        return IsKeyboardEnabled && releasedKeys.Contains(key);
    }

    public bool IsMouseButtonDown(MouseButton button)
    {
        if (button == MouseButton.None || !IsMouseEnabled)
        {
            return false;
        }

        return mouseButtons[(int) button].IsDown;
    }

    public bool IsMouseButtonPressed(MouseButton button)
    {
        if (button == MouseButton.None || !IsMouseEnabled)
        {
            return false;
        }

        return mouseButtons[(int) button].IsPressed;
    }

    public bool IsMouseButtonReleased(MouseButton button)
    {
        if (button == MouseButton.None || !IsMouseEnabled)
        {
            return false;
        }

        return mouseButtons[(int)button].IsReleased;
    }

    public int MouseWheelDelta => IsMouseEnabled ? mouseWheelDelta : 0;

    public Vector2F AbsolutePosition => absolutePosition;

    public Vector2F RelativePosition => output.PointToSurface(absolutePosition);

    /// <summary>Holds the pointer for a drag the game runs - turning a gizmo, say: the cursor is hidden and pinned, and
    /// the motion arrives as <see cref="RawMouseDelta"/>. Released with false, which puts the cursor back.</summary>
    public void HoldPointer(bool hold)
    {
        if (hold == isPointerHeld)
        {
            return;
        }
        isPointerHeld = hold;
        output.HoldPointer(hold, absolutePosition);
    }

    public bool IsGamepadButtonDown(int gamepadIndex, GamepadButton button)
    {
        return ReportsGamepads() && gamepads.IsButtonDown(gamepadIndex, button);
    }

    public bool IsGamepadButtonPressed(int gamepadIndex, GamepadButton button)
    {
        return ReportsGamepads() && gamepads.IsButtonPressed(gamepadIndex, button);
    }

    public bool IsGamepadButtonReleased(int gamepadIndex, GamepadButton button)
    {
        return ReportsGamepads() && gamepads.IsButtonReleased(gamepadIndex, button);
    }

    /// <summary>State of a gamepad - empty unless this output receives the keyboard and takes gamepads.</summary>
    public GamepadState GetGamepadState(int gamepadIndex)
    {
        return ReportsGamepads() ? gamepads.GetState(gamepadIndex) : default;
    }

    private bool ReportsGamepads()
    {
        return IsGamepadEnabled && output.IsKeyboardFocused;
    }

    public void Update(AppTime gameTime)
    {
        UpdateKeyboard();
        UpdateMouse();
    }

    private void UpdateKeyboard()
    {
        pressedKeys.Clear();
        releasedKeys.Clear();

        lock (KeyboadInputs)
        {
            foreach (var keyboardInput in KeyboadInputs)
            {
                switch (keyboardInput.InputType)
                {
                    case InputType.Up:
                        if (IsKeyDown(keyboardInput.Key))
                        {
                            releasedKeys.Add(keyboardInput.Key);
                            downKeys.Remove(keyboardInput.Key);
                        }
                        break;

                    case InputType.Down:
                        if (!IsKeyDown(keyboardInput.Key))
                        {
                            pressedKeys.Add(keyboardInput.Key);
                            downKeys.Add(keyboardInput.Key);
                        }
                        break;
                }
            }
            KeyboadInputs.Clear();
        }
    }

    private void UpdateMouse()
    {
        absolutePosition = output.PointerScreenPosition;
        mouseWheelDelta = 0;
        rawMouseDelta = Vector2F.Zero;

        for (int i = 0; i < mouseButtons.Length; ++i)
        {
            mouseButtons[i].Reset();
        }

        lock (MouseInputs)
        {
            foreach (var mouseInput in MouseInputs)
            {
                ButtonState state;
                switch (mouseInput.InputType)
                {
                    case InputType.Up:
                        state = mouseButtons[(int)mouseInput.Button];
                        HandleButtonState(ref state, false);
                        mouseButtons[(int)mouseInput.Button] = state;
                        break;
                    case InputType.Down:
                        state = mouseButtons[(int)mouseInput.Button];
                        HandleButtonState(ref state, true);
                        mouseButtons[(int)mouseInput.Button] = state;
                        break;
                    case InputType.Wheel:
                        mouseWheelDelta += mouseInput.WheelDelta;
                        break;
                    case InputType.RawDelta:
                        rawMouseDelta += mouseInput.Delta;
                        break;
                }
            }
            MouseInputs.Clear();
        }
    }

    private void HandleButtonState(ref ButtonState state, bool isDown)
    {
        if (isDown)
        {
            if (!state.IsDown)
            {
                state.IsPressed = true;
            }
            state.IsDown = true;
            state.IsReleased = false;
        }
        else
        {
            state.IsReleased = true;
            state.IsDown = false;
            state.IsPressed = false;
        }
    }
}
