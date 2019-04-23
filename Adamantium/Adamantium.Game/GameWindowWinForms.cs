using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;
using WFKeys = System.Windows.Forms.Keys;
using Keys = Adamantium.Engine.GameInput.Keys;

namespace Adamantium.Engine
{
    
    /// <summary>
    /// Represents <see cref="GameWindow"/> based on <see cref="Form"/> 
    /// </summary>
    public class GameWindowWinForms : GameWindow
    {
        private Form nativeWindow;
        private Rectangle clientBounds;
        private GameWindowCursor cursor;
        private Form renderForm;
        private static readonly Dictionary<WFKeys, GameInput.Keys> translationKeys;
        private Vector2F _previousMousePosition;
        private Cursor _transparentCursor;

        static GameWindowWinForms()
        {
            translationKeys = new Dictionary<WFKeys, Keys>();
            translationKeys[WFKeys.None] = Keys.None;
            translationKeys[WFKeys.Back] = Keys.Back;
            translationKeys[WFKeys.Tab] = Keys.Tab;
            translationKeys[WFKeys.Return] = Keys.Enter;
            translationKeys[WFKeys.Pause] = Keys.Pause;
            translationKeys[WFKeys.Capital] = Keys.CapsLock;
            translationKeys[WFKeys.KanaMode] = Keys.Kana;
            translationKeys[WFKeys.HanjaMode] = Keys.Kanji;
            translationKeys[WFKeys.Escape] = Keys.Escape;
            translationKeys[WFKeys.IMEConvert] = Keys.ImeConvert;
            translationKeys[WFKeys.IMENonconvert] = Keys.ImeNonConvert;
            translationKeys[WFKeys.Space] = Keys.Space;
            translationKeys[WFKeys.PageUp] = Keys.PageUp;
            translationKeys[WFKeys.Next] = Keys.PageDown;
            translationKeys[WFKeys.End] = Keys.End;
            translationKeys[WFKeys.Home] = Keys.Home;
            translationKeys[WFKeys.Left] = Keys.LeftArrow;
            translationKeys[WFKeys.Up] = Keys.UpArrow;
            translationKeys[WFKeys.Right] = Keys.RightArrow;
            translationKeys[WFKeys.Down] = Keys.DownArrow;
            translationKeys[WFKeys.Select] = Keys.Select;
            translationKeys[WFKeys.Print] = Keys.Print;
            translationKeys[WFKeys.Execute] = Keys.Execute;
            translationKeys[WFKeys.PrintScreen] = Keys.PrintScreen;
            translationKeys[WFKeys.Insert] = Keys.Insert;
            translationKeys[WFKeys.Delete] = Keys.Delete;
            translationKeys[WFKeys.Help] = Keys.Help;
            translationKeys[WFKeys.D0] = Keys.Digit0;
            translationKeys[WFKeys.D1] = Keys.Digit1;
            translationKeys[WFKeys.D2] = Keys.Digit2;
            translationKeys[WFKeys.D3] = Keys.Digit3;
            translationKeys[WFKeys.D4] = Keys.Digit4;
            translationKeys[WFKeys.D5] = Keys.Digit5;
            translationKeys[WFKeys.D6] = Keys.Digit6;
            translationKeys[WFKeys.D7] = Keys.Digit7;
            translationKeys[WFKeys.D8] = Keys.Digit8;
            translationKeys[WFKeys.D9] = Keys.Digit9;
            translationKeys[WFKeys.A] = Keys.A;
            translationKeys[WFKeys.B] = Keys.B;
            translationKeys[WFKeys.C] = Keys.C;
            translationKeys[WFKeys.D] = Keys.D;
            translationKeys[WFKeys.E] = Keys.E;
            translationKeys[WFKeys.F] = Keys.F;
            translationKeys[WFKeys.G] = Keys.G;
            translationKeys[WFKeys.H] = Keys.H;
            translationKeys[WFKeys.I] = Keys.I;
            translationKeys[WFKeys.J] = Keys.J;
            translationKeys[WFKeys.K] = Keys.K;
            translationKeys[WFKeys.L] = Keys.L;
            translationKeys[WFKeys.M] = Keys.M;
            translationKeys[WFKeys.N] = Keys.N;
            translationKeys[WFKeys.O] = Keys.O;
            translationKeys[WFKeys.P] = Keys.P;
            translationKeys[WFKeys.Q] = Keys.Q;
            translationKeys[WFKeys.R] = Keys.R;
            translationKeys[WFKeys.S] = Keys.S;
            translationKeys[WFKeys.T] = Keys.T;
            translationKeys[WFKeys.U] = Keys.U;
            translationKeys[WFKeys.V] = Keys.V;
            translationKeys[WFKeys.W] = Keys.W;
            translationKeys[WFKeys.X] = Keys.X;
            translationKeys[WFKeys.Y] = Keys.Y;
            translationKeys[WFKeys.Z] = Keys.Z;
            translationKeys[WFKeys.LWin] = Keys.LeftWindows;
            translationKeys[WFKeys.RWin] = Keys.RightWindows;
            translationKeys[WFKeys.Apps] = Keys.Apps;
            translationKeys[WFKeys.Sleep] = Keys.Sleep;
            translationKeys[WFKeys.NumPad0] = Keys.NumPad6;
            translationKeys[WFKeys.NumPad1] = Keys.NumPad1;
            translationKeys[WFKeys.NumPad2] = Keys.NumPad2;
            translationKeys[WFKeys.NumPad3] = Keys.NumPad3;
            translationKeys[WFKeys.NumPad4] = Keys.NumPad4;
            translationKeys[WFKeys.NumPad5] = Keys.NumPad5;
            translationKeys[WFKeys.NumPad6] = Keys.NumPad6;
            translationKeys[WFKeys.NumPad7] = Keys.NumPad7;
            translationKeys[WFKeys.NumPad8] = Keys.NumPad8;
            translationKeys[WFKeys.NumPad9] = Keys.NumPad9;
            translationKeys[WFKeys.Multiply] = Keys.Multiply;
            translationKeys[WFKeys.Add] = Keys.Add;
            translationKeys[WFKeys.Separator] = Keys.Separator;
            translationKeys[WFKeys.Subtract] = Keys.Subtract;
            translationKeys[WFKeys.Decimal] = Keys.Decimal;
            translationKeys[WFKeys.Divide] = Keys.Divide;
            translationKeys[WFKeys.F1] = Keys.F1;
            translationKeys[WFKeys.F2] = Keys.F2;
            translationKeys[WFKeys.F3] = Keys.F3;
            translationKeys[WFKeys.F4] = Keys.F4;
            translationKeys[WFKeys.F5] = Keys.F5;
            translationKeys[WFKeys.F6] = Keys.F6;
            translationKeys[WFKeys.F7] = Keys.F7;
            translationKeys[WFKeys.F8] = Keys.F8;
            translationKeys[WFKeys.F9] = Keys.F9;
            translationKeys[WFKeys.F10] = Keys.F10;
            translationKeys[WFKeys.F11] = Keys.F11;
            translationKeys[WFKeys.F12] = Keys.F12;
            translationKeys[WFKeys.F13] = Keys.F13;
            translationKeys[WFKeys.F14] = Keys.F14;
            translationKeys[WFKeys.F15] = Keys.F15;
            translationKeys[WFKeys.F16] = Keys.F16;
            translationKeys[WFKeys.F17] = Keys.F17;
            translationKeys[WFKeys.F18] = Keys.F18;
            translationKeys[WFKeys.F19] = Keys.F19;
            translationKeys[WFKeys.F20] = Keys.F20;
            translationKeys[WFKeys.F21] = Keys.F21;
            translationKeys[WFKeys.F22] = Keys.F22;
            translationKeys[WFKeys.F23] = Keys.F23;
            translationKeys[WFKeys.F24] = Keys.F24;
            translationKeys[WFKeys.NumLock] = Keys.NumLock;
            translationKeys[WFKeys.Scroll] = Keys.ScrollLock;
            translationKeys[WFKeys.LShiftKey] = Keys.LeftShift;
            translationKeys[WFKeys.RShiftKey] = Keys.RightShift;
            translationKeys[WFKeys.LControlKey] = Keys.LeftControl;
            translationKeys[WFKeys.RControlKey] = Keys.RightControl;
            translationKeys[WFKeys.LMenu] = Keys.LeftAlt;
            translationKeys[WFKeys.RMenu] = Keys.RightAlt;
            translationKeys[WFKeys.BrowserBack] = Keys.BrowserBack;
            translationKeys[WFKeys.BrowserForward] = Keys.BrowserForward;
            translationKeys[WFKeys.BrowserRefresh] = Keys.BrowserRefresh;
            translationKeys[WFKeys.BrowserStop] = Keys.BrowserStop;
            translationKeys[WFKeys.BrowserSearch] = Keys.BrowserSearch;
            translationKeys[WFKeys.BrowserFavorites] = Keys.BrowserFavorites;
            translationKeys[WFKeys.BrowserHome] = Keys.BrowserHome;
            translationKeys[WFKeys.VolumeMute] = Keys.VolumeMute;
            translationKeys[WFKeys.VolumeDown] = Keys.VolumeDown;
            translationKeys[WFKeys.VolumeUp] = Keys.VolumeUp;
            translationKeys[WFKeys.MediaNextTrack] = Keys.MediaNextTrack;
            translationKeys[WFKeys.MediaPreviousTrack] = Keys.MediaPreviousTrack;
            translationKeys[WFKeys.MediaStop] = Keys.MediaStop;
            translationKeys[WFKeys.MediaPlayPause] = Keys.MediaPlayPause;
            translationKeys[WFKeys.LaunchMail] = Keys.LaunchMail;
            translationKeys[WFKeys.SelectMedia] = Keys.SelectMedia;
            translationKeys[WFKeys.LaunchApplication1] = Keys.LaunchApplication1;
            translationKeys[WFKeys.LaunchApplication2] = Keys.LaunchApplication2;
            translationKeys[WFKeys.Oem1] = Keys.OemSemicolon;
            translationKeys[WFKeys.Oemplus] = Keys.OemPlus;
            translationKeys[WFKeys.Oemcomma] = Keys.OemComma;
            translationKeys[WFKeys.OemMinus] = Keys.OemMinus;
            translationKeys[WFKeys.OemPeriod] = Keys.OemPeriod;
            translationKeys[WFKeys.OemQuestion] = Keys.OemQuestion;
            translationKeys[WFKeys.Oemtilde] = Keys.OemTilde;
            translationKeys[WFKeys.OemOpenBrackets] = Keys.OemOpenBrackets;
            translationKeys[WFKeys.Oem5] = Keys.OemPipe;
            translationKeys[WFKeys.Oem6] = Keys.OemCloseBrackets;
            translationKeys[WFKeys.Oem7] = Keys.OemQuotes;
            translationKeys[WFKeys.Oem8] = Keys.Oem8;
            translationKeys[WFKeys.OemBackslash] = Keys.OemBackslash;
            translationKeys[WFKeys.ProcessKey] = Keys.ProcessKey;
            translationKeys[WFKeys.Attn] = Keys.Attn;
            translationKeys[WFKeys.Crsel] = Keys.Crsel;
            translationKeys[WFKeys.Exsel] = Keys.Exsel;
            translationKeys[WFKeys.EraseEof] = Keys.EraseEof;
            translationKeys[WFKeys.Play] = Keys.Play;
            translationKeys[WFKeys.Zoom] = Keys.Zoom;
            translationKeys[WFKeys.Pa1] = Keys.Pa1;
            translationKeys[WFKeys.OemClear] = Keys.OemClear;
            translationKeys[WFKeys.ShiftKey] = Keys.Shift;
            translationKeys[WFKeys.ControlKey] = Keys.Control;
            translationKeys[WFKeys.Menu] = Keys.Alt;

        }

        private void CreateTransparentCursor()
        {
            byte[] cursorAndMask = { 0xFF };
            byte[] cursorOrMask = { 0x00 };
            var transparentCursor = Interop.CreateCursor(IntPtr.Zero, 0, 0, 1, 1, cursorAndMask, cursorOrMask);
            _transparentCursor = new Cursor(transparentCursor);
        }

        internal GameWindowWinForms(GameContext context)
        {
            Initialize(context);
        }

        internal GameWindowWinForms(GameContext context, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel)
        {
            Initialize(context, pixelFormat, depthFormat, msaaLevel);
        }

        protected override void Initialize(GameContext context)
        {
            InitializeInternal(context);
        }

        protected override void Initialize(GameContext context, SurfaceFormat pixelFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.X4)
        {
            InitializeInternal(context);
            Description.PixelFormat = pixelFormat;
            Description.DepthFormat = depthFormat;
            Description.MSAALevel = msaaLevel;
        }

        private void InitializeInternal(GameContext context)
        {
            GameContext = context;
            nativeWindow = (Form)context.Context;
            RawInputDevice.RegisterDevice(HIDUsagePage.Generic, HIDUsageId.Mouse, InputDeviceFlags.None, IntPtr.Zero);
            RawInputDevice.MouseInput += RawInputDevice_MouseInput;

            renderForm = nativeWindow;
            if (renderForm != null)
            {
                renderForm.Text = Name;
            }

            CreateTransparentCursor();
            Interop.GetCursorPos(out NativePoint point);
            Interop.ScreenToClient(nativeWindow.Handle, ref point);
            _previousMousePosition = Vector2F.Zero;
            cursor = DefaultCursor;
            Description = new GameWindowDescription(PresenterType.Swapchain);
            Handle = nativeWindow.Handle;
            UpdateWindowBounds();
            
            nativeWindow.ClientSizeChanged += NativeWindowClientSizeChanged;
            nativeWindow.LocationChanged += NativeWindow_LocationChanged;
            nativeWindow.GotFocus += NativeWindowGotFocus;
            nativeWindow.LostFocus += NativeWindowLostFocus;
            nativeWindow.MouseDown += NativeWindow_MouseDown;
            nativeWindow.MouseUp += NativeWindow_MouseUp;
            nativeWindow.KeyDown += NativeWindow_KeyDown;
            nativeWindow.KeyUp += NativeWindow_KeyUp;
            nativeWindow.MouseWheel += NativeWindow_MouseWheel;
            
            nativeWindow.Disposed += NativeWindow_Disposed;
            RawInputDeviceExtension.AddMessageFilter(nativeWindow.Handle);
        }

        private void NativeWindow_KeyUp(object sender, KeyEventArgs e)
        {
            HandleKeyboardInput(InputType.Up, e.KeyCode);
        }

        private void NativeWindow_KeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyboardInput(InputType.Down, e.KeyCode);
        }

        private void RawInputDevice_MouseInput(object sender, RawMouseInputEventArgs e)
        {
            HandleMouseInput(InputType.RawDelta, MouseButtons.None, new Vector2F(e.DeltaX, e.DeltaY));
        }

        private void NativeWindow_MouseUp(object sender, MouseEventArgs e)
        {
            HandleMouseInput(InputType.Up, e.Button, Vector2F.Zero);
        }

        private void NativeWindow_MouseDown(object sender, MouseEventArgs e)
        {
            HandleMouseInput(InputType.Down,  e.Button, Vector2F.Zero);
        }

        private void NativeWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            HandleMouseInput(InputType.Wheel, e.Button, Vector2F.Zero, e.Delta);
        }

        private void HandleMouseInput(InputType type, MouseButtons button, Vector2F delta, int wheelDelta = 0)
        {
            MouseInput input = new MouseInput();
            input.InputType = type;
            input.Button = GetMouseButtonButton(button);
            if (type == InputType.RawDelta)
            {
                input.Delta = delta;
            }
            else if (type == InputType.Wheel)
            {
                input.WheelDelta = wheelDelta;
            }

            MouseInputEventArgs args = new MouseInputEventArgs(input);
            switch (type)
            {
                case InputType.Up:
                    OnMouseUp(args);
                    break;
                case InputType.Down:
                    OnMouseDown(args);
                    break;
                case InputType.Wheel:
                    OnMouseWheel(args);
                    break;
                case InputType.RawDelta:
                    if (input.Delta.X != 0 || input.Delta.Y != 0)
                    {
                        OnMouseDelta(args);
                    }
                    break;
            }
        }

        private void HandleKeyboardInput(InputType type, WFKeys button)
        {
            if (translationKeys.TryGetValue(button, out Keys key))
            {
                KeyboardInput input = new KeyboardInput();
                input.Key = key;
                input.InputType = type;

                KeyboardInputEventArgs args = new KeyboardInputEventArgs(input);
                switch (type)
                {
                    case InputType.Up:
                        OnKeyUp(args);
                        break;

                    case InputType.Down:
                        OnKeyDown(args);
                        break;
                }
            }
        }

        private MouseButton GetMouseButtonButton(MouseButtons button)
        {
            switch (button)
            {
                case MouseButtons.Left:
                    return MouseButton.Left;
                case MouseButtons.Right:
                    return MouseButton.Right;
                case MouseButtons.Middle:
                    return MouseButton.Middle;
                case MouseButtons.XButton1:
                    return MouseButton.XButton1;
                case MouseButtons.XButton2:
                    return MouseButton.XButton2;
            }

            return MouseButton.None;
        }

        internal void UpdateWindowBounds()
        {
            int borderWidth = (nativeWindow.Width - nativeWindow.ClientSize.Width) / 2;
            int titleBarHeight = nativeWindow.Height - nativeWindow.ClientSize.Height - 2 * borderWidth;
            clientBounds = new Rectangle(nativeWindow.Location.X + borderWidth, nativeWindow.Location.Y + titleBarHeight + borderWidth, nativeWindow.ClientSize.Width, nativeWindow.ClientSize.Height);
            OnWindowBoundsChanged();
            Width = nativeWindow.ClientSize.Width;
            Height = nativeWindow.ClientSize.Height;
        }

        private void NativeWindow_LocationChanged(object sender, EventArgs e)
        {
            UpdateWindowBounds();
        }

        private void NativeWindow_Disposed(object sender, EventArgs e)
        {
            OnClosed();
        }

        protected override GameWindowDescription Description { get; set; }

        public override Rectangle ClientBounds => clientBounds;
        public override object NativeWindow => nativeWindow;

        public override bool IsVisible
        {
            get => nativeWindow.Visible;
            set => nativeWindow.Visible = value;
        }

        public override GameWindowCursor Cursor
        {
            get => cursor;
            set
            {
                nativeWindow.Invoke(
                    new MethodInvoker(delegate ()
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
                                nativeWindow.Cursor = Cursors.Cross;
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
                            case GameWindowCursor.SizeAll:
                                nativeWindow.Cursor = Cursors.SizeAll;
                                break;
                            case GameWindowCursor.SizeNWSE:
                                nativeWindow.Cursor = Cursors.SizeNWSE;
                                break;
                            case GameWindowCursor.SizeEWE:
                                nativeWindow.Cursor =
                                    new Cursor(Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeEWE));
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
                                nativeWindow.Cursor = Cursors.WaitCursor;
                                break;
                            case GameWindowCursor.None:
                                nativeWindow.Cursor = _transparentCursor;
                                break;
                        }
                    }));
            }
        }

        private void NativeWindowLostFocus(object sender, EventArgs e)
        {
            OnDeactivated();
        }

        private void NativeWindowGotFocus(object sender, EventArgs e)
        {
            Debug.WriteLine("window " + Name + " got focus");
            OnActivated();
        }

        private void NativeWindowClientSizeChanged(object sender, EventArgs e)
        {
            if (renderForm?.WindowState == FormWindowState.Minimized)
            {
                return;
            }
            UpdateWindowBounds();
        }

        internal override bool CanHandle(GameContext gameContext)
        {
            return gameContext.ContextType == GameContextType.WinForms;
        }

        internal override void Show()
        {
            nativeWindow.Invoke(new Action(() => nativeWindow.Show()));
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
                nativeWindow.SizeChanged -= NativeWindowClientSizeChanged;
                nativeWindow.GotFocus -= NativeWindowGotFocus;
                nativeWindow.LostFocus -= NativeWindowLostFocus;
                nativeWindow.MouseDown -= NativeWindow_MouseDown;
                nativeWindow.MouseUp -= NativeWindow_MouseUp;
                nativeWindow.MouseWheel -= NativeWindow_MouseWheel;
                nativeWindow.KeyDown -= NativeWindow_KeyDown;
                nativeWindow.KeyUp -= NativeWindow_KeyUp;
                RawInputDeviceExtension.RemoveMessageFilter(nativeWindow.Handle);
                nativeWindow.Dispose();
                Initialize(context);
            }
        }

        internal override GraphicsPresenter CreatePresenter(D3DGraphicsDevice device)
        {
            Presenter = new SwapChainGraphicsPresenter(device, Description.ToPresentationParameters(), GeneratePresenterName());
            return Presenter;
        }
    }
    
}
