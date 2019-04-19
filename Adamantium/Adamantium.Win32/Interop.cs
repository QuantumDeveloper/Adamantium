using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.Win32.RawInput;

namespace Adamantium.Win32
{
    public class Interop
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern UInt16 RegisterClassW([In] ref WndClass lpWndClass);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateWindowExW(
            WindowStyleEx dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            WindowStyle dwStyle,
            Int32 x,
            Int32 y,
            Int32 nWidth,
            Int32 nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        public delegate IntPtr WndProc(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(
            IntPtr wndProc,
            IntPtr hWnd,
            WindowMessages Msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr ShowWindow(IntPtr hwnd, WindowShowStyle count);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "DefWindowProcW")]
        public static extern IntPtr DefWindowProcW(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern Boolean AdjustWindowRect(ref RECT lpRect, UInt32 dwStyle, bool bMenu);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(uint key);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetKeyboardState(byte[] keys);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(uint key);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, NativeCursors cursorName);

        [DllImport("user32.dll", EntryPoint = "LoadCursorFromFileW", SetLastError = true)]
        public static extern IntPtr LoadCursorFromFile(string filePath);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        public static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

        [DllImport("user32.dll")]
        public static extern bool SetSystemCursor(IntPtr hcur, NativeCursors cursorId);

        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool show);

        /// <summary>
        /// Changes an attribute of the specified window. The function also sets the 32-bit (long) value at the specified offset into the extra window memory.
        /// </summary>
        /// <param name="hWnd">A handle to the window and, indirectly, the class to which the window belongs..</param>
        /// <param name="nIndex">The zero-based offset to the value to be set. Valid values are in the range zero through the number of bytes of extra window memory, minus the size of an integer. To set any other value, specify one of the following values: GWL_EXSTYLE, GWL_HINSTANCE, GWL_ID, GWL_STYLE, GWL_USERDATA, GWL_WNDPROC </param>
        /// <param name="dwNewLong">The replacement value.</param>
        /// <returns>If the function succeeds, the return value is the previous value of the specified 32-bit integer. 
        /// If the function fails, the return value is zero. To get extended error information, call GetLastError. </returns>
        public static IntPtr SetWindowLong(IntPtr hWnd, WindowLongType nIndex, IntPtr dwNewLong)
        {
            if (Environment.Is64BitProcess)
            {
                return SetWindowLong64(hWnd, nIndex, dwNewLong);
            }
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Unicode)]
        private static extern IntPtr SetWindowLong64(IntPtr hwnd, WindowLongType index, IntPtr wndProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Unicode)]
        private static extern long SetWindowLong32(IntPtr hwnd, WindowLongType index, long newValue);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out NativePoint lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            SetWindowPosFlags uFlags);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        public static extern MessageBoxResult MessageBoxEx(
            IntPtr hwnd,
            String text,
            String caption,
            uint options,
            ushort languageId);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern int GetMessageTime();

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(
            IntPtr hWnd,
            [MarshalAs(UnmanagedType.Struct)] ref NativePoint lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(
            IntPtr hWnd,
            [MarshalAs(UnmanagedType.Struct)] ref NativePoint lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        //Get value which defines time between two mouse down events, which will be translated as mouse double click 
        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();


        [DllImport("Kernel32.dll")]
        public static extern UInt64 GetTickCount64();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] InputDevice[] pInputDevices,
            int uiNumDevices,
            int cbSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetRawInputDeviceList(
            [In, Out] RawInputDeviceList[] rawInputDeviceList,
            ref int numDevices,
            int size /* = (uint)Marshal.SizeOf(typeof(RawInputDeviceList)) */);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            RawInputDeviceInfoCommand uiCommand,
            IntPtr pData,
            ref int pcbSize);

        /// <summary>
        /// Function to retrieve raw input data.
        /// </summary>
        /// <param name="hRawInput">Handle to the raw input.</param>
        /// <param name="uiCommand">Command to issue when retrieving data.</param>
        /// <param name="pData">Raw input data.</param>
        /// <param name="pcbSize">Number of bytes in the array.</param>
        /// <param name="cbSizeHeader">Size of the header.</param>
        /// <returns>0 if successful if pData is null, otherwise number of bytes if pData is not null.</returns>
        [DllImport("user32.dll")]
        public static extern int GetRawInputData(
            IntPtr hRawInput,
            RawInputCommand uiCommand,
            out RawInputData pData,
            ref int pcbSize,
            int cbSizeHeader);


        [DllImport("user32.dll")]
        public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        public static extern IntPtr DeferWindowPos(
            IntPtr hWinPosInfo,
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            SetWindowPosFlags flags);

        [DllImport("user32.dll")]
        public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        [DllImport("user32.dll")]
        public static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern bool GetUpdateRect(IntPtr hwnd, out RECT lpRect, bool bErase);

        public static IntPtr GetWindowLong(IntPtr hWnd, WindowLongType nIndex)
        {
            return Environment.Is64BitProcess ? GetWindowLong64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        public static WindowStyle GetWindowStyle(IntPtr hWnd, WindowLongType nIndex)
        {
            var longPtr = GetWindowLong64(hWnd, nIndex);
            var style = IntPtr.Size == 4 ? (WindowStyle) longPtr.ToInt32() : (WindowStyle) longPtr.ToInt64();
            return style;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindowLong64(IntPtr hwnd, WindowLongType index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindowLong32(IntPtr hwnd, WindowLongType index);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // When you don't want the ProcessId, use this overload and pass IntPtr.Zero for the second parameter
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, ref IntPtr pvParam, SPIF fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, int[] pvParam, SPIF fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern int GetSystemMetrics(SystemMetrics metrics);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr windowHandle, WindowMessages msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, DeviceContextValues flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, [In] ref RECT lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

        [DllImport("user32.dll")]
        public static extern bool InflateRect(ref RECT lprc, int dx, int dy);

        [DllImport("dwmapi.dll")]
        public static extern IntPtr DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        /*
            Graphics
        */

        [DllImport("gdi32.dll")]
        public static extern bool Rectangle(IntPtr hDC, int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(uint crColor);

        /// <summary>
        /// The SelectObject function selects an object into the specified device context (DC). The new object replaces the previous object of the same type.
        /// </summary>
        /// <param name="hdc">Device Context</param>
        /// <param name="hgdiobj"></param>
        /// <returns></returns>
        [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject([In] IntPtr hdc, [In] IntPtr hgdiobj);

        [DllImport("gdi32.dll", EntryPoint = "CreatePen")]
        public static extern IntPtr CreatePen(PenStyle penStyle, int width, uint color);
    }
}
