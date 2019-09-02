using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Graphics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Rectangle = Adamantium.Mathematics.Rectangle;
using AUIKeys = Adamantium.UI.Input.Key;
using Adamantium.Imaging;

namespace Adamantium.Engine
{
    /// <summary>
    /// Represents <see cref="GameWindow"/> based on <see cref="Window"/> 
    /// </summary>
    public class GameWindowAdamantium : GameWindow
    {
        private RenderTargetPanel nativeWindow;
        private Rectangle clientBounds;
        private GameWindowCursor cursor;
        private static readonly Dictionary<Key, Keys> translationKeys;

        static GameWindowAdamantium()
        {
            translationKeys = new Dictionary<AUIKeys, Keys>();
            translationKeys[AUIKeys.None] = Keys.None;
            translationKeys[AUIKeys.BackSpace] = Keys.Back;
            translationKeys[AUIKeys.Tab] = Keys.Tab;
            translationKeys[AUIKeys.Enter] = Keys.Enter;
            translationKeys[AUIKeys.Pause] = Keys.Pause;
            translationKeys[AUIKeys.CapsLock] = Keys.CapsLock;
            translationKeys[AUIKeys.IMEKana] = Keys.Kana;
            translationKeys[AUIKeys.IMEKanji] = Keys.Kanji;
            translationKeys[AUIKeys.Escape] = Keys.Escape;
            translationKeys[AUIKeys.IMEConvert] = Keys.ImeConvert;
            translationKeys[AUIKeys.IMENonconvert] = Keys.ImeNonConvert;
            translationKeys[AUIKeys.Space] = Keys.Space;
            translationKeys[AUIKeys.PageUp] = Keys.PageUp;
            translationKeys[AUIKeys.PageDown] = Keys.PageDown;
            translationKeys[AUIKeys.End] = Keys.End;
            translationKeys[AUIKeys.Home] = Keys.Home;
            translationKeys[AUIKeys.LeftArrow] = Keys.LeftArrow;
            translationKeys[AUIKeys.UpArrow] = Keys.UpArrow;
            translationKeys[AUIKeys.RightArrow] = Keys.RightArrow;
            translationKeys[AUIKeys.DownArrow] = Keys.DownArrow;
            translationKeys[AUIKeys.Select] = Keys.Select;
            translationKeys[AUIKeys.Print] = Keys.Print;
            translationKeys[AUIKeys.Execute] = Keys.Execute;
            translationKeys[AUIKeys.PrintScreen] = Keys.PrintScreen;
            translationKeys[AUIKeys.Insert] = Keys.Insert;
            translationKeys[AUIKeys.Delete] = Keys.Delete;
            translationKeys[AUIKeys.Help] = Keys.Help;
            translationKeys[AUIKeys.D0] = Keys.Digit0;
            translationKeys[AUIKeys.D1] = Keys.Digit1;
            translationKeys[AUIKeys.D2] = Keys.Digit2;
            translationKeys[AUIKeys.D3] = Keys.Digit3;
            translationKeys[AUIKeys.D4] = Keys.Digit4;
            translationKeys[AUIKeys.D5] = Keys.Digit5;
            translationKeys[AUIKeys.D6] = Keys.Digit6;
            translationKeys[AUIKeys.D7] = Keys.Digit7;
            translationKeys[AUIKeys.D8] = Keys.Digit8;
            translationKeys[AUIKeys.D9] = Keys.Digit9;
            translationKeys[AUIKeys.A] = Keys.A;
            translationKeys[AUIKeys.B] = Keys.B;
            translationKeys[AUIKeys.C] = Keys.C;
            translationKeys[AUIKeys.D] = Keys.D;
            translationKeys[AUIKeys.E] = Keys.E;
            translationKeys[AUIKeys.F] = Keys.F;
            translationKeys[AUIKeys.G] = Keys.G;
            translationKeys[AUIKeys.H] = Keys.H;
            translationKeys[AUIKeys.I] = Keys.I;
            translationKeys[AUIKeys.J] = Keys.J;
            translationKeys[AUIKeys.K] = Keys.K;
            translationKeys[AUIKeys.L] = Keys.L;
            translationKeys[AUIKeys.M] = Keys.M;
            translationKeys[AUIKeys.N] = Keys.N;
            translationKeys[AUIKeys.O] = Keys.O;
            translationKeys[AUIKeys.P] = Keys.P;
            translationKeys[AUIKeys.Q] = Keys.Q;
            translationKeys[AUIKeys.R] = Keys.R;
            translationKeys[AUIKeys.S] = Keys.S;
            translationKeys[AUIKeys.T] = Keys.T;
            translationKeys[AUIKeys.U] = Keys.U;
            translationKeys[AUIKeys.V] = Keys.V;
            translationKeys[AUIKeys.W] = Keys.W;
            translationKeys[AUIKeys.X] = Keys.X;
            translationKeys[AUIKeys.Y] = Keys.Y;
            translationKeys[AUIKeys.Z] = Keys.Z;
            translationKeys[AUIKeys.LeftWin] = Keys.LeftWindows;
            translationKeys[AUIKeys.RightWin] = Keys.RightWindows;
            translationKeys[AUIKeys.Apps] = Keys.Apps;
            translationKeys[AUIKeys.Sleep] = Keys.Sleep;
            translationKeys[AUIKeys.NumPad0] = Keys.NumPad6;
            translationKeys[AUIKeys.NumPad1] = Keys.NumPad1;
            translationKeys[AUIKeys.NumPad2] = Keys.NumPad2;
            translationKeys[AUIKeys.NumPad3] = Keys.NumPad3;
            translationKeys[AUIKeys.NumPad4] = Keys.NumPad4;
            translationKeys[AUIKeys.NumPad5] = Keys.NumPad5;
            translationKeys[AUIKeys.NumPad6] = Keys.NumPad6;
            translationKeys[AUIKeys.NumPad7] = Keys.NumPad7;
            translationKeys[AUIKeys.NumPad8] = Keys.NumPad8;
            translationKeys[AUIKeys.NumPad9] = Keys.NumPad9;
            translationKeys[AUIKeys.NumPadMultiply] = Keys.Multiply;
            translationKeys[AUIKeys.NumPadAdd] = Keys.Add;
            translationKeys[AUIKeys.Separator] = Keys.Separator;
            translationKeys[AUIKeys.NumPadSubtract] = Keys.Subtract;
            translationKeys[AUIKeys.NumPadDecimal] = Keys.Decimal;
            translationKeys[AUIKeys.NumPadDivide] = Keys.Divide;
            translationKeys[AUIKeys.F1] = Keys.F1;
            translationKeys[AUIKeys.F2] = Keys.F2;
            translationKeys[AUIKeys.F3] = Keys.F3;
            translationKeys[AUIKeys.F4] = Keys.F4;
            translationKeys[AUIKeys.F5] = Keys.F5;
            translationKeys[AUIKeys.F6] = Keys.F6;
            translationKeys[AUIKeys.F7] = Keys.F7;
            translationKeys[AUIKeys.F8] = Keys.F8;
            translationKeys[AUIKeys.F9] = Keys.F9;
            translationKeys[AUIKeys.F10] = Keys.F10;
            translationKeys[AUIKeys.F11] = Keys.F11;
            translationKeys[AUIKeys.F12] = Keys.F12;
            translationKeys[AUIKeys.F13] = Keys.F13;
            translationKeys[AUIKeys.F14] = Keys.F14;
            translationKeys[AUIKeys.F15] = Keys.F15;
            translationKeys[AUIKeys.F16] = Keys.F16;
            translationKeys[AUIKeys.F17] = Keys.F17;
            translationKeys[AUIKeys.F18] = Keys.F18;
            translationKeys[AUIKeys.F19] = Keys.F19;
            translationKeys[AUIKeys.F20] = Keys.F20;
            translationKeys[AUIKeys.F21] = Keys.F21;
            translationKeys[AUIKeys.F22] = Keys.F22;
            translationKeys[AUIKeys.F23] = Keys.F23;
            translationKeys[AUIKeys.F24] = Keys.F24;
            translationKeys[AUIKeys.NumLock] = Keys.NumLock;
            translationKeys[AUIKeys.ScrollLock] = Keys.ScrollLock;
            translationKeys[AUIKeys.LeftShift] = Keys.LeftShift;
            translationKeys[AUIKeys.RightShift] = Keys.RightShift;
            translationKeys[AUIKeys.LeftCtrl] = Keys.LeftControl;
            translationKeys[AUIKeys.RightCtrl] = Keys.RightControl;
            translationKeys[AUIKeys.LeftAlt] = Keys.LeftAlt;
            translationKeys[AUIKeys.RightAlt] = Keys.RightAlt;
            translationKeys[AUIKeys.BrowserBackward] = Keys.BrowserBack;
            translationKeys[AUIKeys.BrowserForward] = Keys.BrowserForward;
            translationKeys[AUIKeys.BrowserRefresh] = Keys.BrowserRefresh;
            translationKeys[AUIKeys.BrowserStop] = Keys.BrowserStop;
            translationKeys[AUIKeys.BrowserSearch] = Keys.BrowserSearch;
            translationKeys[AUIKeys.BrowserFavorites] = Keys.BrowserFavorites;
            translationKeys[AUIKeys.BrowserHome] = Keys.BrowserHome;
            translationKeys[AUIKeys.VolumeMute] = Keys.VolumeMute;
            translationKeys[AUIKeys.VolumeDown] = Keys.VolumeDown;
            translationKeys[AUIKeys.VolumeUp] = Keys.VolumeUp;
            translationKeys[AUIKeys.NextTrack] = Keys.MediaNextTrack;
            translationKeys[AUIKeys.PrevTrack] = Keys.MediaPreviousTrack;
            translationKeys[AUIKeys.StopMedia] = Keys.MediaStop;
            translationKeys[AUIKeys.PlayPauseMedia] = Keys.MediaPlayPause;
            translationKeys[AUIKeys.LaunchMail] = Keys.LaunchMail;
            translationKeys[AUIKeys.LaunchMediaSelect] = Keys.SelectMedia;
            translationKeys[AUIKeys.LaunchApp1] = Keys.LaunchApplication1;
            translationKeys[AUIKeys.LaunchApp2] = Keys.LaunchApplication2;
            translationKeys[AUIKeys.OemSemicolon] = Keys.OemSemicolon;
            translationKeys[AUIKeys.OemPlus] = Keys.OemPlus;
            translationKeys[AUIKeys.OemComma] = Keys.OemComma;
            translationKeys[AUIKeys.OemMinus] = Keys.OemMinus;
            translationKeys[AUIKeys.OemPeriod] = Keys.OemPeriod;
            translationKeys[AUIKeys.OemQuestion] = Keys.OemQuestion;
            translationKeys[AUIKeys.OemTilde] = Keys.OemTilde;
            translationKeys[AUIKeys.OemOpenBrackets] = Keys.OemOpenBrackets;
            translationKeys[AUIKeys.OemPipe] = Keys.OemPipe;
            translationKeys[AUIKeys.OemCloseBrackets] = Keys.OemCloseBrackets;
            translationKeys[AUIKeys.OemQuotes] = Keys.OemQuotes;
            translationKeys[AUIKeys.Oem8] = Keys.Oem8;
            translationKeys[AUIKeys.OemBackSlash] = Keys.OemBackslash;
            translationKeys[AUIKeys.ProcessKey] = Keys.ProcessKey;
            translationKeys[AUIKeys.Attn] = Keys.Attn;
            translationKeys[AUIKeys.CrSel] = Keys.Crsel;
            translationKeys[AUIKeys.ExSel] = Keys.Exsel;
            translationKeys[AUIKeys.EraseEof] = Keys.EraseEof;
            translationKeys[AUIKeys.Play] = Keys.Play;
            translationKeys[AUIKeys.Zoom] = Keys.Zoom;
            translationKeys[AUIKeys.PA1] = Keys.Pa1;
            translationKeys[AUIKeys.OemClear] = Keys.OemClear;
            translationKeys[AUIKeys.Shift] = Keys.Shift;
            translationKeys[AUIKeys.Ctrl] = Keys.Control;
            translationKeys[AUIKeys.Alt] = Keys.Alt;
        }

        internal GameWindowAdamantium(GameContext context)
        {
            Initialize(context);
        }

        internal GameWindowAdamantium(GameContext context, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel)
        {
            Initialize(context, pixelFormat, depthFormat, msaaLevel);
        }

        protected override void Initialize(GameContext context)
        {
            InitializeInternal(context);
        }

        protected override void Initialize(GameContext context, SurfaceFormat pixelFormat,
           DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.X4)
        {
            InitializeInternal(context);
            Description.PixelFormat = pixelFormat;
            Description.DepthFormat = depthFormat;
            Description.MSAALevel = msaaLevel;
        }

        private void InitializeInternal(GameContext context)
        {
            GameContext = context;
            nativeWindow = (RenderTargetPanel)GameContext.Context;
            nativeWindow.RenderTargetChanged += NativeWindow_RenderTargetChanged;
            nativeWindow.GotFocus += NativeWindow_GotFocus;
            nativeWindow.LostFocus += NativeWindow_LostFocus;
            Description = new GameWindowDescription(PresenterType.RenderTarget);
            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Handle = nativeWindow.Handle;
            clientBounds = new Rectangle(0, 0, (int)Description.Width, (int)Description.Height);
        }

        private void NativeWindow_RenderTargetChanged(object sender, RenderTargetEventArgs e)
        {
            Handle = e.Handle;
            clientBounds = new Rectangle(0, 0, e.Width, e.Height);
            Width = (uint)nativeWindow.ActualWidth;
            Height = (uint)nativeWindow.ActualHeight;
            Debug.WriteLine("RenderTarget window size = " + Description.Width + " " + Description.Height);
        }

        private void NativeWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            OnDeactivated();
        }

        private void NativeWindow_GotFocus(object sender, RoutedEventArgs e)
        {
            OnActivated();
        }

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="GameWindow"/> 
        /// </summary>
        public override GameWindowCursor Cursor
        {
            get
            {
                return cursor;
            }
            set
            {
                cursor = value;
                switch (value)
                {
                    case GameWindowCursor.Arrow:
                        nativeWindow.Cursor = Cursors.Arrow;
                        break;
                    case GameWindowCursor.AppStarting:
                        nativeWindow.Cursor = Cursors.AppStarting;
                        break;
                    case GameWindowCursor.CrossHair:
                        nativeWindow.Cursor = Cursors.Crosshair;
                        break;
                    case GameWindowCursor.Hand:
                        nativeWindow.Cursor = Cursors.Hand;
                        break;
                    case GameWindowCursor.Help:
                        nativeWindow.Cursor = Cursors.Help;
                        break;
                    case GameWindowCursor.IBeam:
                        nativeWindow.Cursor = Cursors.IBeam;
                        break;
                    case GameWindowCursor.No:
                        nativeWindow.Cursor = Cursors.No;
                        break;
                    case GameWindowCursor.None:
                        nativeWindow.Cursor = Cursors.None;
                        break;
                    case GameWindowCursor.SizeAll:
                        nativeWindow.Cursor = Cursors.SizeAll;
                        break;
                    case GameWindowCursor.SizeNWSE:
                        nativeWindow.Cursor = Cursors.SizeNWSE;
                        break;
                    case GameWindowCursor.SizeEWE:
                        nativeWindow.Cursor = Cursors.SizeEWE;
                        break;
                    case GameWindowCursor.SizeNESW:
                        nativeWindow.Cursor = Cursors.SizeNESW;
                        break;
                    case GameWindowCursor.SizeNS:
                        nativeWindow.Cursor = Cursors.SizeNS;
                        break;
                    case GameWindowCursor.UpArrow:
                        nativeWindow.Cursor = Cursors.UpArrow;
                        break;
                    case GameWindowCursor.Wait:
                        nativeWindow.Cursor = Cursors.Wait;
                        break;
                }
            }
        }

        /// <summary>
        /// Contains <see cref="GameWindow"/> description
        /// </summary>
        protected override GameWindowDescription Description { get; set; }

        /// <summary>
        /// Bounds of the <see cref="GameWindow"/> starting always from (0,0)
        /// </summary>
        public override Rectangle ClientBounds => clientBounds;

        /// <summary>
        /// Underlying control for rendering
        /// </summary>
        public override object NativeWindow => nativeWindow;

        /// <summary>
        /// Defines is <see cref="GameWindow"/> currently displayed
        /// </summary>
        public override bool IsVisible
        {
            get { return nativeWindow.Visibility == Visibility.Visible; }
            set { nativeWindow.Visibility = Visibility.Hidden; }
        }


        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.RenderTargetPanel;
        }

        internal override void Resize(int width, int height)
        {
            nativeWindow.Width = width;
            nativeWindow.Height = height;
        }

        internal override void SwitchContext(GameContext context)
        {
            if (CanHandle(context))
            {
                nativeWindow.RenderTargetChanged -= NativeWindow_RenderTargetChanged;
                nativeWindow.GotFocus -= NativeWindow_GotFocus;
                nativeWindow.LostFocus -= NativeWindow_LostFocus;
                Initialize(context);
            }
        }

        //internal override GraphicsPresenter CreatePresenter(GraphicsDevice device)
        //{
        //    Presenter = new RenderTargetGraphicsPresenter(device, Description.ToPresentationParameters(), GeneratePresenterName());
        //    return Presenter;
        //}

    }
}
