using System;
using System.Collections.Generic;
using Adamantium.Core.Events;
using Adamantium.Multiverse;
using Adamantium.Multiverse.Input;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using UniverseMouseButton = Adamantium.Multiverse.Input.MouseButton;


namespace Adamantium.UI.Universes
{
    public abstract class UIUniverseOutput : UniverseOutput
    {
        private OutputCursor cursor;
        private IWindow scaleSource;
        private IInputComponent listenedInput;

        public override UniverseOutputDescription Description { get; protected set; }

        /// <summary>The component the universe's surface IS - a window, or the panel it is hosted in. Public because a
        /// cursor position only means something relative to it.</summary>
        public IInputComponent InputComponent { get; protected set; }

        // Relative to the surface, not the window, through the UI's own conversion: by hand it was right only at 100% DPI.
        public override Vector2F PointerScreenPosition
        {
            get
            {
                var point = MouseDevice.CurrentDevice.GetScreenPosition();
                return new Vector2F((float)point.X, (float)point.Y);
            }
        }

        public override Vector2F PointToSurface(Vector2F absolute)
        {
            var point = UIExtensions.PointToClient(InputComponent, new PixelPoint((int)absolute.X, (int)absolute.Y));
            return new Vector2F((float)point.X, (float)point.Y) * PixelsPerPoint;
        }

        /// <summary>The host window's scale, pixels per point; 1 while the surface has no window.</summary>
        protected float HostScale => HostWindow is { } window ? (float)window.DpiScale.X : 1;

        /// <summary>The host window moved to a screen of another scale; <see cref="HostScale"/> has the new one.</summary>
        protected virtual void OnHostScaleChanged()
        {
        }

        // The same relative mode the surface's own mouse-look engages: it enters and leaves on the window's own thread
        // (the worker posts itself a message), which is what makes it safe to ask for from the universe loop.
        public override void HoldPointer(bool hold, Vector2F origin)
        {
            if (InputComponent?.RootVisual is not WindowBase root) return;

            if (hold) heldFrom = new PixelPoint((int)origin.X, (int)origin.Y);

            root.SetRelativeMouseMode(hold, hold ? default : heldFrom);
        }

        private PixelPoint heldFrom;

        private static readonly HashSet<Keys> KnownKeys = [.. Enum.GetValues<Keys>()];

        protected internal static readonly Dictionary<Key, Keys> TranslationKeys;
        protected internal static readonly Dictionary<MouseButtons, UniverseMouseButton> MouseTranslationKeys;

        static UIUniverseOutput()
        {
            TranslationKeys = new Dictionary<Key, Keys>();
            TranslationKeys[Key.None] = Keys.None;
            TranslationKeys[Key.BackSpace] = Keys.Back;
            TranslationKeys[Key.Tab] = Keys.Tab;
            TranslationKeys[Key.Enter] = Keys.Enter;
            TranslationKeys[Key.Pause] = Keys.Pause;
            TranslationKeys[Key.CapsLock] = Keys.CapsLock;
            TranslationKeys[Key.IMEKana] = Keys.Kana;
            TranslationKeys[Key.IMEKanji] = Keys.Kanji;
            TranslationKeys[Key.Escape] = Keys.Escape;
            TranslationKeys[Key.IMEConvert] = Keys.ImeConvert;
            TranslationKeys[Key.IMENonconvert] = Keys.ImeNonConvert;
            TranslationKeys[Key.Space] = Keys.Space;
            TranslationKeys[Key.PageUp] = Keys.PageUp;
            TranslationKeys[Key.PageDown] = Keys.PageDown;
            TranslationKeys[Key.End] = Keys.End;
            TranslationKeys[Key.Home] = Keys.Home;
            TranslationKeys[Key.LeftArrow] = Keys.LeftArrow;
            TranslationKeys[Key.UpArrow] = Keys.UpArrow;
            TranslationKeys[Key.RightArrow] = Keys.RightArrow;
            TranslationKeys[Key.DownArrow] = Keys.DownArrow;
            TranslationKeys[Key.Select] = Keys.Select;
            TranslationKeys[Key.Execute] = Keys.Execute;
            TranslationKeys[Key.PrintScreen] = Keys.PrintScreen;
            TranslationKeys[Key.Insert] = Keys.Insert;
            TranslationKeys[Key.Delete] = Keys.Delete;
            TranslationKeys[Key.Help] = Keys.Help;
            TranslationKeys[Key.D0] = Keys.Digit0;
            TranslationKeys[Key.D1] = Keys.Digit1;
            TranslationKeys[Key.D2] = Keys.Digit2;
            TranslationKeys[Key.D3] = Keys.Digit3;
            TranslationKeys[Key.D4] = Keys.Digit4;
            TranslationKeys[Key.D5] = Keys.Digit5;
            TranslationKeys[Key.D6] = Keys.Digit6;
            TranslationKeys[Key.D7] = Keys.Digit7;
            TranslationKeys[Key.D8] = Keys.Digit8;
            TranslationKeys[Key.D9] = Keys.Digit9;
            TranslationKeys[Key.A] = Keys.A;
            TranslationKeys[Key.B] = Keys.B;
            TranslationKeys[Key.C] = Keys.C;
            TranslationKeys[Key.D] = Keys.D;
            TranslationKeys[Key.E] = Keys.E;
            TranslationKeys[Key.F] = Keys.F;
            TranslationKeys[Key.G] = Keys.G;
            TranslationKeys[Key.H] = Keys.H;
            TranslationKeys[Key.I] = Keys.I;
            TranslationKeys[Key.J] = Keys.J;
            TranslationKeys[Key.K] = Keys.K;
            TranslationKeys[Key.L] = Keys.L;
            TranslationKeys[Key.M] = Keys.M;
            TranslationKeys[Key.N] = Keys.N;
            TranslationKeys[Key.O] = Keys.O;
            TranslationKeys[Key.P] = Keys.P;
            TranslationKeys[Key.Q] = Keys.Q;
            TranslationKeys[Key.R] = Keys.R;
            TranslationKeys[Key.S] = Keys.S;
            TranslationKeys[Key.T] = Keys.T;
            TranslationKeys[Key.U] = Keys.U;
            TranslationKeys[Key.V] = Keys.V;
            TranslationKeys[Key.W] = Keys.W;
            TranslationKeys[Key.X] = Keys.X;
            TranslationKeys[Key.Y] = Keys.Y;
            TranslationKeys[Key.Z] = Keys.Z;
            TranslationKeys[Key.LeftWin] = Keys.LeftWindows;
            TranslationKeys[Key.RightWin] = Keys.RightWindows;
            TranslationKeys[Key.Apps] = Keys.Apps;
            TranslationKeys[Key.Sleep] = Keys.Sleep;
            TranslationKeys[Key.NumPad0] = Keys.NumPad0;
            TranslationKeys[Key.NumPad1] = Keys.NumPad1;
            TranslationKeys[Key.NumPad2] = Keys.NumPad2;
            TranslationKeys[Key.NumPad3] = Keys.NumPad3;
            TranslationKeys[Key.NumPad4] = Keys.NumPad4;
            TranslationKeys[Key.NumPad5] = Keys.NumPad5;
            TranslationKeys[Key.NumPad6] = Keys.NumPad6;
            TranslationKeys[Key.NumPad7] = Keys.NumPad7;
            TranslationKeys[Key.NumPad8] = Keys.NumPad8;
            TranslationKeys[Key.NumPad9] = Keys.NumPad9;
            TranslationKeys[Key.NumPadMultiply] = Keys.Multiply;
            TranslationKeys[Key.NumPadAdd] = Keys.Add;
            TranslationKeys[Key.Separator] = Keys.Separator;
            TranslationKeys[Key.NumPadSubtract] = Keys.Subtract;
            TranslationKeys[Key.NumPadDecimal] = Keys.Decimal;
            TranslationKeys[Key.NumPadDivide] = Keys.Divide;
            TranslationKeys[Key.F1] = Keys.F1;
            TranslationKeys[Key.F2] = Keys.F2;
            TranslationKeys[Key.F3] = Keys.F3;
            TranslationKeys[Key.F4] = Keys.F4;
            TranslationKeys[Key.F5] = Keys.F5;
            TranslationKeys[Key.F6] = Keys.F6;
            TranslationKeys[Key.F7] = Keys.F7;
            TranslationKeys[Key.F8] = Keys.F8;
            TranslationKeys[Key.F9] = Keys.F9;
            TranslationKeys[Key.F10] = Keys.F10;
            TranslationKeys[Key.F11] = Keys.F11;
            TranslationKeys[Key.F12] = Keys.F12;
            TranslationKeys[Key.F13] = Keys.F13;
            TranslationKeys[Key.F14] = Keys.F14;
            TranslationKeys[Key.F15] = Keys.F15;
            TranslationKeys[Key.F16] = Keys.F16;
            TranslationKeys[Key.F17] = Keys.F17;
            TranslationKeys[Key.F18] = Keys.F18;
            TranslationKeys[Key.F19] = Keys.F19;
            TranslationKeys[Key.F20] = Keys.F20;
            TranslationKeys[Key.F21] = Keys.F21;
            TranslationKeys[Key.F22] = Keys.F22;
            TranslationKeys[Key.F23] = Keys.F23;
            TranslationKeys[Key.F24] = Keys.F24;
            TranslationKeys[Key.NumLock] = Keys.NumLock;
            TranslationKeys[Key.ScrollLock] = Keys.ScrollLock;
            TranslationKeys[Key.LeftShift] = Keys.LeftShift;
            TranslationKeys[Key.RightShift] = Keys.RightShift;
            TranslationKeys[Key.LeftCtrl] = Keys.LeftControl;
            TranslationKeys[Key.RightCtrl] = Keys.RightControl;
            TranslationKeys[Key.LeftAlt] = Keys.LeftAlt;
            TranslationKeys[Key.RightAlt] = Keys.RightAlt;
            TranslationKeys[Key.BrowserBackward] = Keys.BrowserBack;
            TranslationKeys[Key.BrowserForward] = Keys.BrowserForward;
            TranslationKeys[Key.BrowserRefresh] = Keys.BrowserRefresh;
            TranslationKeys[Key.BrowserStop] = Keys.BrowserStop;
            TranslationKeys[Key.BrowserSearch] = Keys.BrowserSearch;
            TranslationKeys[Key.BrowserFavorites] = Keys.BrowserFavorites;
            TranslationKeys[Key.BrowserHome] = Keys.BrowserHome;
            TranslationKeys[Key.VolumeMute] = Keys.VolumeMute;
            TranslationKeys[Key.VolumeDown] = Keys.VolumeDown;
            TranslationKeys[Key.VolumeUp] = Keys.VolumeUp;
            TranslationKeys[Key.NextTrack] = Keys.MediaNextTrack;
            TranslationKeys[Key.PrevTrack] = Keys.MediaPreviousTrack;
            TranslationKeys[Key.StopMedia] = Keys.MediaStop;
            TranslationKeys[Key.PlayPauseMedia] = Keys.MediaPlayPause;
            TranslationKeys[Key.LaunchMail] = Keys.LaunchMail;
            TranslationKeys[Key.LaunchMediaSelect] = Keys.SelectMedia;
            TranslationKeys[Key.LaunchApp1] = Keys.LaunchApplication1;
            TranslationKeys[Key.LaunchApp2] = Keys.LaunchApplication2;
            TranslationKeys[Key.OemSemicolon] = Keys.OemSemicolon;
            TranslationKeys[Key.OemPlus] = Keys.OemPlus;
            TranslationKeys[Key.OemComma] = Keys.OemComma;
            TranslationKeys[Key.OemMinus] = Keys.OemMinus;
            TranslationKeys[Key.OemPeriod] = Keys.OemPeriod;
            TranslationKeys[Key.OemQuestion] = Keys.OemQuestion;
            TranslationKeys[Key.OemTilde] = Keys.OemTilde;
            TranslationKeys[Key.OemOpenBrackets] = Keys.OemOpenBrackets;
            TranslationKeys[Key.OemPipe] = Keys.OemPipe;
            TranslationKeys[Key.OemCloseBrackets] = Keys.OemCloseBrackets;
            TranslationKeys[Key.OemQuotes] = Keys.OemQuotes;
            TranslationKeys[Key.OemBackSlash] = Keys.OemBackslash;
            TranslationKeys[Key.CrSel] = Keys.Crsel;
            TranslationKeys[Key.ExSel] = Keys.Exsel;
            TranslationKeys[Key.OemClear] = Keys.OemClear;
            TranslationKeys[Key.Shift] = Keys.Shift;
            TranslationKeys[Key.Ctrl] = Keys.Control;
            TranslationKeys[Key.Alt] = Keys.Alt;

            MouseTranslationKeys = new Dictionary<MouseButtons, MouseButton>();
            MouseTranslationKeys[MouseButtons.Left] = MouseButton.Left;
            MouseTranslationKeys[MouseButtons.Middle] = MouseButton.Middle;
            MouseTranslationKeys[MouseButtons.Right] = MouseButton.Right;
            MouseTranslationKeys[MouseButtons.None] = MouseButton.None;
            MouseTranslationKeys[MouseButtons.XButton1] = MouseButton.XButton1;
            MouseTranslationKeys[MouseButtons.XButton2] = MouseButton.XButton2;
            
        }

        protected UIUniverseOutput(IEventAggregator eventAggregator) : base(eventAggregator)
        {
            
        }

        protected override void Initialize(OutputContext context)
        {
            Initialize(context, SurfaceFormat.B8G8R8A8.UNorm);
        }

        protected override void Initialize(
            OutputContext context, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4)
        {
            OutputContext = context;
            InitializeInternal(context);
            Description.PixelFormat = pixelFormat;
            Description.DepthFormat = depthFormat;
            Description.MsaaLevel = msaaLevel;
        }
        
        protected virtual void InitializeInternal(OutputContext context)
        {
            if (scaleSource != null)
            {
                scaleSource.DpiChanged -= HostDpiChanged;
            }

            scaleSource = HostWindow;
            if (scaleSource != null)
            {
                scaleSource.DpiChanged += HostDpiChanged;
            }

            StopListening();
            listenedInput = InputComponent;
            listenedInput.KeyDown += WindowOnKeyDown;
            listenedInput.KeyUp += WindowOnKeyUp;
            listenedInput.TextInput += WindowOnTextInput;
            listenedInput.MouseDown += OnMouseDown;
            listenedInput.MouseUp += OnMouseUp;
            listenedInput.MouseWheel += OnMouseWheel;
            listenedInput.RawMouseMove += OnMouseMove;
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            StopListening();
            if (scaleSource != null)
            {
                scaleSource.DpiChanged -= HostDpiChanged;
                scaleSource = null;
            }

            base.Dispose(disposeManagedResources);
        }

        private void StopListening()
        {
            if (listenedInput == null)
            {
                return;
            }

            listenedInput.KeyDown -= WindowOnKeyDown;
            listenedInput.KeyUp -= WindowOnKeyUp;
            listenedInput.TextInput -= WindowOnTextInput;
            listenedInput.MouseDown -= OnMouseDown;
            listenedInput.MouseUp -= OnMouseUp;
            listenedInput.MouseWheel -= OnMouseWheel;
            listenedInput.RawMouseMove -= OnMouseMove;
            listenedInput = null;
        }

        public override object NativeWindow => OutputContext.Context;

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="UniverseOutput"/> 
        /// </summary>
        public override OutputCursor Cursor
        {
            get => cursor;
            set
            {
                cursor = value;
                switch (value)
                {
                    case OutputCursor.Arrow:
                        InputComponent.Cursor = Cursors.Arrow;
                        break;
                    case OutputCursor.AppStarting:
                        InputComponent.Cursor = Cursors.AppStarting;
                        break;
                    case OutputCursor.CrossHair:
                        InputComponent.Cursor = Cursors.Crosshair;
                        break;
                    case OutputCursor.Hand:
                        InputComponent.Cursor = Cursors.Hand;
                        break;
                    case OutputCursor.Help:
                        InputComponent.Cursor = Cursors.Help;
                        break;
                    case OutputCursor.IBeam:
                        InputComponent.Cursor = Cursors.IBeam;
                        break;
                    case OutputCursor.No:
                        InputComponent.Cursor = Cursors.No;
                        break;
                    case OutputCursor.None:
                        InputComponent.Cursor = Cursors.None;
                        break;
                    case OutputCursor.SizeAll:
                        InputComponent.Cursor = Cursors.SizeAll;
                        break;
                    case OutputCursor.SizeNWSE:
                        InputComponent.Cursor = Cursors.SizeNWSE;
                        break;
                    case OutputCursor.SizeEWE:
                        InputComponent.Cursor = Cursors.SizeEWE;
                        break;
                    case OutputCursor.SizeNESW:
                        InputComponent.Cursor = Cursors.SizeNESW;
                        break;
                    case OutputCursor.SizeNS:
                        InputComponent.Cursor = Cursors.SizeNS;
                        break;
                    case OutputCursor.UpArrow:
                        InputComponent.Cursor = Cursors.UpArrow;
                        break;
                    case OutputCursor.Wait:
                        InputComponent.Cursor = Cursors.Wait;
                        break;
                }
            }
        }
        
        public override OutputState State
        {
            get
            {
                if (HostWindow is { State: WindowState.Minimized })
                {
                    return OutputState.Minimized;
                }

                if (!InputComponent.IsAttachedToVisualTree)
                {
                    return OutputState.OutOfView;
                }

                if (InputComponent.Visibility != Visibility.Visible)
                {
                    return OutputState.Hidden;
                }

                return OutputState.Shown;
            }
        }

        public override bool IsKeyboardFocused => InputComponent.IsKeyboardFocused && HostWindow is { IsActive: true };

        public override bool IsPointerOver =>
            InputComponent.IsMouseDirectlyOver || ReferenceEquals(Mouse.Captured, InputComponent);

        protected IWindow HostWindow => InputComponent as IWindow ?? InputComponent.RootVisual as IWindow;

        private void HostDpiChanged(object sender, EventArgs e)
        {
            OnHostScaleChanged();
        }

        private void OnMouseMove(object sender, UnboundMouseEventArgs e)
        {
            var mouseInput = new MouseInput();
            mouseInput.InputType = InputType.RawDelta;
            mouseInput.Delta = e.Delta;
            OnMouseInput(mouseInput);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mouseInput = new MouseInput();
            mouseInput.InputType = InputType.Wheel;
            mouseInput.WheelDelta = e.Delta;
            OnMouseInput(mouseInput);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!MouseTranslationKeys.TryGetValue(e.ChangedButton, out var button))
            {
                return;
            }

            OnMouseInput(new MouseInput { InputType = InputType.Up, Button = button });
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!MouseTranslationKeys.TryGetValue(e.ChangedButton, out var button))
            {
                return;
            }

            OnMouseInput(new MouseInput { InputType = InputType.Down, Button = button, ClickCount = e.ClickCount });
        }

        internal static bool TryTranslate(Key key, uint physicalKey, out Keys result)
        {
            if (physicalKey != 0 && KnownKeys.Contains((Keys)physicalKey))
            {
                result = (Keys)physicalKey;
                return true;
            }

            return TranslationKeys.TryGetValue(key, out result);
        }

        private void WindowOnKeyUp(object sender, KeyEventArgs e)
        {
            if (!TryTranslate(e.Key, e.PhysicalKey, out var key))
            {
                return;
            }

            OnKeyInput(new KeyboardInput { Key = key, InputType = InputType.Up });
        }

        private void WindowOnTextInput(object sender, TextInputEventArgs e)
        {
            OnTextInput(e.Text);
        }

        private void WindowOnKeyDown(object sender, KeyEventArgs e)
        {
            if (!TryTranslate(e.Key, e.PhysicalKey, out var key))
            {
                return;
            }

            OnKeyInput(new KeyboardInput { Key = key, InputType = InputType.Down });
        }
    }
}