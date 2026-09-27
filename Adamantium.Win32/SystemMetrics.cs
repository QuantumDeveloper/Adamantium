namespace Adamantium.Win32
{
    public enum SystemMetrics
    {
        /// <summary>
        /// The flags that specify how the system arranged minimized windows. For more information, see the Remarks section in this topic.
        /// </summary>
        Arrange = 56,

        /// <summary>
        /// The value that specifies how the system is started:
        /// 0 Normal boot
        /// 1 Fail-safe boot
        /// 2 Fail-safe with network boot
        /// A fail-safe boot (also called SafeBoot, Safe Mode, or Clean Boot) bypasses the user startup files.
        /// </summary>
        Cleanboot = 67,

        /// <summary>
        /// The number of display monitors on a desktop. For more information, see the Remarks section in this topic.
        /// </summary>
        Cmonitors = 80,

        /// <summary>
        /// The number of buttons on a mouse, or zero if no mouse is installed.
        /// </summary>
        Cmousebuttons = 43,

        /// <summary>
        /// Reflects the state of the laptop or slate mode, 0 for Slate Mode and non-zero otherwise. When this system metric changes, the system sends a broadcast message via WM_SETTINGCHANGE with "ConvertibleSlateMode" in the LPARAM. Note that this system metric doesn't apply to desktop PCs. In that case, use GetAutoRotationState.
        /// </summary>
        Convertibleslatemode = 0x2003,

        /// <summary>
        /// The width of a window border, in pixels. This is equivalent to the SM_CXEDGE value for windows with the 3-D look.
        /// </summary>
        Cxborder = 5,

        /// <summary>
        /// The width of a cursor, in pixels. The system cannot create cursors of other sizes.
        /// </summary>
        Cxcursor = 13,

        /// <summary>
        /// This value is the same as SM_CXFIXEDFRAME.
        /// </summary>
        Cxdlgframe = 7,

        /// <summary>
        /// The width of the rectangle around the location of a first click in a double-click sequence, in pixels. The second click must occur within the rectangle that is defined by SM_CXDOUBLECLK and SM_CYDOUBLECLK for the system to consider the two clicks a double-click. The two clicks must also occur within a specified time.
        /// To set the width of the double-click rectangle, call SystemParametersInfo with SPI_SETDOUBLECLKWIDTH.
        /// </summary>
        Cxdoubleclk = 36,

        /// <summary>
        /// The number of pixels on either side of a mouse-down point that the mouse pointer can move before a drag operation begins. This allows the user to click and release the mouse button easily without unintentionally starting a drag operation. If this value is negative, it is subtracted from the left of the mouse-down point and added to the right of it.
        /// </summary>
        Cxdrag = 68,

        /// <summary>
        /// The width of a 3-D border, in pixels. This metric is the 3-D counterpart of SM_CXBORDER.
        /// </summary>
        Cxedge = 45,

        /// <summary>
        /// The thickness of the frame around the perimeter of a window that has a caption but is not sizable, in pixels. SM_CXFIXEDFRAME is the height of the horizontal border, and SM_CYFIXEDFRAME is the width of the vertical border.
        /// This value is the same as SM_CXDLGFRAME.
        /// </summary>
        Cxfixedframe = 7,

        /// <summary>
        /// The width of the left and right edges of the focus rectangle that the DrawFocusRect draws. This value is in pixels.
        /// Windows 2000:  This value is not supported.
        /// </summary>
        Cxfocusborder = 83,

        /// <summary>
        /// This value is the same as SM_CXSIZEFRAME.
        /// </summary>
        Cxframe = 32,

        /// <summary>
        /// The width of the client area for a full-screen window on the primary display monitor, in pixels. To get the coordinates of the portion of the screen that is not obscured by the system taskbar or by application desktop toolbars, call the SystemParametersInfo function with the SPI_GETWORKAREA value.
        /// </summary>
        Cxfullscreen = 16,

        /// <summary>
        /// The width of the arrow bitmap on a horizontal scroll bar, in pixels.
        /// </summary>
        Cxhscroll = 21,

        /// <summary>
        /// The width of the thumb box in a horizontal scroll bar, in pixels.
        /// </summary>
        Cxhthumb = 10,

        /// <summary>
        /// The default width of an icon, in pixels. The LoadIcon function can load only icons with the dimensions that SM_CXICON and SM_CYICON specifies.
        /// </summary>
        Cxicon = 11,

        /// <summary>
        /// The width of a grid cell for items in large icon view, in pixels. Each item fits into a rectangle of size SM_CXICONSPACING by SM_CYICONSPACING when arranged. This value is always greater than or equal to SM_CXICON.
        /// </summary>
        Cxiconspacing = 38,

        /// <summary>
        /// The default width, in pixels, of a maximized top-level window on the primary display monitor.
        /// </summary>
        Cxmaximized = 61,

        /// <summary>
        /// The default maximum width of a window that has a caption and sizing borders, in pixels. This metric refers to the entire desktop. The user cannot drag the window frame to a size larger than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
        /// </summary>
        Cxmaxtrack = 59,

        /// <summary>
        /// The width of the default menu check-mark bitmap, in pixels.
        /// </summary>
        Cxmenucheck = 71,

        /// <summary>
        /// The width of menu bar buttons, such as the child window close button that is used in the multiple document interface, in pixels.
        /// </summary>
        Cxmenusize = 54,

        /// <summary>
        /// The minimum width of a window, in pixels.
        /// </summary>
        Cxmin = 28,

        /// <summary>
        /// The width of a minimized window, in pixels.
        /// </summary>
        Cxminimized = 57,

        /// <summary>
        /// The width of a grid cell for a minimized window, in pixels.Each minimized window fits into a rectangle this size when arranged.This value is always greater than or equal to SM_CXMINIMIZED.
        /// </summary>
        Cxminspacing = 47,

        /// <summary>
        /// The minimum tracking width of a window, in pixels.The user cannot drag the window frame to a size smaller than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
        /// </summary>
        Cxmintrack = 34,

        /// <summary>
        /// The amount of border padding for captioned windows, in pixels.
        /// Windows XP/2000:  This value is not supported.
        /// </summary>
        Cxpaddedborder = 92,

        /// <summary>
        /// The width of the screen of the primary display monitor, in pixels.This is the same value obtained by calling GetDeviceCaps as follows: GetDeviceCaps(hdcPrimaryMonitor, HORZRES).
        /// </summary>
        Cxscreen = 0,

        /// <summary>
        /// The width of a button in a window caption or title bar, in pixels.
        /// </summary>
        Cxsize = 30,

        /// <summary>
        /// The thickness of the sizing border around the perimeter of a window that can be resized, in pixels.SM_CXSIZEFRAME is the width of the horizontal border, and SM_CYSIZEFRAME is the height of the vertical border.
        /// This value is the same as SM_CXFRAME.
        /// </summary>
        Cxsizeframe = 32,

        /// <summary>
        /// The recommended width of a small icon, in pixels.Small icons typically appear in window captions and in small icon view.
        /// </summary>
        Cxsmicon = 49,

        /// <summary>
        /// The width of small caption buttons, in pixels.
        /// </summary>
        Cxsmsize = 52,

        /// <summary>
        /// The width of the virtual screen, in pixels.The virtual screen is the bounding rectangle of all display monitors.The SM_XVIRTUALSCREEN metric is the coordinates for the left side of the virtual screen.
        /// </summary>
        CxVirtualscreen = 78,

        /// <summary>
        /// The width of a vertical scroll bar, in pixels.
        /// </summary>
        CxvScroll = 2,

        /// <summary>
        /// The height of a window border, in pixels.This is equivalent to the SM_CYEDGE value for windows with the 3-D look.
        /// </summary>
        Cyborder = 6,

        /// <summary>
        /// The height of a caption area, in pixels.
        /// </summary>
        Cycaption = 4,

        /// <summary>
        /// The height of a cursor, in pixels.The system cannot create cursors of other sizes.
        /// </summary>
        Cycursor = 14,

        /// <summary>
        /// This value is the same as SM_CYFIXEDFRAME.
        /// </summary>
        Cydlgframe = 8,

        /// <summary>
        /// The height of the rectangle around the location of a first click in a double-click sequence, in pixels.The second click must occur within the rectangle defined by SM_CXDOUBLECLK and SM_CYDOUBLECLK for the system to consider the two clicks a double-click.The two clicks must also occur within a specified time.
        /// To set the height of the double-click rectangle, call SystemParametersInfo with SPI_SETDOUBLECLKHEIGHT.
        /// </summary>
        Cydoubleclk = 37,

        /// <summary>
        /// The number of pixels above and below a mouse-down point that the mouse pointer can move before a drag operation begins.This allows the user to click and release the mouse button easily without unintentionally starting a drag operation. If this value is negative, it is subtracted from above the mouse-down point and added below it.
        /// </summary>
        Cydrag = 69,

        /// <summary>
        /// The height of a 3-D border, in pixels.This is the 3-D counterpart of SM_CYBORDER.
        /// </summary>
        Cyedge = 46,

        /// <summary>
        /// The thickness of the frame around the perimeter of a window that has a caption but is not sizable, in pixels.SM_CXFIXEDFRAME is the height of the horizontal border, and SM_CYFIXEDFRAME is the width of the vertical border.
        /// This value is the same as SM_CYDLGFRAME.
        /// </summary>
        Cyfixedframe = 8,

        /// <summary>
        /// The height of the top and bottom edges of the focus rectangle drawn by DrawFocusRect.This value is in pixels.
        /// Windows 2000:  This value is not supported.
        /// </summary>
        Cyfocusborder = 84,

        /// <summary>
        /// This value is the same as SM_CYSIZEFRAME.
        /// </summary>
        Cyframe = 33,

        /// <summary>
        /// The height of the client area for a full-screen window on the primary display monitor, in pixels.To get the coordinates of the portion of the screen not obscured by the system taskbar or by application desktop toolbars, call the SystemParametersInfo function with the SPI_GETWORKAREA value.
        /// </summary>
        Cyfullscreen = 17,

        /// <summary>
        /// The height of a horizontal scroll bar, in pixels.
        /// </summary>
        Cyhscroll = 3,

        /// <summary>
        /// The default height of an icon, in pixels.The LoadIcon function can load only icons with the dimensions SM_CXICON and SM_CYICON.
        /// </summary>
        Cyicon = 12,

        /// <summary>
        /// The height of a grid cell for items in large icon view, in pixels.Each item fits into a rectangle of size SM_CXICONSPACING by SM_CYICONSPACING when arranged.This value is always greater than or equal to SM_CYICON.
        /// </summary>
        Cyiconspacing = 39,

        /// <summary>
        /// For double byte character set versions of the system, this is the height of the Kanji window at the bottom of the screen, in pixels.
        /// </summary>
        Cykanjiwindow = 18,

        /// <summary>
        /// The default height, in pixels, of a maximized top-level window on the primary display monitor.
        /// </summary>
        Cymaximized = 62,

        /// <summary>
        /// The default maximum height of a window that has a caption and sizing borders, in pixels.This metric refers to the entire desktop.The user cannot drag the window frame to a size larger than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
        /// </summary>
        Cymaxtrack = 60,

        /// <summary>
        /// The height of a single-line menu bar, in pixels.
        /// </summary>
        Cymenu = 15,

        /// <summary>
        /// The height of the default menu check-mark bitmap, in pixels.
        /// </summary>
        Cymenucheck = 72,

        /// <summary>
        /// The height of menu bar buttons, such as the child window close button that is used in the multiple document interface, in pixels.
        /// </summary>
        Cymenusize = 55,

        /// <summary>
        /// The minimum height of a window, in pixels.
        /// </summary>
        Cymin = 29,

        /// <summary>
        /// The height of a minimized window, in pixels.
        /// </summary>
        Cyminimized = 58,

        /// <summary>
        /// The height of a grid cell for a minimized window, in pixels.Each minimized window fits into a rectangle this size when arranged.This value is always greater than or equal to SM_CYMINIMIZED.
        /// </summary>
        Cyminspacing = 48,

        /// <summary>
        /// The minimum tracking height of a window, in pixels.The user cannot drag the window frame to a size smaller than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
        /// </summary>
        Cymintrack = 35,

        /// <summary>
        /// The height of the screen of the primary display monitor, in pixels.This is the same value obtained by calling GetDeviceCaps as follows: GetDeviceCaps(hdcPrimaryMonitor, VERTRES).
        /// </summary>
        Cyscreen = 1,

        /// <summary>
        /// The height of a button in a window caption or title bar, in pixels.
        /// </summary>
        Cysize = 31,

        /// <summary>
        /// The thickness of the sizing border around the perimeter of a window that can be resized, in pixels.SM_CXSIZEFRAME is the width of the horizontal border, and SM_CYSIZEFRAME is the height of the vertical border.
        /// This value is the same as SM_CYFRAME.
        /// </summary>
        Cysizeframe = 33,

        /// <summary>
        /// The height of a small caption, in pixels.
        /// </summary>
        Cysmcaption = 51,

        /// <summary>
        /// The recommended height of a small icon, in pixels.Small icons typically appear in window captions and in small icon view.
        /// </summary>
        Cysmicon = 50,

        /// <summary>
        /// The height of small caption buttons, in pixels.
        /// </summary>
        Cysmsize = 53,

        /// <summary>
        /// The height of the virtual screen, in pixels.The virtual screen is the bounding rectangle of all display monitors.The SM_YVIRTUALSCREEN metric is the coordinates for the top of the virtual screen.
        /// </summary>
        CyVirtualscreen = 79,

        /// <summary>
        /// The height of the arrow bitmap on a vertical scroll bar, in pixels.
        /// </summary>
        Cyvscroll = 20,

        /// <summary>
        /// The height of the thumb box in a vertical scroll bar, in pixels.
        /// </summary>
        Cyvthumb = 9,

        /// <summary>
        /// Nonzero if User32.dll supports DBCS; otherwise, 0.
        /// </summary>
        Dbcsenabled = 42,

        /// <summary>
        /// Nonzero if the debug version of User.exe is installed; otherwise, 0.
        /// </summary>
        Debug = 22,

        /// <summary>
        /// Nonzero if the current operating system is Windows 7 or Windows Server 2008 R2 and the Tablet PC Input service is started; otherwise, 0. The return value is a bitmask that specifies the type of digitizer input supported by the device.For more information, see Remarks.
        /// Windows Server 2008, Windows Vista and Windows XP/2000:  This value is not supported.
        /// </summary>
        Digitizer = 94,

        /// <summary>
        /// Nonzero if Input Method Manager/Input Method Editor features are enabled; otherwise, 0.
        /// IMMENABLED indicates whether the system is ready to use a Unicode-based IME on a Unicode application. To ensure that a language-dependent IME works, check SM_DBCSENABLED and the system ANSI code page. Otherwise the ANSI-to-Unicode conversion may not be performed correctly, or some components like fonts or registry settings may not be present.
        /// </summary>
        Immenabled = 82,

        /// <summary>
        /// Nonzero if there are digitizers in the system; otherwise, 0.
        /// SM_MAXIMUMTOUCHES returns the aggregate maximum of the maximum number of contacts supported by every digitizer in the system. If the system has only single-touch digitizers, the return value is 1. If the system has multi-touch digitizers, the return value is the number of simultaneous contacts the hardware can provide.
        /// Windows Server 2008, Windows Vista and Windows XP/2000:  This value is not supported.
        /// </summary>
        Maximumtouches = 95,

        /// <summary>
        /// Nonzero if the current operating system is the Windows XP, Media Center Edition, 0 if not.
        /// </summary>
        Mediacenter = 87,

        /// <summary>
        /// Nonzero if drop-down menus are right-aligned with the corresponding menu-bar item; 0 if the menus are left-aligned.
        /// </summary>
        Menudropalignment = 40,

        /// <summary>
        /// Nonzero if the system is enabled for Hebrew and Arabic languages, 0 if not.
        /// </summary>
        Mideastenabled = 74,

        /// <summary>
        /// Nonzero if a mouse is installed; otherwise, 0. This value is rarely zero, because of support for virtual mice and because some systems detect the presence of the port instead of the presence of a mouse.
        /// </summary>
        Mousepresent = 19,

        /// <summary>
        /// Nonzero if a mouse with a horizontal scroll wheel is installed; otherwise 0.
        /// </summary>
        Mousehorizontalwheelpresent = 91,

        /// <summary>
        /// Nonzero if a mouse with a vertical scroll wheel is installed; otherwise 0.
        /// </summary>
        Mousewheelpresent = 75,

        /// <summary>
        /// The least significant bit is set if a network is present; otherwise, it is cleared.The other bits are reserved for future use.
        /// </summary>
        Network = 63,

        /// <summary>
        /// Nonzero if the Microsoft Windows for Pen computing extensions are installed; zero otherwise.
        /// </summary>
        Penwindows = 41,

        /// <summary>
        /// This system metric is used in a Terminal Services environment to determine if the current Terminal Server session is being remotely controlled.Its value is nonzero if the current session is remotely controlled; otherwise, 0.
        /// You can use terminal services management tools such as Terminal Services Manager (tsadmin.msc) and shadow.exe to control a remote session. When a session is being remotely controlled, another user can view the contents of that session and potentially interact with it.
        /// </summary>
        Remotecontrol = 0x2001,

        /// <summary>
        /// This system metric is used in a Terminal Services environment. If the calling process is associated with a Terminal Services client session, the return value is nonzero.If the calling process is associated with the Terminal Services console session, the return value is 0.
        /// Windows Server 2003 and Windows XP:  The console session is not necessarily the physical console.For more information, see WTSGetActiveConsoleSessionId.
        /// </summary>
        Remotesession = 0x1000,

        /// <summary>
        /// Nonzero if all the display monitors have the same color format, otherwise, 0. Two displays can have the same bit depth, but different color formats. For example, the red, green, and blue pixels can be encoded with different numbers of bits, or those bits can be located in different places in a pixel color value.
        /// </summary>
        Samedisplayformat = 81,

        /// <summary>
        /// This system metric should be ignored; it always returns 0.
        /// </summary>
        Secure = 44,

        /// <summary>
        /// The build number if the system is Windows Server 2003 R2; otherwise, 0.
        /// </summary>
        Serverr2 = 89,

        /// <summary>
        /// Nonzero if the user requires an application to present information visually in situations where it would otherwise present the information only in audible form; otherwise, 0.
        /// </summary>
        Showsounds = 70,

        /// <summary>
        /// Nonzero if the current session is shutting down; otherwise, 0.
        /// Windows 2000:  This value is not supported.
        /// </summary>
        Shuttingdown = 0x2000,

        /// <summary>
        /// Nonzero if the computer has a low-end (slow) processor; otherwise, 0.
        /// </summary>
        Slowmachine = 73,

        /// <summary>
        /// Nonzero if the current operating system is Windows 7 Starter Edition, Windows Vista Starter, or Windows XP Starter Edition; otherwise, 0.
        /// </summary>
        Starter = 88,

        /// <summary>
        /// Nonzero if the meanings of the left and right mouse buttons are swapped; otherwise, 0.
        /// </summary>
        Swapbutton = 23,

        /// <summary>
        /// Reflects the state of the docking mode, 0 for Undocked Mode and non-zero otherwise. When this system metric changes, the system sends a broadcast message via WM_SETTINGCHANGE with "SystemDockMode" in the LPARAM.
        /// </summary>
        Systemdocked = 0x2004,

        /// <summary>
        /// Nonzero if the current operating system is the Windows XP Tablet PC edition or if the current operating system is Windows Vista or Windows 7 and the Tablet PC Input service is started; otherwise, 0. The SM_DIGITIZER setting indicates the type of digitizer input supported by a device running Windows 7 or Windows Server 2008 R2.For more information, see Remarks.
        /// </summary>
        Tabletpc = 86,

        /// <summary>
        /// The coordinates for the left side of the virtual screen.The virtual screen is the bounding rectangle of all display monitors.The SM_CXVIRTUALSCREEN metric is the width of the virtual screen.
        /// </summary>
        Xvirtualscreen = 76,

        /// <summary>
        /// The coordinates for the top of the virtual screen.The virtual screen is the bounding rectangle of all display monitors.The SM_CYVIRTUALSCREEN metric is the height of the virtual screen.
        /// </summary>
        Yvirtualscreen = 77,
    }
}