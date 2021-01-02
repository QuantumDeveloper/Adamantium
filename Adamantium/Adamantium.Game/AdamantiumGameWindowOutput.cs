using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Game.GameInput;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.UI.Input;

namespace Adamantium.Game
{
    public abstract class AdamantiumGameOutputBase : GameOutput
    {
        private GameWindowCursor cursor;

        protected FrameworkComponent UIComponent { get; set; }

        private static readonly Dictionary<Key, Keys> translationKeys;

        static AdamantiumGameOutputBase()
        {
            translationKeys = new Dictionary<Key, Keys>();
            translationKeys[Key.None] = Keys.None;
            translationKeys[Key.BackSpace] = Keys.Back;
            translationKeys[Key.Tab] = Keys.Tab;
            translationKeys[Key.Enter] = Keys.Enter;
            translationKeys[Key.Pause] = Keys.Pause;
            translationKeys[Key.CapsLock] = Keys.CapsLock;
            translationKeys[Key.IMEKana] = Keys.Kana;
            translationKeys[Key.IMEKanji] = Keys.Kanji;
            translationKeys[Key.Escape] = Keys.Escape;
            translationKeys[Key.IMEConvert] = Keys.ImeConvert;
            translationKeys[Key.IMENonconvert] = Keys.ImeNonConvert;
            translationKeys[Key.Space] = Keys.Space;
            translationKeys[Key.PageUp] = Keys.PageUp;
            translationKeys[Key.PageDown] = Keys.PageDown;
            translationKeys[Key.End] = Keys.End;
            translationKeys[Key.Home] = Keys.Home;
            translationKeys[Key.LeftArrow] = Keys.LeftArrow;
            translationKeys[Key.UpArrow] = Keys.UpArrow;
            translationKeys[Key.RightArrow] = Keys.RightArrow;
            translationKeys[Key.DownArrow] = Keys.DownArrow;
            translationKeys[Key.Select] = Keys.Select;
            translationKeys[Key.Print] = Keys.Print;
            translationKeys[Key.Execute] = Keys.Execute;
            translationKeys[Key.PrintScreen] = Keys.PrintScreen;
            translationKeys[Key.Insert] = Keys.Insert;
            translationKeys[Key.Delete] = Keys.Delete;
            translationKeys[Key.Help] = Keys.Help;
            translationKeys[Key.D0] = Keys.Digit0;
            translationKeys[Key.D1] = Keys.Digit1;
            translationKeys[Key.D2] = Keys.Digit2;
            translationKeys[Key.D3] = Keys.Digit3;
            translationKeys[Key.D4] = Keys.Digit4;
            translationKeys[Key.D5] = Keys.Digit5;
            translationKeys[Key.D6] = Keys.Digit6;
            translationKeys[Key.D7] = Keys.Digit7;
            translationKeys[Key.D8] = Keys.Digit8;
            translationKeys[Key.D9] = Keys.Digit9;
            translationKeys[Key.A] = Keys.A;
            translationKeys[Key.B] = Keys.B;
            translationKeys[Key.C] = Keys.C;
            translationKeys[Key.D] = Keys.D;
            translationKeys[Key.E] = Keys.E;
            translationKeys[Key.F] = Keys.F;
            translationKeys[Key.G] = Keys.G;
            translationKeys[Key.H] = Keys.H;
            translationKeys[Key.I] = Keys.I;
            translationKeys[Key.J] = Keys.J;
            translationKeys[Key.K] = Keys.K;
            translationKeys[Key.L] = Keys.L;
            translationKeys[Key.M] = Keys.M;
            translationKeys[Key.N] = Keys.N;
            translationKeys[Key.O] = Keys.O;
            translationKeys[Key.P] = Keys.P;
            translationKeys[Key.Q] = Keys.Q;
            translationKeys[Key.R] = Keys.R;
            translationKeys[Key.S] = Keys.S;
            translationKeys[Key.T] = Keys.T;
            translationKeys[Key.U] = Keys.U;
            translationKeys[Key.V] = Keys.V;
            translationKeys[Key.W] = Keys.W;
            translationKeys[Key.X] = Keys.X;
            translationKeys[Key.Y] = Keys.Y;
            translationKeys[Key.Z] = Keys.Z;
            translationKeys[Key.LeftWin] = Keys.LeftWindows;
            translationKeys[Key.RightWin] = Keys.RightWindows;
            translationKeys[Key.Apps] = Keys.Apps;
            translationKeys[Key.Sleep] = Keys.Sleep;
            translationKeys[Key.NumPad0] = Keys.NumPad6;
            translationKeys[Key.NumPad1] = Keys.NumPad1;
            translationKeys[Key.NumPad2] = Keys.NumPad2;
            translationKeys[Key.NumPad3] = Keys.NumPad3;
            translationKeys[Key.NumPad4] = Keys.NumPad4;
            translationKeys[Key.NumPad5] = Keys.NumPad5;
            translationKeys[Key.NumPad6] = Keys.NumPad6;
            translationKeys[Key.NumPad7] = Keys.NumPad7;
            translationKeys[Key.NumPad8] = Keys.NumPad8;
            translationKeys[Key.NumPad9] = Keys.NumPad9;
            translationKeys[Key.NumPadMultiply] = Keys.Multiply;
            translationKeys[Key.NumPadAdd] = Keys.Add;
            translationKeys[Key.Separator] = Keys.Separator;
            translationKeys[Key.NumPadSubtract] = Keys.Subtract;
            translationKeys[Key.NumPadDecimal] = Keys.Decimal;
            translationKeys[Key.NumPadDivide] = Keys.Divide;
            translationKeys[Key.F1] = Keys.F1;
            translationKeys[Key.F2] = Keys.F2;
            translationKeys[Key.F3] = Keys.F3;
            translationKeys[Key.F4] = Keys.F4;
            translationKeys[Key.F5] = Keys.F5;
            translationKeys[Key.F6] = Keys.F6;
            translationKeys[Key.F7] = Keys.F7;
            translationKeys[Key.F8] = Keys.F8;
            translationKeys[Key.F9] = Keys.F9;
            translationKeys[Key.F10] = Keys.F10;
            translationKeys[Key.F11] = Keys.F11;
            translationKeys[Key.F12] = Keys.F12;
            translationKeys[Key.F13] = Keys.F13;
            translationKeys[Key.F14] = Keys.F14;
            translationKeys[Key.F15] = Keys.F15;
            translationKeys[Key.F16] = Keys.F16;
            translationKeys[Key.F17] = Keys.F17;
            translationKeys[Key.F18] = Keys.F18;
            translationKeys[Key.F19] = Keys.F19;
            translationKeys[Key.F20] = Keys.F20;
            translationKeys[Key.F21] = Keys.F21;
            translationKeys[Key.F22] = Keys.F22;
            translationKeys[Key.F23] = Keys.F23;
            translationKeys[Key.F24] = Keys.F24;
            translationKeys[Key.NumLock] = Keys.NumLock;
            translationKeys[Key.ScrollLock] = Keys.ScrollLock;
            translationKeys[Key.LeftShift] = Keys.LeftShift;
            translationKeys[Key.RightShift] = Keys.RightShift;
            translationKeys[Key.LeftCtrl] = Keys.LeftControl;
            translationKeys[Key.RightCtrl] = Keys.RightControl;
            translationKeys[Key.LeftAlt] = Keys.LeftAlt;
            translationKeys[Key.RightAlt] = Keys.RightAlt;
            translationKeys[Key.BrowserBackward] = Keys.BrowserBack;
            translationKeys[Key.BrowserForward] = Keys.BrowserForward;
            translationKeys[Key.BrowserRefresh] = Keys.BrowserRefresh;
            translationKeys[Key.BrowserStop] = Keys.BrowserStop;
            translationKeys[Key.BrowserSearch] = Keys.BrowserSearch;
            translationKeys[Key.BrowserFavorites] = Keys.BrowserFavorites;
            translationKeys[Key.BrowserHome] = Keys.BrowserHome;
            translationKeys[Key.VolumeMute] = Keys.VolumeMute;
            translationKeys[Key.VolumeDown] = Keys.VolumeDown;
            translationKeys[Key.VolumeUp] = Keys.VolumeUp;
            translationKeys[Key.NextTrack] = Keys.MediaNextTrack;
            translationKeys[Key.PrevTrack] = Keys.MediaPreviousTrack;
            translationKeys[Key.StopMedia] = Keys.MediaStop;
            translationKeys[Key.PlayPauseMedia] = Keys.MediaPlayPause;
            translationKeys[Key.LaunchMail] = Keys.LaunchMail;
            translationKeys[Key.LaunchMediaSelect] = Keys.SelectMedia;
            translationKeys[Key.LaunchApp1] = Keys.LaunchApplication1;
            translationKeys[Key.LaunchApp2] = Keys.LaunchApplication2;
            translationKeys[Key.OemSemicolon] = Keys.OemSemicolon;
            translationKeys[Key.OemPlus] = Keys.OemPlus;
            translationKeys[Key.OemComma] = Keys.OemComma;
            translationKeys[Key.OemMinus] = Keys.OemMinus;
            translationKeys[Key.OemPeriod] = Keys.OemPeriod;
            translationKeys[Key.OemQuestion] = Keys.OemQuestion;
            translationKeys[Key.OemTilde] = Keys.OemTilde;
            translationKeys[Key.OemOpenBrackets] = Keys.OemOpenBrackets;
            translationKeys[Key.OemPipe] = Keys.OemPipe;
            translationKeys[Key.OemCloseBrackets] = Keys.OemCloseBrackets;
            translationKeys[Key.OemQuotes] = Keys.OemQuotes;
            translationKeys[Key.Oem8] = Keys.Oem8;
            translationKeys[Key.OemBackSlash] = Keys.OemBackslash;
            translationKeys[Key.ProcessKey] = Keys.ProcessKey;
            translationKeys[Key.Attn] = Keys.Attn;
            translationKeys[Key.CrSel] = Keys.Crsel;
            translationKeys[Key.ExSel] = Keys.Exsel;
            translationKeys[Key.EraseEof] = Keys.EraseEof;
            translationKeys[Key.Play] = Keys.Play;
            translationKeys[Key.Zoom] = Keys.Zoom;
            translationKeys[Key.PA1] = Keys.Pa1;
            translationKeys[Key.OemClear] = Keys.OemClear;
            translationKeys[Key.Shift] = Keys.Shift;
            translationKeys[Key.Ctrl] = Keys.Control;
            translationKeys[Key.Alt] = Keys.Alt;
        }

        protected AdamantiumGameOutputBase()
        {
            
        }
        

        protected override void Initialize(GameContext context)
        {
            GameContext = context;
            InitializeInternal(context);
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
            
        }
        
        /// <summary>
        /// Contains <see cref="GameOutput"/> description
        /// </summary>
        protected override GameWindowDescription Description { get; set; }

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
                        UIComponent.Cursor = Cursors.Arrow;
                        break;
                    case GameWindowCursor.AppStarting:
                        UIComponent.Cursor = Cursors.AppStarting;
                        break;
                    case GameWindowCursor.CrossHair:
                        UIComponent.Cursor = Cursors.Crosshair;
                        break;
                    case GameWindowCursor.Hand:
                        UIComponent.Cursor = Cursors.Hand;
                        break;
                    case GameWindowCursor.Help:
                        UIComponent.Cursor = Cursors.Help;
                        break;
                    case GameWindowCursor.IBeam:
                        UIComponent.Cursor = Cursors.IBeam;
                        break;
                    case GameWindowCursor.No:
                        UIComponent.Cursor = Cursors.No;
                        break;
                    case GameWindowCursor.None:
                        UIComponent.Cursor = Cursors.None;
                        break;
                    case GameWindowCursor.SizeAll:
                        UIComponent.Cursor = Cursors.SizeAll;
                        break;
                    case GameWindowCursor.SizeNWSE:
                        UIComponent.Cursor = Cursors.SizeNWSE;
                        break;
                    case GameWindowCursor.SizeEWE:
                        UIComponent.Cursor = Cursors.SizeEWE;
                        break;
                    case GameWindowCursor.SizeNESW:
                        UIComponent.Cursor = Cursors.SizeNESW;
                        break;
                    case GameWindowCursor.SizeNS:
                        UIComponent.Cursor = Cursors.SizeNS;
                        break;
                    case GameWindowCursor.UpArrow:
                        UIComponent.Cursor = Cursors.UpArrow;
                        break;
                    case GameWindowCursor.Wait:
                        UIComponent.Cursor = Cursors.Wait;
                        break;
                }
            }
        }
        
        /// <summary>
        /// Defines is <see cref="GameOutput"/> currently displayed
        /// </summary>
        public override bool IsVisible => UIComponent.Visibility == Visibility.Visible;

        internal override void Resize(int width, int height)
        {
            UIComponent.Width = width;
            UIComponent.Height = height;
        }
    }
}