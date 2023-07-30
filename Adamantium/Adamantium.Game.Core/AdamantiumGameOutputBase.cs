using System.Collections.Generic;
using Adamantium.Core.Events;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Core.Input;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Input;
using GameMouseButtons = Adamantium.Game.Core.Input.MouseButton;


namespace Adamantium.Game.Core
{
    public abstract class AdamantiumGameOutputBase : GameOutput
    {
        private GameWindowCursor cursor;
        
        public override GameWindowDescription Description { get; protected set; }

        protected IInputComponent InputComponent { get; set; }

        protected static readonly Dictionary<Key, Keys> TranslationKeys;
        protected static readonly Dictionary<MouseButtons, GameMouseButtons> MouseTranslationKeys;

        static AdamantiumGameOutputBase()
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
            TranslationKeys[Key.Print] = Keys.Print;
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
            TranslationKeys[Key.NumPad0] = Keys.NumPad6;
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
            TranslationKeys[Key.Oem8] = Keys.Oem8;
            TranslationKeys[Key.OemBackSlash] = Keys.OemBackslash;
            TranslationKeys[Key.ProcessKey] = Keys.ProcessKey;
            TranslationKeys[Key.Attn] = Keys.Attn;
            TranslationKeys[Key.CrSel] = Keys.Crsel;
            TranslationKeys[Key.ExSel] = Keys.Exsel;
            TranslationKeys[Key.EraseEof] = Keys.EraseEof;
            TranslationKeys[Key.Play] = Keys.Play;
            TranslationKeys[Key.Zoom] = Keys.Zoom;
            TranslationKeys[Key.PA1] = Keys.Pa1;
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

        protected AdamantiumGameOutputBase(IEventAggregator eventAggregator) : base(eventAggregator)
        {
            
        }

        protected override void Initialize(GameContext context)
        {
            Initialize(context, SurfaceFormat.B8G8R8A8.UNorm);
        }

        protected override void Initialize(
            GameContext context, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4)
        {
            GameContext = context;
            InitializeInternal(context);
            Description.PixelFormat = pixelFormat;
            Description.DepthFormat = depthFormat;
            Description.MsaaLevel = msaaLevel;
        }
        
        protected virtual void InitializeInternal(GameContext context)
        {
            InputComponent.KeyDown += WindowOnKeyDown;
            InputComponent.KeyUp += WindowOnKeyUp;
            InputComponent.MouseDown += OnMouseDown;
            InputComponent.MouseUp += OnMouseUp;
            InputComponent.MouseWheel += OnMouseWheel;
            InputComponent.RawMouseMove += OnMouseMove;
        }

        public override object NativeWindow => GameContext.Context;

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="GameOutput"/> 
        /// </summary>
        public override GameWindowCursor Cursor
        {
            get => cursor;
            set
            {
                cursor = value;
                switch (value)
                {
                    case GameWindowCursor.Arrow:
                        InputComponent.Cursor = Cursors.Arrow;
                        break;
                    case GameWindowCursor.AppStarting:
                        InputComponent.Cursor = Cursors.AppStarting;
                        break;
                    case GameWindowCursor.CrossHair:
                        InputComponent.Cursor = Cursors.Crosshair;
                        break;
                    case GameWindowCursor.Hand:
                        InputComponent.Cursor = Cursors.Hand;
                        break;
                    case GameWindowCursor.Help:
                        InputComponent.Cursor = Cursors.Help;
                        break;
                    case GameWindowCursor.IBeam:
                        InputComponent.Cursor = Cursors.IBeam;
                        break;
                    case GameWindowCursor.No:
                        InputComponent.Cursor = Cursors.No;
                        break;
                    case GameWindowCursor.None:
                        InputComponent.Cursor = Cursors.None;
                        break;
                    case GameWindowCursor.SizeAll:
                        InputComponent.Cursor = Cursors.SizeAll;
                        break;
                    case GameWindowCursor.SizeNWSE:
                        InputComponent.Cursor = Cursors.SizeNWSE;
                        break;
                    case GameWindowCursor.SizeEWE:
                        InputComponent.Cursor = Cursors.SizeEWE;
                        break;
                    case GameWindowCursor.SizeNESW:
                        InputComponent.Cursor = Cursors.SizeNESW;
                        break;
                    case GameWindowCursor.SizeNS:
                        InputComponent.Cursor = Cursors.SizeNS;
                        break;
                    case GameWindowCursor.UpArrow:
                        InputComponent.Cursor = Cursors.UpArrow;
                        break;
                    case GameWindowCursor.Wait:
                        InputComponent.Cursor = Cursors.Wait;
                        break;
                }
            }
        }
        
        /// <summary>
        /// Defines is <see cref="GameOutput"/> currently displayed
        /// </summary>
        public override bool IsVisible => InputComponent.Visibility == Visibility.Visible;

        internal override void Resize(uint width, uint height)
        {
            UpdateViewportAndScissor(width, height);
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
            var mouseInput = new MouseInput();
            mouseInput.InputType = InputType.Up;
            mouseInput.Button = MouseTranslationKeys[e.ChangedButton];
            OnMouseInput(mouseInput);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var mouseInput = new MouseInput();
            mouseInput.InputType = InputType.Down;
            mouseInput.Button = MouseTranslationKeys[e.ChangedButton];
            OnMouseInput(mouseInput);
        }

        private void WindowOnKeyUp(object sender, KeyEventArgs e)
        {
            var keyboardInput = new KeyboardInput();
            keyboardInput.Key = TranslationKeys[e.Key];
            keyboardInput.InputType = InputType.Up;
            
            OnKeyInput(keyboardInput);
        }

        private void WindowOnKeyDown(object sender, KeyEventArgs e)
        {
            var keyboardInput = new KeyboardInput();
            keyboardInput.Key = TranslationKeys[e.Key];
            keyboardInput.InputType = InputType.Down;
            
            OnKeyInput(keyboardInput);
        }
    }
}