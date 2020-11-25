using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;
using Adamantium.Win32;
using Adamantium.XInput;

namespace Adamantium.Game.GameInput
{
    public class InputService : GameSystem
    {
        private const int MaxGamepadsCount = 8;
        private const float LeftThumbDeadZone = 0.2f;
        private const float RightThumbDeadZone = 0.2f;
        private const float TriggerThreshold = 0.11f;

        private readonly HashSet<Keys> downKeys;
        private readonly HashSet<Keys> pressedKeys;
        private readonly HashSet<Keys> releasedKeys;
        private readonly ButtonState[] _mouseButtons;
        private readonly HashSet<GamepadButton>[] downGamepadButtons;
        private readonly HashSet<GamepadButton>[] pressedGamepadButtons;
        private readonly HashSet<GamepadButton>[] releasedGamepadButtons;
        private readonly HashSet<GamepadButton>[] currentGamepadButtons;

        private readonly HashSet<GamepadButton> supportedGamepadButtons;

        private IGamePlatform _gamePlatform;
        protected Rectangle Bounds { get; private set; }
        private Vector2F _absolutePosition;
        private Vector2F _absolutePositionPrevious;
        private Vector2F _virtualPosition;
        private Vector2F _mouseDelta;
        private IntPtr Handle;
        private Vector2F _acceleratedMouseDelta;
        private Vector2F _lockMousePosition;
        private bool _isLockedToCenter;
        private GameWindow _window;
        private GameWindowCursor _currentCursor;
        private XBoxGamepadFactory _gamepadFactory;
        private Gamepad[] _gamepads;
        private GamepadState[] _gamepadStates;
        private int _virtualPositionMultiplierX = 0;
        private int _virtualPositionMultiplierY = 0;

        public InputService(GameBase game, IDependencyResolver container)
            : base(game, container)
        {
            downKeys = new HashSet<Keys>();
            pressedKeys = new HashSet<Keys>();
            releasedKeys = new HashSet<Keys>();

            downGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
            pressedGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
            releasedGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];
            currentGamepadButtons = new HashSet<GamepadButton>[MaxGamepadsCount];

            for (int i = 0; i < MaxGamepadsCount; i++)
            {
                downGamepadButtons[i] = new HashSet<GamepadButton>();
                pressedGamepadButtons[i] = new HashSet<GamepadButton>();
                releasedGamepadButtons[i] = new HashSet<GamepadButton>();
                currentGamepadButtons[i] = new HashSet<GamepadButton>();
            }
            

            KeyboadInputs = new List<KeyboardInput>();
            MouseInputs = new List<MouseInput>();

            Enabled = true;
            Services.RegisterInstance<InputService>(this);
            SystemManager.AddSystem(this);

            _mouseButtons = new ButtonState[5];

            _gamePlatform = container.Resolve<IGamePlatform>();
            _gamePlatform.WindowRemoved += GamePlatformWindowRemoved;
            _gamePlatform.WindowDeactivated += GamePlatformWindowDeactivated;
            _gamePlatform.WindowActivated += GamePlatformWindowActivated;
            _gamePlatform.WindowBoundsChanged += GamePlatformWindowBoundsChanged;
            _gamePlatform.MouseUp += GamePlatformMouseStateChanged;
            _gamePlatform.MouseDown += GamePlatformMouseStateChanged;
            _gamePlatform.MouseWheel += GamePlatformMouseStateChanged;
            _gamePlatform.MouseDelta += GamePlatformMouseStateChanged;
            _gamePlatform.KeyUp += GamePlatformKeyboardStateChanged;
            _gamePlatform.KeyDown += GamePlatformKeyboardStateChanged;

            _gamepadFactory = new XBoxGamepadFactory();
            _gamepads = _gamepadFactory.GetConnectedGamepads();
            _gamepadStates = new GamepadState[MaxGamepadsCount];

            supportedGamepadButtons = new HashSet<GamepadButton>();
            supportedGamepadButtons.Add(GamepadButton.A);
            supportedGamepadButtons.Add(GamepadButton.B);
            supportedGamepadButtons.Add(GamepadButton.X);
            supportedGamepadButtons.Add(GamepadButton.Y);
            supportedGamepadButtons.Add(GamepadButton.Back);
            supportedGamepadButtons.Add(GamepadButton.Start);
            supportedGamepadButtons.Add(GamepadButton.LeftThumb);
            supportedGamepadButtons.Add(GamepadButton.RightThumb);
            supportedGamepadButtons.Add(GamepadButton.LeftShoulder);
            supportedGamepadButtons.Add(GamepadButton.RightShoulder);
            supportedGamepadButtons.Add(GamepadButton.DpadLeft);
            supportedGamepadButtons.Add(GamepadButton.DpadRight);
            supportedGamepadButtons.Add(GamepadButton.DpadUp);
            supportedGamepadButtons.Add(GamepadButton.DpadDown);

            LockCursorToWindowBounds();
        }

        private void GamePlatformKeyboardStateChanged(object sender, KeyboardInputEventArgs e)
        {
            lock (KeyboadInputs)
            {
                KeyboadInputs.Add(e.KeyboardInput);
            }
        }

        private void GamePlatformMouseStateChanged(object sender, MouseInputEventArgs e)
        {
            lock (MouseInputs)
            {
                MouseInputs.Add(e.MouseInput);
            }
        }

        private void GamePlatformWindowBoundsChanged(object sender, GameWindowBoundsChangedEventArgs e)
        {
            Bounds = e.Bounds;
        }

        private void GamePlatformWindowActivated(object sender, GameWindowEventArgs e)
        {
            IsWindowFocused = true;
            IsWindowAvailable = true;
            Bounds = e.Window.ClientBounds;
            Handle = e.Window.Handle;
            _window = e.Window;
        }

        private void GamePlatformWindowDeactivated(object sender, GameWindowEventArgs e)
        {
            IsWindowFocused = false;
        }

        private void GamePlatformWindowRemoved(object sender, GameWindowEventArgs e)
        {
            IsWindowAvailable = false;
        }

        internal List<KeyboardInput> KeyboadInputs { get; private set; }

        internal List<MouseInput> MouseInputs { get; private set; }

        public bool HasKeyboard { get; internal set; }

        public bool HasMouse { get; internal set; }

        public bool HasGamePad { get; internal set; }

        public bool IsWindowFocused { get; private set; }

        public bool IsWindowAvailable { get; private set; }

        public Vector2F RawMouseDelta { get; private set; }

        public Vector2F AcceleratedMouseDelta
        {
            get { return _acceleratedMouseDelta; }
            private set { _acceleratedMouseDelta = value; }
        }

        public bool IsKeyDown(Keys key)
        {
            return downKeys.Contains(key);
        }

        public bool IsKeyPressed(Keys key)
        {
            return pressedKeys.Contains(key);
        }

        public bool IsKeyReleased(Keys key)
        {
            return releasedKeys.Contains(key);
        }

        public bool IsMouseButtonDown(MouseButton button)
        {
            if (button == MouseButton.None)
            {
                return false;
            }

            return _mouseButtons[(int) button].IsDown;
        }

        public bool IsMouseButtonPressed(MouseButton button)
        {
            if (button == MouseButton.None)
            {
                return false;
            }

            return _mouseButtons[(int) button].IsPressed;
        }

        public bool IsMouseButtonReleased(MouseButton button)
        {
            if (button == MouseButton.None)
            {
                return false;
            }

            return _mouseButtons[(int)button].IsReleased;
        }

        public int MouseWheelDelta { get; private set; }

        public Vector2F AbsolutePosition => _absolutePosition;

        public Vector2F RelativePosition
        {
            get
            {
                NativePoint point = new NativePoint((int)_absolutePosition.X, (int)_absolutePosition.Y);
                Win32Interop.ScreenToClient(Handle, ref point);
                return new Vector2F(point.X, point.Y);
            }
        }

        public Vector2F VirtualPosition
        {
            get
            {
                if (IsMouseButtonDown(MouseButton.Left) && IsLockedToWindowBounds)
                {
                    return _virtualPosition;
                }

                return RelativePosition;
            }
        }

        public void ScanInputDevices()
        {
            
        }

        public bool IsMousePositionLocked { get; private set; }

        public bool IsLockedToWindowBounds { get; private set; }

        protected virtual void SetMousePosition(Vector2F position)
        {
            Win32Interop.SetCursorPos((int)position.X, (int)position.Y);
        }

        protected virtual void LockMousePosition(bool lockToCenter = true)
        {
            IsMousePositionLocked = true;
            _isLockedToCenter = lockToCenter;
            Win32Interop.GetCursorPos(out var point);
            _lockMousePosition = new Vector2F(point.X, point.Y);
            SetLockedMousePosition();
            if (_window != null)
            {
                _currentCursor = _window.Cursor;
                _window.Cursor = GameWindowCursor.None;
            }
        }

        protected virtual void UnlockMousePosition()
        {
            IsMousePositionLocked = false;
            if (_window != null)
            {
                _window.Cursor = _currentCursor;
            }
        }

        protected virtual void LockCursorToWindowBounds()
        {
            IsLockedToWindowBounds = true;
        }

        protected virtual void UnlockWindowBounds()
        {
            IsLockedToWindowBounds = false;
        }

        public bool IsGamepadButtonDown(int gamepadIndex, GamepadButton button)
        {
            return downGamepadButtons[gamepadIndex].Contains(button);
        }

        public bool IsGamepadButtonPressed(int gamepadIndex, GamepadButton button)
        {
            return pressedGamepadButtons[gamepadIndex].Contains(button);
        }

        public bool IsGamepadButtonReleased(int gamepadIndex, GamepadButton button)
        {
            return releasedGamepadButtons[gamepadIndex].Contains(button);
        }

        public GamepadState GetGamepadState(int gamepadIndex)
        {
            return _gamepadStates[gamepadIndex];
        }

        public override void Update(IGameTime gameTime)
        {
            if (!IsWindowFocused)
            {
                return;
            }

            UpdateKeyboard();
            UpdateMouse();
            UpdateGamepads();
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
            if (!IsMousePositionLocked)
            {
                Win32Interop.GetCursorPos(out NativePoint np);
                _absolutePosition = new Vector2F(np.X, np.Y);
            }
            else
            {
                SetLockedMousePosition();
            }
            MouseWheelDelta = 0;
            RawMouseDelta = Vector2F.Zero;
            AcceleratedMouseDelta = Vector2F.Zero;

            for (int i = 0; i < _mouseButtons.Length; ++i)
            {
                _mouseButtons[i].Reset();
            }

            lock (MouseInputs)
            {
                foreach (var mouseInput in MouseInputs)
                {
                    ButtonState state;
                    switch (mouseInput.InputType)
                    {
                        case InputType.Up:
                            state = _mouseButtons[(int)mouseInput.Button];
                            HandleButtonState(ref state, false);
                            _mouseButtons[(int)mouseInput.Button] = state;
                            break;
                        case InputType.Down:
                            state = _mouseButtons[(int)mouseInput.Button];
                            HandleButtonState(ref state, true);
                            _mouseButtons[(int)mouseInput.Button] = state;
                            break;
                        case InputType.Wheel:
                            MouseWheelDelta += mouseInput.WheelDelta;
                            break;
                        case InputType.RawDelta:
                            RawMouseDelta += mouseInput.Delta;
                            break;
                    }
                }
                MouseInputs.Clear();
            }

            if (IsMouseButtonPressed(MouseButton.Left) && IsLockedToWindowBounds)
            {
                _virtualPosition = RelativePosition;
                //_window.Cursor = GameWindowCursor.None;
            }

            if (IsMouseButtonReleased(MouseButton.Left))
            {
                _virtualPosition = RelativePosition;
                _virtualPositionMultiplierX = 0;
                _virtualPositionMultiplierY = 0;
                //_window.Cursor = GameWindowCursor.Arrow;
            }

            if (IsMouseButtonDown(MouseButton.Left) && (IsLockedToWindowBounds && RawMouseDelta != Vector2F.Zero))
            {
                CalculateMousePosition();
                if (!IsMousePositionLocked)
                {
                    if (_virtualPositionMultiplierX == 0)
                    {
                        _virtualPosition.X = RelativePosition.X;
                    }
                    else
                    {
                        _virtualPosition.X = Bounds.Width * _virtualPositionMultiplierX +RelativePosition.X;
                    }

                    if (_virtualPositionMultiplierY == 0)
                    {
                        _virtualPosition.Y = RelativePosition.Y;
                    }
                    else
                    {
                        _virtualPosition.Y = Bounds.Height * _virtualPositionMultiplierY + RelativePosition.Y;
                    }
                }
            }
            _absolutePositionPrevious = _absolutePosition;
        }

        private void UpdateGamepads()
        {
            lock (_gamepadStates)
            {
                for (int i = 0; i < MaxGamepadsCount; i++)
                {
                    pressedGamepadButtons[i].Clear();
                    releasedGamepadButtons[i].Clear();
                    currentGamepadButtons[i].Clear();
                    _gamepadStates[i].IsConnected = false;
                }

                for (var i = 0; i < _gamepads.Length; i++)
                {
                    var gamepad = _gamepads[i];
                    var state = gamepad.GetState();
                    ClampDeadZone(ref state);
                    _gamepadStates[i] = state;
                }

                for (int i = 0; i < _gamepadStates.Length; ++i) 
                {
                    foreach (var supportedGamepadButton in supportedGamepadButtons)
                    {
                        if (!_gamepadStates[i].IsConnected)
                        {
                            continue;
                        }

                        var state = _gamepadStates[i];
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

        private void CalculateMousePosition()
        {
            if (IsOutsideXBounds())
            {
                if (AbsolutePosition.X >= Bounds.Right)
                {
                    _absolutePosition.X = Bounds.Left;
                    _virtualPositionMultiplierX++;
                }
                else if (AbsolutePosition.X <= Bounds.Left)
                {
                    _absolutePosition.X = Bounds.Right;
                    _virtualPositionMultiplierX--;
                }
                Win32Interop.SetCursorPos((int)_absolutePosition.X, (int)_absolutePosition.Y);
            }

            if (IsOutsideYBounds())
            {
                if (AbsolutePosition.Y >= Bounds.Bottom)
                {
                    _absolutePosition.Y = Bounds.Top;
                    _virtualPositionMultiplierY++;
                }
                else if (AbsolutePosition.Y <= Bounds.Top)
                {
                    _absolutePosition.Y = Bounds.Bottom;
                    _virtualPositionMultiplierY--;
                }
                Win32Interop.SetCursorPos((int)_absolutePosition.X, (int)_absolutePosition.Y);
            }

            if (_absolutePosition.X == _absolutePositionPrevious.X && RawMouseDelta.X != 0)
            {
                if (AbsolutePosition.X >= SystemParameters.VirtualScreenWidth - 1)
                {
                    _absolutePosition.X = Bounds.Left;
                    _virtualPositionMultiplierX++;
                }
                else if (AbsolutePosition.X <= 0)
                {
                    _absolutePosition.X = Bounds.Right;
                    _virtualPositionMultiplierX--;
                }
                Win32Interop.SetCursorPos((int)_absolutePosition.X, (int)_absolutePosition.Y);
            }

            if (_absolutePosition.Y == _absolutePositionPrevious.Y && RawMouseDelta.Y != 0)
            {
                if (AbsolutePosition.Y >= SystemParameters.VirtualScreenHeight - 1)
                {
                    _absolutePosition.Y = Bounds.Top;
                    _virtualPositionMultiplierY++;
                }
                else if (AbsolutePosition.Y <= 0)
                {
                    _absolutePosition.Y = Bounds.Bottom;
                    _virtualPositionMultiplierY--;
                }
                Win32Interop.SetCursorPos((int)_absolutePosition.X, (int)_absolutePosition.Y);
            }
        }

        private bool IsOutsideXBounds()
        {
            var actualPos = AbsolutePosition.X;
            if (actualPos > Bounds.Right || actualPos < Bounds.X)
            {
                return true;
            }
            return false;
        }

        private bool IsOutsideYBounds()
        {
            var actualPos = AbsolutePosition.Y;
            if (actualPos > Bounds.Bottom || actualPos < Bounds.Y)
            {
                return true;
            }
            return false;
        }

        private void SetLockedMousePosition()
        {
            if (!_isLockedToCenter)
            {
                Win32Interop.SetCursorPos((int)_lockMousePosition.X, (int)_lockMousePosition.Y);
            }
            else
            {
                Win32Interop.SetCursorPos((int)Bounds.Center.X, (int)Bounds.Center.Y);
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

    public struct KeyboardInput : IEquatable<KeyboardInput>
    {
        public Keys Key;

        public InputType InputType;

        public bool Equals(KeyboardInput other)
        {
            return Key == other.Key && InputType == other.InputType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ButtonState && Equals((ButtonState)obj);
        }
    }


    public struct MouseInput : IEquatable<MouseInput>
    {
        public MouseButton Button;

        public InputType InputType;

        public int WheelDelta;

        public Vector2F Delta;

        public bool Equals(MouseInput other)
        {
            return Button == other.Button && InputType == other.InputType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MouseButton && Equals((MouseButton)obj);
        }
    }

    public enum InputType
    {
        Up,
        Down,
        Wheel,
        RawDelta,
    }

}
