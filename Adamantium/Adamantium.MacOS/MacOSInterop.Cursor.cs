using System;
using System.Runtime.InteropServices;

namespace Adamantium.MacOS
{
    public static partial class MacOSInterop
    {
        public static class Cursor
        {
            [DllImport(WrapperFramework, EntryPoint = "Hide")]
            public static extern IntPtr Hide();
            
            [DllImport(WrapperFramework, EntryPoint = "Unhide")]
            public static extern IntPtr Unhide();
            
            [DllImport(WrapperFramework, EntryPoint = "Pop")]
            public static extern IntPtr Pop();
            
            [DllImport(WrapperFramework, EntryPoint = "SetHiddenUntilMouseMoves")]
            public static extern IntPtr SetHiddenUntilMouseMoves();
            
            [DllImport(WrapperFramework, EntryPoint = "SetCursorType")]
            public static extern IntPtr SetCursorType(uint cursorType);
            
            [DllImport(WrapperFramework, EntryPoint = "GetCursorType")]
            public static extern IntPtr GetCursorType(uint cursorType);
            
            [DllImport(WrapperFramework, EntryPoint = "GetCurrentCursor")]
            public static extern IntPtr GetCurrentCursor();
        }
    }
}