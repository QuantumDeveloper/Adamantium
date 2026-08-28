using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Internals;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Input.Raw;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

internal class Win32WindowWorker : AdamantiumComponent, IWindowWorkerService
{
    //private WindowBase window;
    private IWindow window;
    private readonly Dictionary<uint, HandleMessage> messageTable;

    private bool isOverSizeFrame;
    private bool trackMouse;
    private bool osMouseCaptured;
    private Win32NativeWindowWrapper source;

    // RELATIVE mouse mode (game mouse-look). SetRelativeMouseMode posts RelativeModeMessage (enable in wParam, the restore
    // screen point packed in lParam) so the cursor hide/show + capture - HWND-thread-affine - run on the PUMP thread. While
    // active, HandleMouseMove reads the physical cursor, feeds a RawMouseMove delta and re-centres to _recenterScreen so the
    // delta never runs out at the window edge. The cursor's SAVED position is NOT held here - the panel owns it (in its own
    // coords) and hands the screen point back on release. See RenderTargetPanel.
    private bool _relativeActive;
    private NativePoint _recenterScreen;

    // PLAIN-FIELD snapshots of the window state the WndProc handlers need. The WndProc runs on the OS message (pump)
    // thread; reading an AdamantiumProperty there calls GetValue -> Monitor.Enter, which DEADLOCKS against the loop
    // thread when it is mid-SetValue (holding the component lock) and blocked in a cross-thread ShowWindow. So the
    // handlers read these instead. Updated on the loop thread (benign torn read of a bool/enum).
    private bool chromeCustom;
    private WindowResizeMode chromeResizeMode;
    private WindowState chromeState;
    private double chromeMinWidth;    // DIP; the WM_GETMINMAXINFO floor so the caption can't be crushed
    private double chromeMinHeight;

    static Win32WindowWorker()
    {
        // Declare the process Per-Monitor-DPI-Aware V2 before any window exists (this static ctor runs once, before the
        // first worker instance = before the first HWND). PMv2 => the OS stops bitmap-stretching our frames and sends
        // WM_DPICHANGED when the window crosses monitors; we scale the render ourselves (docs/PER_MONITOR_DPI_PLAN.md).
        try { Win32Interop.SetProcessDpiAwarenessContext(Win32Interop.DpiAwarenessContextPerMonitorAwareV2); }
        catch { /* pre-1703 OS without the API, or awareness already pinned by a manifest - ignore */ }
    }

    public Win32WindowWorker(IUIContext uiContext)
    {
        UIContext = uiContext;

        // What the OS is asking for RIGHT NOW, before any window is up: a theme set to follow the system must open in
        // the right appearance, not correct itself after the first change notification arrives.
        Core.Resources.SystemAppearance.PrefersDark = Win32Interop.SystemPrefersDarkAppearance();


        messageTable = new Dictionary<uint, HandleMessage>();
        messageTable[(uint)WindowMessages.Activate] = HandleActivate;
        messageTable[(uint)WindowMessages.Syscommand] = HandleSysCommand;
        messageTable[(uint)WindowMessages.Nclbuttondown] = HandleNcButtonDown;
        messageTable[(uint)WindowMessages.Nchittest] = HandleNcHittest;
        messageTable[(uint)WindowMessages.Nccalcsize] = HandleNcCalcSize;
        messageTable[(uint)WindowMessages.Getminmaxinfo] = HandleGetMinMaxInfo;
        messageTable[(uint)WindowMessages.Size] = HandleResize;
        messageTable[(uint)WindowMessages.Moving] = HandleMoving;
        messageTable[(uint)WindowMessages.Exitsizemove] = HandleExitSizeMove;
        messageTable[(uint)WindowMessages.Dpichanged] = HandleDpiChanged;
        messageTable[(uint)WindowMessages.Settingchange] = HandleSettingChange;
        messageTable[(uint)WindowMessages.Keydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Syskeydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Keyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Syskeyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Char] = HandleChar;
        messageTable[(uint)WindowMessages.Mousemove] = HandleMouseMove;
        messageTable[(uint)WindowMessages.Mouseleave] = HandleMouseLeave;
        messageTable[(uint)WindowMessages.Capturechanged] = HandleCaptureChanged;
        messageTable[(uint)WindowMessages.LeftButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttondown] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.LeftButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttonup] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.LeftButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttondblclk] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.MouseWheel] = HandleMouseWheel;
        messageTable[(uint)WindowMessages.MouseHwheel] = HandleMouseWheel;   // horizontal (tilt) wheel
        messageTable[(uint)WindowMessages.Setcursor] = HandleSetCursor;
        messageTable[BeginMoveDragMessage] = HandleBeginMoveDrag;
        messageTable[RelativeModeMessage] = HandleRelativeModeMessage;
    }

    // App-private message (WM_APP range) the loop thread posts to hand a caption drag BACK to the HWND-owning thread.
    // The classic ReleaseCapture + WM_NCLBUTTONDOWN/HTCAPTION trick MUST run on the thread that owns the window's input
    // queue, else the native move loop reads no held button and returns instantly. Input routing (where a drag is decided)
    // runs on the loop thread, so BeginMoveDrag PostMessages this; CustomWndProc then runs HandleBeginMoveDrag on the
    // owner thread. lParam carries the packed screen cursor position (LOWORD x, HIWORD y).
    private const uint BeginMoveDragMessage = (uint)WindowMessages.App + 1;

    // App-private message the loop thread posts (SetRelativeMouseMode) so the enter/exit of relative mouse mode - which
    // hides/shows the cursor and takes/drops the OS capture, both thread-affine to the HWND owner - runs on the pump thread.
    private const uint RelativeModeMessage = (uint)WindowMessages.App + 2;

    // Initial OS-window extent: prefer the explicit Width/Height; if unset, fall back to the requested CLIENT size (a
    // window can be sized by its client area alone - e.g. one opened from a view model), then a sane default so it is
    // never created at 0. For a custom-chrome (borderless) window the client area == the window, so this is exact.
    private static int InitialExtent(double preferred, double clientFallback, int defaultExtent)
    {
        if (!double.IsNaN(preferred) && preferred > 0) return (int)preferred;
        if (!double.IsNaN(clientFallback) && clientFallback > 0) return (int)clientFallback;
        return defaultExtent;
    }

    public void SetWindow(IWindow window)
    {
        this.window = window;
        // Snapshot the chrome config (read once here, on the setup thread, before the loop runs) so the WndProc never
        // has to touch a lockable AdamantiumProperty. State is kept current by WindowOnStateChanged / HandleSysCommand.
        chromeCustom = window.UseCustomChrome;
        chromeResizeMode = window.ResizeMode;
        chromeState = window.State;
        chromeMinWidth = !double.IsNaN(window.MinWidth) && window.MinWidth > 0 ? window.MinWidth : 320;
        chromeMinHeight = !double.IsNaN(window.MinHeight) && window.MinHeight > 0 ? window.MinHeight : 120;
        // What the caller ASKED for, in logical units, kept before creation overwrites it. The window has to be created
        // before its monitor - and so its scale - is known, so the geometry is asked for again once it is (below).
        var requestedLeft = window.Left;
        var requestedTop = window.Top;
        var requestedClientWidth = window.ClientWidth;
        var requestedClientHeight = window.ClientHeight;
        this.window.Closed += OnWindowClosed;
        var classStyle = WindowClassStyle.OwnDC | WindowClassStyle.DoubleClicks; //| WindowClassStyle.VerticalRedraw | WindowClassStyle.HorizontalRedraw;
        // No WS_EX_ACCEPTFILES: the partial WM_DROPFILES path is replaced by a full OLE drop target (registered below),
        // which carries text and arbitrary formats too - and reports drag-over live instead of only the final drop.
        var wndStyleEx = WindowStyleEx.Appwindow;
        var wndStyle = //WindowStyle.Popup |
            WindowStyle.Overlappedwindow | WindowStyle.Maximizebox | WindowStyle.Minimizebox |
            WindowStyle.Clipsiblings | WindowStyle.Clipchildren | WindowStyle.Sizeframe;

        // Overlay traits, read here with the rest: native styles are fixed at creation.
        if (window.WindowOpacity < 1.0) wndStyleEx |= WindowStyleEx.Layered;
        if (window.Topmost) wndStyleEx |= WindowStyleEx.Topmost;
        if (window.TransparentToInput) wndStyleEx |= WindowStyleEx.Transparent;
        if (!window.ActivateOnShow)
        {
            // Toolwindow as well: an overlay has no business in the task bar or the Alt-Tab list.
            wndStyleEx |= WindowStyleEx.Noactivate | WindowStyleEx.Toolwindow;
            wndStyleEx &= ~WindowStyleEx.Appwindow;
        }
        source = new Win32NativeWindowWrapper(
            classStyle,
            wndStyleEx,
            wndStyle,
            (int)window.Left,
            (int)window.Top,
            InitialExtent(window.Width, window.ClientWidth, 800),
            InitialExtent(window.Height, window.ClientHeight, 600),
            IntPtr.Zero);
        this.window.SetHandle(source.Handle);
        
        if (source.Handle == IntPtr.Zero) 
            return;
        
        this.window.SetSurfaceHandle(source.Handle);

        ApplyTransparency();

        source.AddHook(CustomWndProc);

        // The HWND was created BEFORE our hook was attached, so our WM_NCCALCSIZE never ran during creation. For custom
        // chrome, force a frame re-calc now (FRAMECHANGED re-sends WM_NCCALCSIZE) so the borderless client takes effect.
        if (chromeCustom)
        {
            Win32Interop.SetWindowPos(source.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SetWindowPosFlags.Nomove | SetWindowPosFlags.Nosize | SetWindowPosFlags.Nozorder |
                SetWindowPosFlags.Noactivate | SetWindowPosFlags.Framechanged);

            // The shadow and the accent outline are what a WINDOW looks like, and not every window wants to look like
            // one. Asked of the property that MEANS this, not inferred from how the window happens to be composed:
            // see-through and framed, or opaque and frameless, are both things somebody may want.
            if (window.ShowWindowBorder)
            {
                // Real OS drop shadow on the borderless window: extend the DWM frame 1px into the client. With
                // WM_NCCALCSIZE keeping the window client-only, this is the standard WindowChrome trick - DWM draws the
                // window's ambient shadow (and Aero Snap preview) around it without a visible frame.
                var shadowMargins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                Win32Interop.DwmExtendFrameIntoClientArea(source.Handle, ref shadowMargins);

                // Windows 11 native accent border: DWMWA_BORDER_COLOR (34) = a COLORREF (0x00BBGGRR). DWM draws a crisp
                // 1px border around the window in that colour - no extra windows, resize-free. A no-op on Windows 10
                // (the call just returns a failing HRESULT, which we ignore). Colour follows the THEME accent.
                var accentBorder = AccentColorRef();
                Win32Interop.DwmSetWindowAttribute(source.Handle, 34, ref accentBorder, sizeof(int));
            }
        }

        this.window.DpiScale = ReadDpiScale(source.Handle);   // initial per-monitor DPI (PMv2)

        // The window was created with the requested geometry handed straight to Win32 - which reads it as PHYSICAL
        // pixels, while a window's position and size are LOGICAL. On a scaled display that made every window come out
        // 1/scale too small and too close to the origin, and the line below then quietly rewrote ClientWidth to match, so
        // the number the caller asked for simply vanished. The real scale is known only now (it belongs to the monitor
        // the window landed on), so ask again with it. A no-op at 100%, where the two units are the same number.
        SetPosition(requestedLeft, requestedTop);
        SetSize(requestedClientWidth, requestedClientHeight);

        Win32Interop.GetClientRect(window.Handle, out var client);
        // ClientWidth/Height are LOGICAL (DIP) = physical px / DPI scale (per-axis). The renderer sizes the swapchain
        // back up by RenderScale (= DpiScale); the projection + layout stay logical. On a 100% monitor this is identity.
        this.window.ClientWidth = client.Width / this.window.DpiScale.X;
        this.window.ClientHeight = client.Height / this.window.DpiScale.Y;

        // Accept drops from OTHER applications for this window's whole life - registered here, on the thread that owns
        // the HWND (OLE requires that), never per drag. Silently does nothing when the OS bridge is unavailable.
        Input.DragDrop.RegisterNativeDropTarget(this.window);

        UIContext.ThemeEngine.ApplyCurrentTheme(this.window);
        UIContext.UIApplication.AddWindow(this.window);
                
        this.window.OnSourceInitialized();
        this.window.StateChanged += WindowOnStateChanged;
        this.window.ResizeModeChanged += WindowOnResizeModeChanged;
    }

    // Runtime ResizeMode change (e.g. toggling grip-resize): refresh the hit-test snapshot. Fires on the loop thread (the
    // property is set there), so reading window.ResizeMode is safe - not the OS message thread that must never lock it.
    private void WindowOnResizeModeChanged(object sender, EventArgs e) => chromeResizeMode = window.ResizeMode;

    // The current theme's accent as a Win32 COLORREF (0x00BBGGRR) for the DWM window border. Falls back to the Fluent
    // accent if the theme exposes no solid accent brush.
    private static int AccentColorRef()
    {
        if (UIAppContext.Current?.ThemeManager?.CurrentTheme?.AccentColor is SolidColorBrush accent)
        {
            var c = accent.Color;
            return c.R | (c.G << 8) | (c.B << 16);
        }
        return 0x00F79100;
    }

    public void SetTitle(string title)
    {
        if (window == null) return;

        Win32Interop.SetWindowText(window.Handle, title);
    }

    public void SetPosition(double left, double top)
    {
        if (window == null) return;

        // Nosize keeps the current size, Nozorder the current stacking, Noactivate the current focus - a move is only a
        // move. CreateWindowExW placed this window by the same origin (the WINDOW rect, non-client included), so the
        // coordinates mean here exactly what they meant there.
        // PHYSICAL desktop pixels, deliberately - unlike the size, which is logical. A window's position has no scale of
        // its own to be logical in: the scale belongs to the monitor the point falls on, and which monitor that is can
        // only be known once the point is physical. Measured: a torn-off window placed in logical units was born at the
        // origin (primary monitor, 100%), took ITS scale, and landed at cursor/1.5 on a 4K display - far to the left.
        Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, (int)Math.Round(left), (int)Math.Round(top), 0, 0,
            SetWindowPosFlags.Nosize | SetWindowPosFlags.Nozorder | SetWindowPosFlags.Noactivate);
    }

    // True only while the OS's own size is being written onto the window (see HandleResize): those writes report, they
    // do not request, and answering them is how a window ends up arguing with itself.
    private bool _reportingOsSize;

    public void SetSize(double clientWidth, double clientHeight)
    {
        if (window == null || _reportingOsSize || double.IsNaN(clientWidth) || double.IsNaN(clientHeight)) return;

        // Maximized or minimized, the size is the OS's to decide - and it reports it back as ClientWidth/Height, which
        // arrives here. Answering would be arguing with the state the user just asked for.
        if (window.State != WindowState.Normal) return;

        // Logical (DIP) in, physical client px out - the same scale WM_SIZE divides by on the way back.
        var width = (int)Math.Round(clientWidth * window.DpiScale.X);
        var height = (int)Math.Round(clientHeight * window.DpiScale.Y);
        if (width <= 0 || height <= 0) return;

        // Already that size? Then say nothing. The OS reports every resize back as ClientWidth/Height, which lands here
        // again - and a SetWindowPos per echo would recreate the swapchain for a size that never changed.
        Win32Interop.GetClientRect(window.Handle, out var client);
        if (client.Width == width && client.Height == height) return;

        // SetWindowPos speaks WINDOW rects, so the client size has to be grown by whatever sits around it. MEASURED off
        // this very window (window rect minus client rect), never computed from its styles: a window with custom chrome
        // answers WM_NCCALCSIZE so that its client area IS the whole window, while AdjustWindowRect - which only knows
        // the styles - still swears there is a caption and a border. Adding that phantom frame made the window a few
        // pixels bigger, the OS reported the new size back, and it grew again on the next frame, every frame.
        Win32Interop.GetWindowRect(window.Handle, out var outer);
        var frameWidth = outer.Width - client.Width;
        var frameHeight = outer.Height - client.Height;

        Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, 0, 0, width + frameWidth, height + frameHeight,
            SetWindowPosFlags.Nomove | SetWindowPosFlags.Nozorder | SetWindowPosFlags.Noactivate);
    }

    /// <summary>Puts the current overlay traits onto the live window: the extended styles are rewritten, the z-order
    /// band is set, and the layered attributes are pushed again. Everything here works on an open window, which is the
    /// point - these are properties, not construction arguments.</summary>
    public void UpdateOverlayTraits()
    {
        if (source == null || source.Handle == IntPtr.Zero) return;

        var style = (WindowStyleEx)Win32Interop.GetWindowLong(source.Handle, WindowLongType.Exstyle).ToInt64();

        style = Set(style, WindowStyleEx.Topmost, window.Topmost);
        style = Set(style, WindowStyleEx.Transparent, window.TransparentToInput);
        style = Set(style, WindowStyleEx.Layered, window.WindowOpacity < 1.0);
        style = Set(style, WindowStyleEx.Noactivate | WindowStyleEx.Toolwindow, !window.ActivateOnShow);
        style = Set(style, WindowStyleEx.Appwindow, window.ActivateOnShow);

        Win32Interop.SetWindowLong(source.Handle, WindowLongType.Exstyle, new IntPtr((long)style));

        // The topmost BAND is not part of the style - it is a z-order position, and only SetWindowPos moves a window
        // between bands. Setting WS_EX_TOPMOST alone leaves an open window exactly where it was.
        Win32Interop.SetWindowPos(source.Handle, window.Topmost ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0,
            SetWindowPosFlags.Nomove | SetWindowPosFlags.Nosize | SetWindowPosFlags.Noactivate |
            SetWindowPosFlags.Framechanged);

        ApplyTransparency();
        ApplyBorder();
    }

    /// <summary>Extends the DWM frame into the client (shadow + accent outline) or retracts it. Live, so the border can
    /// be turned off after the window is open instead of only before it exists.</summary>
    private void ApplyBorder()
    {
        if (source == null || source.Handle == IntPtr.Zero || !chromeCustom) return;

        var edge = window.ShowWindowBorder ? 1 : 0;
        var margins = new Margins { Left = edge, Right = edge, Top = edge, Bottom = edge };
        Win32Interop.DwmExtendFrameIntoClientArea(source.Handle, ref margins);

        // DWMWA_BORDER_COLOR = 34; 0xFFFFFFFE is DWMWA_COLOR_NONE - "draw no border at all".
        var border = window.ShowWindowBorder ? AccentColorRef() : unchecked((int)0xFFFFFFFE);
        Win32Interop.DwmSetWindowAttribute(source.Handle, 34, ref border, sizeof(int));
    }

    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    private static WindowStyleEx Set(WindowStyleEx style, WindowStyleEx bits, bool on)
        => on ? style | bits : style & ~bits;

    private void ApplyTransparency()
    {
        if (source == null || source.Handle == IntPtr.Zero) return;

        if (window.WindowOpacity >= 1.0) return;

        var alpha = (byte)Math.Clamp(window.WindowOpacity * 255, 0, 255);
        Win32Interop.SetLayeredWindowAttributes(source.Handle, 0, alpha, Win32Interop.LWA_ALPHA);
    }

    public void ShowWindow(WindowState windowState)
    {
        // An overlay must not take focus: showing it the ordinary way activates it, and activating something else in the
        // middle of a drag ends the drag it was put on screen to help with.
        var windowShowStyle = window.ActivateOnShow ? WindowShowStyle.Show : WindowShowStyle.ShowNoActivate;
        switch (windowState)
        {
            case WindowState.Maximized:
                windowShowStyle = WindowShowStyle.Maximize;
                break;
            case WindowState.Minimized:
                windowShowStyle = WindowShowStyle.Minimize;
                break;
        }
        Win32Interop.ShowWindow(source.Handle, windowShowStyle);
    }

    public void HideWindow()
    {
        Win32Interop.ShowWindow(source.Handle, WindowShowStyle.Hide);
    }

    public void Activate()
    {
        if (window == null || window.Handle == IntPtr.Zero) return;
        var hwnd = window.Handle;
        // Un-minimize first (Restore returns to the prior normal/maximized state); a minimized window can't be raised.
        if (chromeState == WindowState.Minimized)
            Win32Interop.ShowWindow(hwnd, WindowShowStyle.Restore);
        // Request foreground the STANDARD way. When the calling process isn't already the foreground one (launched from a
        // background process, e.g. a shell/launcher), Windows deliberately refuses to steal focus and just flashes the
        // taskbar - and that's correct. The old topmost-toggle (HWND_TOPMOST -> HWND_NOTOPMOST) forced it above everything,
        // which left the window stuck on top: a click on another window couldn't send it to the back. Don't force it.
        Win32Interop.SetForegroundWindow(hwnd);
    }

    // Z-order only, NO activation: SWP_NOACTIVATE is precisely what keeps the mouse capture alive, so a drag can raise
    // the window it is heading for instead of being cancelled by the OS revoking capture on a foreground change.
    public void RaiseWithoutActivation()
    {
        if (window == null || window.Handle == IntPtr.Zero) return;
        Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, 0, 0, 0, 0,
            SetWindowPosFlags.Nomove | SetWindowPosFlags.Nosize | SetWindowPosFlags.Noactivate | SetWindowPosFlags.Showwindow);
    }

    public IUIContext UIContext { get; }

    private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
    {
        // Minimizing via the custom title-bar button drives this managed path (Window.State = Minimized), NOT the
        // SC_MINIMIZE SysCommand path - so remember what we're minimizing FROM here too. Otherwise lastWindowState keeps
        // its stale default (Normal) and a later SC_RESTORE (un-minimize from the taskbar) restores to Normal, losing a
        // Maximized window's state (the "always comes back default" bug). Guard on the real transition so a redundant
        // Minimized->Minimized re-raise can't overwrite the saved pre-minimize state with Minimized itself.
        if (e.State == WindowState.Minimized && chromeState != WindowState.Minimized)
            lastWindowState = chromeState;
        chromeState = e.State;   // keep the WndProc snapshot current (this fires on every State change)
        Win32Interop.ShowWindow(window.Handle, ConvertStateToShowStyle(e.State));
    }

    private WindowShowStyle ConvertStateToShowStyle(WindowState state)
    {
        switch (state)
        {
            case WindowState.Normal:
                return WindowShowStyle.ShowNormal;
            case WindowState.Maximized:
                return WindowShowStyle.Maximize;
            case WindowState.Minimized:
                return WindowShowStyle.Minimize;
            default:
                return WindowShowStyle.ShowNormal;
        }
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        source.RemoveHook(CustomWndProc);
        UIContext.UIApplication.RemoveWindow(window);
        // Actually destroy the OS window. The custom-caption close raises Closed on the LOOP thread, but DestroyWindow must
        // run on the HWND's owning (pump/UI) thread - marshal it there (fire-and-forget; non-blocking from either thread).
        // Without this a secondary window's HWND lingered on screen with its WndProc hook already removed - a frozen,
        // never-closing window. (The native SC_CLOSE path used to destroy inline; that is now consolidated here.)
        if (window.Handle != IntPtr.Zero)
        {
            var closing = window;
            window.SetHandle(IntPtr.Zero);
            // Revoke the OLE drop target on the SAME (HWND-owning) thread and before the window dies - revoking after
            // DestroyWindow leaves the registration dangling on a handle the OS may hand to someone else.
            _ = UIContext.UIApplication.ExecuteOnUIThreadAsync(() =>
            {
                Input.DragDrop.UnregisterNativeDropTarget(closing);
                source.Destroy();
            });
        }
    }

    /// <summary>
    /// Window Procedure
    /// </summary>
    /// <param name="hWnd">windows handler</param>
    /// <param name="msg">window message (one of window messages)<see cref="WindowMessages"/>"/></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <param name="handled"></param>
    /// <returns></returns>
    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (messageTable.TryGetValue(msg, out var handler))
        {
            return handler((WindowMessages)msg, wParam, lParam, out handled);
        }

        return IntPtr.Zero;
    }

    // Window input arrives on the OS message thread. Raising the event below routes to controls and mutates/invalidates
    // the visual tree, which must NOT happen concurrently with the measure/arrange/render running on the loop thread.
    // The event args are built synchronously by the caller (capturing the message's transient state); only the raising
    // is marshalled onto the loop thread (drained at the start of Update). Order is preserved (a single FIFO queue).
    private static void DispatchInput(Action raise)
    {
        var dispatcher = Threading.Dispatcher.CurrentDispatcher;
        if (dispatcher != null) dispatcher.Post(raise);
        else raise();
    }

    // OS mouse messages carry PHYSICAL client px; the layout / hit-test work in logical DIP. Divide by the window's DPI
    // scale so the tracked position + reported GetPosition are logical (identity at 100%). Screen coords stay physical.
    private Vector2 ToLogical(Vector2 physicalClient)
    {
        var dpi = window.DpiScale;
        return new Vector2(physicalClient.X / dpi.X, physicalClient.Y / dpi.Y);
    }

    // A DESKTOP point out of a Win32 message. Named, so the physical->typed step is visible at the boundary rather than
    // an implicit conversion somewhere downstream.
    private static PixelPoint ToPixel(Vector2 screen) => new(screen.X, screen.Y);


    // The OS move loop (a caption drag, ours included via BeginMoveDrag) runs INSIDE the window procedure and swallows
    // the mouse - no managed move/up arrives while it lasts. These two messages are the only view we get of a gesture
    // that is otherwise invisible to us: WM_MOVING on every step, WM_EXITSIZEMOVE when the button comes up. That is what
    // lets a torn-off window be dropped back onto a tab strip - the drop is decided from where the window is, not from
    // input we cannot see. Marshalled to the loop thread like every other handler here: they reach the visual tree.
    private IntPtr HandleMoving(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        DispatchInput(() => window.RaiseWindowMoving());
        handled = false;   // let DefWindowProc go on moving the window
        return IntPtr.Zero;
    }

    private IntPtr HandleExitSizeMove(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // Where the OS actually left it. Left/Top otherwise still hold whatever WE last assigned - the move loop is
        // invisible to managed code - so anything that reads a window's position afterwards (a saved layout, say)
        // wrote down where it USED to be.
        Win32Interop.GetWindowRect(window.Handle, out var rect);
        DispatchInput(() =>
        {
            window.UpdatePositionFromPlatform(rect.Left, rect.Top);
            window.RaiseWindowMoveCompleted();
        });

        handled = false;
        return IntPtr.Zero;
    }

    private IntPtr HandleActivate(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var state = Messages.GetWindowActivationState(wParam);
        // Activation touches focus (FocusManager), the window's IsActive + the app's active-window - all loop-owned
        // managed state that must not be mutated straight off the OS message thread. Marshal it like the input handlers.
        switch (state)
        {
            case WindowActivation.Active:
            case WindowActivation.ClickActive:
                DispatchInput(HandleActivation);
                break;

            case WindowActivation.Inactive:
                DispatchInput(HandleDeactivation);
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleNcButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var ht = (NcHitTest)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
        window.StateChanged -= WindowOnStateChanged;
        switch (ht)
        {
            case NcHitTest.Close:
                window.Close();
                break;
            // case NcHitTest.Minbutton:
            //     window.State = WindowState.Minimized;
            //     break;
            // case NcHitTest.Maxbutton:
            //     window.State = WindowState.Maximized;
            //     break;
        }
        handled = true;
        window.StateChanged += WindowOnStateChanged;
        return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
    }

    private IntPtr HandleNcHittest(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // A window that declared itself TRANSPARENT TO INPUT must say so here, whatever chrome it has. WS_EX_TRANSPARENT
        // alone is not enough: the OS asks the window where the point landed, and a window that answers "my client area"
        // has claimed it - the style only decides what happens when nobody claims it.
        // Measured on the docking compass, an overlay that is nothing but a read-out: custom chrome made it answer
        // HTCLIENT everywhere, so it swallowed every click over the area it floats above and over any window under it.
        if (window.TransparentToInput)
        {
            handled = true;
            return (IntPtr)(int)NcHitTest.Transparent;
        }

        if (!chromeCustom)
        {
            // Native frame: let the OS decide the hit region; remember whether it's a sizing frame so HandleSetCursor
            // hands the resize cursor back to DefWindowProc.
            var native = Win32Interop.DefWindowProc(window.Handle, WindowMessages.Nchittest, wParam, lParam);
            var nativeHt = (NcHitTest)(Environment.Is64BitProcess ? native.ToInt64() : native.ToInt32());
            isOverSizeFrame = nativeHt != NcHitTest.Client;
            handled = false;
            return native;
        }

        // Custom chrome: WM_NCCALCSIZE removed the OS frame, so the OS reports HTCLIENT everywhere. Re-create the resize
        // borders ourselves (a margin along each edge -> HTLEFT/HTTOP/... so DefWindowProc still runs the native resize
        // loop + Aero Snap). Everything else is HTCLIENT: the managed content (title bar included) gets the mouse, and a
        // caption drag is a managed DragMove(). No resizing while maximized or when ResizeMode forbids it.
        var pt = Messages.PointFromLParam(lParam);   // SCREEN coordinates
        Win32Interop.GetWindowRect(window.Handle, out var r);

        var result = NcHitTest.Client;
        var canResize = chromeResizeMode == WindowResizeMode.CanResize && chromeState != WindowState.Maximized;
        if (canResize)
        {
            var margin = Win32Interop.GetSystemMetrics(SystemMetrics.Cxframe) +
                         Win32Interop.GetSystemMetrics(SystemMetrics.Cxpaddedborder);
            if (margin < 6) margin = 6;
            var left = pt.X < r.Left + margin;
            var right = pt.X >= r.Right - margin;
            var top = pt.Y < r.Top + margin;
            var bottom = pt.Y >= r.Bottom - margin;
            if (top && left) result = NcHitTest.Topleft;
            else if (top && right) result = NcHitTest.Topright;
            else if (bottom && left) result = NcHitTest.Bottomleft;
            else if (bottom && right) result = NcHitTest.Bottomright;
            else if (left) result = NcHitTest.Left;
            else if (right) result = NcHitTest.Right;
            else if (top) result = NcHitTest.Top;
            else if (bottom) result = NcHitTest.Bottom;
        }
        else if (chromeResizeMode == WindowResizeMode.CanResizeWithGrip && chromeState != WindowState.Maximized)
        {
            // Grip-only resize (the fully-custom-chrome mode): NO edge borders - the bottom-right ResizeGripper is the
            // ONLY sizing region. A point in its published rect hit-tests as the bottom-right corner, so WS_THICKFRAME's
            // native resize runs from the grip. Plain-Rect read (thread-safe), like CaptionDragRect below.
            var dpi = window.DpiScale;
            var gripDip = new Vector2((pt.X - r.Left) / dpi.X, (pt.Y - r.Top) / dpi.Y);
            if (window.ResizeGripRect.Contains(gripDip)) result = NcHitTest.Bottomright;
        }

        // The caption strip stays HTCLIENT: the drag is MANAGED. A geometric HTCAPTION here couldn't know the z-order, so
        // any interactive content floating over the title bar (a popup, a SlidePanel, an overlay button) had its clicks
        // stolen by the OS caption move-loop. Instead the TitleBar's PART_DragArea handles MouseLeftButtonDown and calls
        // DragMove() (ReleaseCapture + WM_NCLBUTTONDOWN/HTCAPTION -> the SAME native drag + Aero Snap). Managed hit-testing
        // already respects overlays, so a drag only starts when the drag area is genuinely the topmost element.

        isOverSizeFrame = result == NcHitTest.Left || result == NcHitTest.Right || result == NcHitTest.Top ||
                          result == NcHitTest.Bottom || result == NcHitTest.Topleft || result == NcHitTest.Topright ||
                          result == NcHitTest.Bottomleft || result == NcHitTest.Bottomright;
        handled = true;
        return (IntPtr)(int)result;
    }

    private IntPtr HandleNcCalcSize(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // Native frame, or the FALSE form (a single RECT we don't touch): let the OS compute the non-client area.
        if (!chromeCustom || wParam == IntPtr.Zero)
        {
            handled = false;
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }

        // Custom chrome: return the proposed window rect UNCHANGED as the client rect -> the whole window is client, so
        // the OS draws no visible frame. WS_THICKFRAME is still set, so native resize / Aero Snap / the drop shadow /
        // maximize-to-work-area keep working; the resize borders come back via WM_NCHITTEST.
        // Query the LIVE maximized state, not the cached chromeState: a native caption drag restores a maximized window
        // mid-loop and fires WM_NCCALCSIZE before our chromeState catches up. Trusting the stale "Maximized" then inset a
        // now-normal window, leaving a ~2px frame strip poking through the top. IsZoomed is always correct at this instant.
        if (Win32Interop.IsZoomed(window.Handle))
        {
            // A maximized WS_THICKFRAME window is oversized by the frame on every edge (positioned at -frame), so a full
            // client would spill off-screen / under the taskbar. Inset by the frame thickness so the client == work area.
            // Touch ONLY rgrc[0] (the proposed rect, at offset 0) as a bare RECT - NOT the whole NCCALCSIZE_PARAMS: its
            // lppos field is a NATIVE POINTER, and writing the struct back would clobber it, sending the OS into a spin.
            var fx = Win32Interop.GetSystemMetrics(SystemMetrics.Cxframe) +
                     Win32Interop.GetSystemMetrics(SystemMetrics.Cxpaddedborder);
            var fy = Win32Interop.GetSystemMetrics(SystemMetrics.Cyframe) +
                     Win32Interop.GetSystemMetrics(SystemMetrics.Cxpaddedborder);
            var rect = Marshal.PtrToStructure<RECT>(lParam);
            rect.Left += fx;
            rect.Right -= fx;
            rect.Top += fy;
            rect.Bottom -= fy;
            Marshal.StructureToPtr(rect, lParam, false);
        }

        handled = true;
        return IntPtr.Zero;
    }

    // Floor the drag-resize track size so a custom-chrome window can't be shrunk until the caption is crushed. Only the
    // MIN track is set; the OS-proposed maximize size/position are left alone (so maximize still fits the work area).
    // No lockable property read here (chromeMinWidth/Height were cached at SetWindow; DpiScale is non-locking).
    private IntPtr HandleGetMinMaxInfo(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        if (!chromeCustom)
        {
            handled = false;
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var dpi = window.DpiScale;
        mmi.ptMinTrackSize.X = (int)(chromeMinWidth * dpi.X);
        mmi.ptMinTrackSize.Y = (int)(chromeMinHeight * dpi.Y);
        Marshal.StructureToPtr(mmi, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    private WindowState lastWindowState;
    private IntPtr HandleSysCommand(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var command = (SystemCommands)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
        var p = Messages.PointFromLParam(lParam);
        window.StateChanged -= WindowOnStateChanged;
        switch (command)
        {
            case SystemCommands.CLOSE:
                // Managed close; OnWindowClosed does the HWND teardown (RemoveHook + RemoveWindow + DestroyWindow).
                window.Close();
                break;
            case SystemCommands.MOVE:
                Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, (int)p.X, (int)p.Y, (int)window.Width,
                    (int)window.Height, SetWindowPosFlags.Asyncwindowpos | SetWindowPosFlags.Nosize);
                break;
            case SystemCommands.MAXIMIZE:
                chromeState = WindowState.Maximized;
                window.State = WindowState.Maximized;
                Win32Interop.ShowWindow(window.Handle, WindowShowStyle.Maximize);
                break;
            case SystemCommands.MINIMIZE:
                lastWindowState = chromeState;
                chromeState = WindowState.Minimized;
                window.State = WindowState.Minimized;
                Win32Interop.ShowWindow(window.Handle, WindowShowStyle.Minimize);
                break;
            case SystemCommands.RESTORE:
                if (lastWindowState == WindowState.Maximized && chromeState == WindowState.Minimized)
                {
                    lastWindowState = WindowState.Maximized;
                }
                else
                {
                    lastWindowState = WindowState.Normal;
                }
                var style = ConvertStateToShowStyle(lastWindowState);
                chromeState = lastWindowState;
                window.State = lastWindowState;
                Win32Interop.ShowWindow(window.Handle, style);
                break;
            default:
                Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
                break;
        }
        window.StateChanged += WindowOnStateChanged;
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleResize(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        if (chromeState == WindowState.Minimized)
        {
            return IntPtr.Zero;
        }

        // Read the new size synchronously (the message's transient state), but APPLY it on the UI loop thread via the
        // same dispatch input uses. The Width/ClientWidth setters fire ClientSizeChanged, which mutates the renderer's
        // viewport/scissor/projection + IsRendererUpToDate + re-runs layout - doing that straight off the OS message
        // thread races the render/layout loop (a non-volatile flag it may never see, a torn viewport vs swapchain), so
        // the picture stops tracking the window. Marshalled through the queue it runs in order at the next Update start.
        Win32Interop.GetWindowRect(window.Handle, out var rect);
        Win32Interop.GetClientRect(window.Handle, out var client);
        var w = rect.Width;
        var h = rect.Height;
        var cw = client.Width;   // physical client px
        var ch = client.Height;

        // WM_SIZE's type tells us when the OS itself changed the maximize state (Aero Snap, drag-to-maximize, a caption
        // restore-drag) - paths that never go through our button/SysCommand. Sync window.State so StateChanged fires and
        // the title bar's maximize<->restore glyph follows; otherwise it sticks on the state it had before the drag.
        var sizeType = Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32();
        WindowState? osState = sizeType switch
        {
            0 => WindowState.Normal,      // SIZE_RESTORED
            2 => WindowState.Maximized,   // SIZE_MAXIMIZED
            _ => null                     // SIZE_MINIMIZED etc. - handled elsewhere / not a caption-drag case
        };

        DispatchInput(() =>
        {
            window.Width = w;
            window.Height = h;
            // These writes REPORT what the OS did; they do not ask for anything. Say so, or SetSize takes them for a
            // request and pushes them back - and since they are marshalled here (a frame or more after the message), the
            // number it pushes is already stale, the OS resizes to it, reports THAT, and the window shakes for good.
            _reportingOsSize = true;
            // ClientWidth/Height logical (DIP) = physical / DPI. DpiScale is read on the loop thread (where it's updated
            // by the marshalled WM_DPICHANGED handler), so a DPI change that precedes this resize is already applied.
            window.ClientWidth = cw / window.DpiScale.X;
            window.ClientHeight = ch / window.DpiScale.Y;
            _reportingOsSize = false;

            if (osState.HasValue && window.State != osState.Value)
            {
                // Detach the managed->OS driver first so this OS-originated sync doesn't bounce back into ShowWindow.
                window.StateChanged -= WindowOnStateChanged;
                chromeState = osState.Value;
                window.State = osState.Value;
                window.StateChanged += WindowOnStateChanged;
            }
        });

        return IntPtr.Zero;
    }

    // The window moved to a monitor with a different scale (or the scale changed). wParam packs the new DPI (LOWORD X /
    // HIWORD Y); lParam is the RECT the OS wants the window at, already sized for the new DPI. Apply that rect
    // synchronously here (a Win32 op that must run on the owning thread), but marshal the managed DpiScale update onto
    // the loop thread - it fires DpiChanged, which re-scales the renderer + re-lays-out, and must not race the loop.
    // Windows announces a personalisation change - the user flipping light/dark, or the scheduled switch at sunset -
    // by broadcasting WM_SETTINGCHANGE with "ImmersiveColorSet" in lParam. Announced, not polled: nothing here asks
    // the OS repeatedly, it is told.
    private IntPtr HandleSettingChange(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;   // a broadcast: other windows and the default handler still want it

        var area = lParam == IntPtr.Zero ? null : System.Runtime.InteropServices.Marshal.PtrToStringUni(lParam);
        if (!string.Equals(area, "ImmersiveColorSet", StringComparison.Ordinal)) return IntPtr.Zero;

        // Setting this is the whole reaction: SystemAppearance raises its own change, the theme manager re-resolves the
        // application's variant if it is following the system, and the subtrees that asked to follow it are re-styled.
        // A variant switch, so no template is rebuilt - the OS turning night is a colour write per palette key.
        Core.Resources.SystemAppearance.PrefersDark = Win32Interop.SystemPrefersDarkAppearance();
        return IntPtr.Zero;
    }

    private IntPtr HandleDpiChanged(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        var packed = wParam.ToInt64();
        var dpiX = (uint)(packed & 0xFFFF);
        var dpiY = (uint)((packed >> 16) & 0xFFFF);

        // Update DpiScale (loop thread) BEFORE SetWindowPos: SetWindowPos synchronously fires WM_SIZE -> HandleResize,
        // which marshals ClientWidth = physical / DpiScale. Queuing the scale update ahead of it (one FIFO) makes that
        // divide use the NEW scale. Setting DpiScale also fires DpiChanged -> the renderer re-scales RenderScale.
        var scale = new Vector2(dpiX / 96.0, dpiY / 96.0);
        DispatchInput(() => window.DpiScale = scale);

        var r = Marshal.PtrToStructure<RECT>(lParam);
        Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height,
            SetWindowPosFlags.Nozorder | SetWindowPosFlags.Noactivate);
        return IntPtr.Zero;
    }

    // The window's current-monitor DPI as a per-axis scale (1,1 = 96 DPI / 100%). Falls back to 1,1 if the query fails.
    private static Vector2 ReadDpiScale(IntPtr hwnd)
    {
        var monitor = Win32Interop.MonitorFromWindow(hwnd, Win32Interop.MonitorDefaultToNearest);
        if (Win32Interop.GetDpiForMonitor(monitor, Win32Interop.MdtEffectiveDpi, out var dpiX, out var dpiY) == 0 && dpiX > 0)
            return new Vector2(dpiX / 96.0, dpiY / 96.0);
        return new Vector2(1, 1);
    }

    // Decode WM_KEY*'s bit-packed LPARAM HERE, so the neutral input layer receives what the press MEANS (was it already
    // down, how many repeats) instead of a Win32 message parameter it would have to know how to unpack.
    private static KeyPressInfo DecodePress(IntPtr lParam)
    {
        var parameters = Messages.GetKeyParameters(lParam);
        return new KeyPressInfo
        {
            PreviousState = parameters.PreviousState == Adamantium.Win32.KeyStates.Down ? KeyState.Down : KeyState.Up,
            RepeatCount = parameters.RepeatCount
        };
    }

    private IntPtr HandleKeyDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var press = DecodePress(lParam);
        // ...to THIS window when nothing is focused: a window opens with no focused element, and a key dropped there
        // left the keyboard dead until something had been clicked. See KeyboardDevice.ProcessEvent.
        DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyDown, press, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp()), window));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleKeyUp(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var press = DecodePress(lParam);
        DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyUp, press, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp()), window));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleChar(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var text = Messages.GetChar(wParam);
        //Ignoring system keys
        if (text >= 32)
        {
            DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawTextInputEventArgs(text.ToString(),
                KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp())));
        }
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseMove(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // RELATIVE mouse mode (game mouse-look): synthesize a RawMouseMove delta from the physical move and re-centre the
        // cursor, so the game gets unbounded relative motion (the cursor never reaches the window edge). The normal
        // MouseMove is suppressed while looking - the cursor is hidden and pinned to the centre.
        if (_relativeActive)
        {
            Win32Interop.GetCursorPos(out var cur);
            var dx = cur.X - _recenterScreen.X;
            var dy = cur.Y - _recenterScreen.Y;
            if (dx != 0 || dy != 0)
            {
                Win32Interop.SetCursorPos(_recenterScreen.X, _recenterScreen.Y);
                var rawArgs = new RawInputMouseEventArgs(new Vector2(dx, dy), RawMouseEventType.RawMouseMove, window,
                    ToLogical(Messages.PointFromLParam(lParam)),
                    WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam), MouseDevice.CurrentDevice, GetTimeStamp());
                DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(rawArgs));
            }
            handled = true;
            return IntPtr.Zero;
        }

        if (!trackMouse)
        {
            var tm = new TRACKMOUSEEVENT
            {
                cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                dwFlags = 2, // we are tracking only MouseLease event
                hwndTrack = window.Handle,
                dwHoverTime = 0,
            };
            trackMouse = true;
            Win32Interop.TrackMouseEvent(ref tm);
        }
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.MouseMove, window, ToLogical(Messages.PointFromLParam(lParam)),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam), MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        // A drag's override cursor: WM_SETCURSOR barely fires while the mouse is captured, so re-assert the override on
        // every move here (the reliable path) - otherwise the drop-effect cursor (Move / Copy / No) never shows.
        if (Mouse.OverrideCursor is { } dragCursor) Cursor.Platform?.Apply(dragCursor);
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeave(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        trackMouse = false;
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.LeaveWindow, window, Vector2.Zero,
            KeyboardDevice.CurrentDevice.Modifiers, MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeftButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var modifiers = WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam);
        var eventType = WindowsMouseDeviceExtension.EventTypeFromMessage(windowMessage, wParam);
        var eventArgs = new RawMouseEventArgs(eventType, window, ToLogical(Messages.PointFromLParam(lParam)),
            modifiers, MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));

        // Hold the OS mouse capture on the WINDOW while ANY mouse button is down (button state read from wParam, which
        // the OS sets per message). This guarantees the window receives the button-UP even when the release happens
        // OUTSIDE it, so a drag past the window edge (a scrollbar thumb dragged out and released) still gets its release
        // and stops - instead of sticking and then "following" the pointer with no button held. Runs on THIS message-pump
        // thread that owns the window (SetCapture is thread-affine). The app-level routing capture (MouseDevice.Captured)
        // is separate and set on the loop thread by the marshalled ProcessEvent above - which is exactly why mirroring
        // THAT here was unreliable: Captured is not set yet when this line runs.
        var anyButton = InputModifiers.LeftMouseButton | InputModifiers.RightMouseButton |
            InputModifiers.MiddleMouseButton | InputModifiers.X1MouseButton | InputModifiers.X2MouseButton;
        var buttonDown = (modifiers & anyButton) != 0;
        if (buttonDown != osMouseCaptured)
        {
            osMouseCaptured = buttonDown;
            SetMouseCapture(buttonDown);
        }

        handled = true;
        return IntPtr.Zero;
    }

    // Platform-specific OS capture (the shared "when" lives in MouseDevice.SyncOsMouseCapture).
    public void SetMouseCapture(bool capture)
    {
        if (capture) Win32Interop.SetCapture(window.Handle);
        else Win32Interop.ReleaseCapture();
    }

    // Called on the LOOP thread (from RenderTargetPanel via the window). Posts the enter/exit to the pump thread (cursor
    // hide/show + capture are HWND-thread-affine): enable in wParam; on disable the panel's RESTORE screen point packed
    // into lParam (two 32-bit ints in the 64-bit value), so the worker warps the cursor back without storing any position.
    public void SetRelativeMouseMode(bool enabled, PixelPoint restoreScreen)
    {
        if (window == null || window.Handle == IntPtr.Zero) return;
        var wp = enabled ? (IntPtr)1 : IntPtr.Zero;
        var lp = enabled ? IntPtr.Zero
            : (IntPtr)(((long)(int)restoreScreen.X << 32) | (uint)(int)restoreScreen.Y);
        Messages.PostMessage(window.Handle, RelativeModeMessage, wp, lp);
    }

    // Pump thread. Enter: hide the cursor, hold OS capture (moves keep arriving even at the window edge) and centre it;
    // HandleMouseMove then feeds the delta and re-centres. Exit: warp the cursor to the panel's restore point (where it
    // vanished, in screen coords), show it, and drop the capture unless a button drag still owns it.
    private IntPtr HandleRelativeModeMessage(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        if (window == null || window.Handle == IntPtr.Zero) return IntPtr.Zero;
        var enable = wParam != IntPtr.Zero;
        if (enable && !_relativeActive)
        {
            _relativeActive = true;
            Win32Interop.SetCapture(window.Handle);
            Win32Interop.ShowCursor(false);
            _recenterScreen = ClientCentreScreen();
            Win32Interop.SetCursorPos(_recenterScreen.X, _recenterScreen.Y);
        }
        else if (!enable && _relativeActive)
        {
            _relativeActive = false;
            var lp = (long)lParam;
            Win32Interop.SetCursorPos((int)(lp >> 32), (int)lp);
            Win32Interop.ShowCursor(true);
            if (!osMouseCaptured) Win32Interop.ReleaseCapture();
        }
        return IntPtr.Zero;
    }

    private NativePoint ClientCentreScreen()
    {
        Win32Interop.GetClientRect(window.Handle, out var rc);
        var centre = new NativePoint((rc.Right - rc.Left) / 2, (rc.Bottom - rc.Top) / 2);
        Win32Interop.ClientToScreen(window.Handle, ref centre);
        return centre;
    }

    // Caption drag: hand the window to the OS modal move loop (which also does Aero Snap + maximized restore-drag). This
    // is called on the LOOP thread (input routing), but the native WM_NCLBUTTONDOWN/HTCAPTION handoff must run on the
    // thread that OWNS the HWND's input queue - otherwise the move loop reads no held button and returns instantly. So we
    // just PostMessage the request to the window; CustomWndProc runs HandleBeginMoveDrag on the owner thread and does the
    // in-thread trick there. lParam = packed screen cursor pos so the native loop starts from the right point.
    public void BeginMoveDrag()
    {
        if (window == null || window.Handle == IntPtr.Zero) return;
        Win32Interop.GetCursorPos(out var cur);
        var lp = (IntPtr)((cur.Y << 16) | (cur.X & 0xFFFF));
        Messages.PostMessage(window.Handle, BeginMoveDragMessage, IntPtr.Zero, lp);
    }

    // Runs on the HWND-owning thread (posted by BeginMoveDrag). ReleaseCapture drops our own button capture, then faking a
    // non-client caption press hands off to the OS modal move loop - in-thread, so it sees the held button and engages. A
    // maximized window is intentionally allowed: the native loop restores it under the cursor and keeps dragging.
    private IntPtr HandleBeginMoveDrag(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        if (window == null || window.Handle == IntPtr.Zero) return IntPtr.Zero;
        Win32Interop.ReleaseCapture();
        osMouseCaptured = false;
        Win32Interop.SendMessage(window.Handle, WindowMessages.Nclbuttondown, (IntPtr)(int)NcHitTest.Caption, lParam);
        return IntPtr.Zero;
    }

    private IntPtr HandleCaptureChanged(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // The OS revoked our capture (another window/app grabbed it, alt-tab, etc.). Drop the internal capture too so a
        // captured control doesn't stay stuck believing the drag is still live. Capture() re-routes input against the
        // visual tree, so marshal it onto the loop thread (the check runs there too - no cross-thread read of Captured).
        osMouseCaptured = false;
        DispatchInput(() => { if (MouseDevice.CurrentDevice.Captured != null) MouseDevice.CurrentDevice.Capture(null); });
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseWheel(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var eventArgs = new RawMouseWheelEventArgs(
            Messages.GetWheelDelta(wParam),
            RawMouseEventType.MouseWheel,
            window,
            window.PointToClient(ToPixel(Messages.PointFromLParam(lParam))),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam),
            MouseDevice.CurrentDevice, GetTimeStamp(),
            windowMessage == WindowMessages.MouseHwheel);
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleSetCursor(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;
        Cursor.Platform?.Apply(Mouse.OverrideCursor ?? Mouse.Cursor);   // a drag's override wins
        if (isOverSizeFrame)
        {
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }
        else
        {
            handled = true;
            return IntPtr.Zero;
        }
    }

    private static uint GetTimeStamp()
    {
        return unchecked((uint)Win32Interop.GetMessageTime());
    }

    private void HandleActivation()
    {
        window.SetIsActive(true);
        UIContext.UIApplication.SetActiveWindow(window);
        // Back to where the keyboard was in THIS window; a window being seen for the first time has nowhere to go back
        // to, so it starts at its first stop - otherwise it would come up with no focus at all and stay dead until
        // something in it was clicked. Unspecified, not Tab: activation is not a keyboard move, so no ring lights up.
        if (!FocusManager.TryRestoreFocus(window))
        {
            KeyboardNavigation.MoveInto(window, NavigationMethod.Unspecified);
        }
    }

    private void HandleDeactivation()
    {
        window.SetIsActive(false);
        UIContext.UIApplication.InactivateWindow(window);
        // The one focused element must not stay pointing into a window the keys no longer reach - see LeaveWindow.
        FocusManager.LeaveWindow(window);
    }

}
