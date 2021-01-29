using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Core;
using Adamantium.Game.Events;
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
        private readonly ButtonState[] mouseButtons;
        private readonly HashSet<GamepadButton>[] downGamepadButtons;
        private readonly HashSet<GamepadButton>[] pressedGamepadButtons;
        private readonly HashSet<GamepadButton>[] releasedGamepadButtons;
        private readonly HashSet<GamepadButton>[] currentGamepadButtons;

        private readonly HashSet<GamepadButton> supportedGamepadButtons;
        
        private IEventAggregator eventAggregator;
        
        protected Rectangle Bounds { get; private set; }
        private Vector2F absolutePosition;
        private Vector2F absolutePositionPrevious;
        private Vector2F virtualPosition;
        private Vector2F mouseDelta;
        private IntPtr Handle;
        private Vector2F acceleratedMouseDelta;
        private Vector2F lockMousePosition;
        private bool isLockedToCenter;
        private GameOutput window;
        private GameWindowCursor currentCursor;
        private XBoxGamepadFactory gamepadFactory;
        private Gamepad[] gamepads;
        private GamepadState[] gamepadStates;
        private int virtualPositionMultiplierX = 0;
        private int virtualPositionMultiplierY = 0;

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

            mouseButtons = new ButtonState[5];

            eventAggregator = container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<GameOutputRemovedEvent>().Subscribe(GamePlatformWindowRemoved);
            eventAggregator.GetEvent<GameOutputActivatedEvent>().Subscribe(GamePlatformWindowActivated);
            eventAggregator.GetEvent<GameOutputDeactivatedEvent>().Subscribe(GamePlatformWindowDeactivated);
            eventAggregator.GetEvent<GameOutputBoundsChangedEvent>().Subscribe(GamePlatformWindowBoundsChanged);
            eventAggregator.GetEvent<MouseInputEvent>().Subscribe(GamePlatformMouseStateChanged);
            eventAggregator.GetEvent<KeyboardInputEvent>().Subscribe(GamePlatformKeyboardStateChanged);

            gamepadFactory = new XBoxGamepadFactory();
            gamepads = gamepadFactory.GetConnectedGamepads();
            gamepadStates = new GamepadState[MaxGamepadsCount];

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

            //LockCursorToWindowBounds();
        }

        private void GamePlatformKeyboardStateChanged(KeyboardInput e)
        {
            lock (KeyboadInputs)
            {
                KeyboadInputs.Add(e);
            }
        }

        private void GamePlatformMouseStateChanged(MouseInput e)
        {
            lock (MouseInputs)
            {
                MouseInputs.Add(e);
            }
        }

        private void GamePlatformWindowBoundsChanged(GameOutputBoundsChangedPayload payload)
        {
            Bounds = payload.Bounds;
        }

        private void GamePlatformWindowActivated(GameOutput output)
        {
            IsWindowFocused = true;
            IsWindowAvailable = true;
            Bounds = output.ClientBounds;
            Handle = output.Handle;
            window = output;
        }

        private void GamePlatformWindowDeactivated(GameOutput output)
        {
            IsWindowFocused = false;
        }

        private void GamePlatformWindowRemoved(GameOutput output)
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
            get { return acceleratedMouseDelta; }
            private set { acceleratedMouseDelta = value; }
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

            return mouseButtons[(int) button].IsDown;
        }

        public bool IsMouseButtonPressed(MouseButton button)
        {
            if (button == MouseButton.None)
            {
                return false;
            }

            return mouseButtons[(int) button].IsPressed;
        }

        public bool IsMouseButtonReleased(MouseButton button)
        {
            if (button == MouseButton.None)
            {
                return false;
            }

            return mouseButtons[(int)button].IsReleased;
        }

        public int MouseWheelDelta { get; private set; }

        public Vector2F AbsolutePosition => absolutePosition;

        public Vector2F RelativePosition
        {
            get
            {
                NativePoint point = new NativePoint((int)absolutePosition.X, (int)absolutePosition.Y);
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
                    return virtualPosition;
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
            isLockedToCenter = lockToCenter;
            Win32Interop.GetCursorPos(out var point);
            lockMousePosition = new Vector2F(point.X, point.Y);
            SetLockedMousePosition();
            if (window != null)
            {
                currentCursor = window.Cursor;
                window.Cursor = GameWindowCursor.None;
            }
        }

        protected virtual void UnlockMousePosition()
        {
            IsMousePositionLocked = false;
            if (window != null)
            {
                window.Cursor = currentCursor;
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
            return gamepadStates[gamepadIndex];
        }

        public override void Update(IGameTime gameTime)
        {
            if (!IsWindowFocused)
            {
                //return;
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
                absolutePosition = new Vector2F(np.X, np.Y);
            }
            else
            {
                SetLockedMousePosition();
            }
            MouseWheelDelta = 0;
            RawMouseDelta = Vector2F.Zero;
            AcceleratedMouseDelta = Vector2F.Zero;

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
                virtualPosition = RelativePosition;
                //_window.Cursor = GameWindowCursor.None;
            }

            if (IsMouseButtonReleased(MouseButton.Left))
            {
                virtualPosition = RelativePosition;
                virtualPositionMultiplierX = 0;
                virtualPositionMultiplierY = 0;
                //_window.Cursor = GameWindowCursor.Arrow;
            }

            if (IsMouseButtonDown(MouseButton.Left) && (IsMousePositionLocked && RawMouseDelta != Vector2F.Zero))
            {
                CalculateMousePosition();
                if (!IsMousePositionLocked)
                {
                    if (virtualPositionMultiplierX == 0)
                    {
                        virtualPosition.X = RelativePosition.X;
                    }
                    else
                    {
                        virtualPosition.X = Bounds.Width * virtualPositionMultiplierX +RelativePosition.X;
                    }
                
                    if (virtualPositionMultiplierY == 0)
                    {
                        virtualPosition.Y = RelativePosition.Y;
                    }
                    else
                    {
                        virtualPosition.Y = Bounds.Height * virtualPositionMultiplierY + RelativePosition.Y;
                    }
                }
            }
            absolutePositionPrevious = absolutePosition;
        }

        private void UpdateGamepads()
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

        private void CalculateMousePosition()
        {
            if (IsOutsideXBounds())
            {
                if (AbsolutePosition.X >= Bounds.Right)
                {
                    absolutePosition.X = Bounds.Left;
                    virtualPositionMultiplierX++;
                }
                else if (AbsolutePosition.X <= Bounds.Left)
                {
                    absolutePosition.X = Bounds.Right;
                    virtualPositionMultiplierX--;
                }
                Win32Interop.SetCursorPos((int)absolutePosition.X, (int)absolutePosition.Y);
            }

            if (IsOutsideYBounds())
            {
                if (AbsolutePosition.Y >= Bounds.Bottom)
                {
                    absolutePosition.Y = Bounds.Top;
                    virtualPositionMultiplierY++;
                }
                else if (AbsolutePosition.Y <= Bounds.Top)
                {
                    absolutePosition.Y = Bounds.Bottom;
                    virtualPositionMultiplierY--;
                }
                Win32Interop.SetCursorPos((int)absolutePosition.X, (int)absolutePosition.Y);
            }

            if (absolutePosition.X == absolutePositionPrevious.X && RawMouseDelta.X != 0)
            {
                if (AbsolutePosition.X >= SystemParameters.VirtualScreenWidth - 1)
                {
                    absolutePosition.X = Bounds.Left;
                    virtualPositionMultiplierX++;
                }
                else if (AbsolutePosition.X <= 0)
                {
                    absolutePosition.X = Bounds.Right;
                    virtualPositionMultiplierX--;
                }
                Win32Interop.SetCursorPos((int)absolutePosition.X, (int)absolutePosition.Y);
            }

            if (absolutePosition.Y == absolutePositionPrevious.Y && RawMouseDelta.Y != 0)
            {
                if (AbsolutePosition.Y >= SystemParameters.VirtualScreenHeight - 1)
                {
                    absolutePosition.Y = Bounds.Top;
                    virtualPositionMultiplierY++;
                }
                else if (AbsolutePosition.Y <= 0)
                {
                    absolutePosition.Y = Bounds.Bottom;
                    virtualPositionMultiplierY--;
                }
                Win32Interop.SetCursorPos((int)absolutePosition.X, (int)absolutePosition.Y);
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
            if (!isLockedToCenter)
            {
                Win32Interop.SetCursorPos((int)lockMousePosition.X, (int)lockMousePosition.Y);
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
}
