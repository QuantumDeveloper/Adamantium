using System;
using System.Runtime.InteropServices;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>The Win32 text clipboard (CF_UNICODETEXT), registered on <see cref="Clipboard.Current"/> by the Windows
/// platform. All calls run on the UI (message-pump) thread, as clipboard ownership is thread-affine. Every operation is
/// best-effort: if the OS clipboard is momentarily locked by another app, it fails quietly rather than throwing into the
/// input handler.</summary>
internal sealed class WindowsClipboard : IClipboard
{
    public string GetText()
    {
        if (!Win32Interop.IsClipboardFormatAvailable(Win32Interop.CF_UNICODETEXT)) return string.Empty;
        if (!Win32Interop.OpenClipboard(IntPtr.Zero)) return string.Empty;
        try
        {
            var handle = Win32Interop.GetClipboardData(Win32Interop.CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return string.Empty;
            var ptr = Win32Interop.GlobalLock(handle);
            if (ptr == IntPtr.Zero) return string.Empty;
            try { return Marshal.PtrToStringUni(ptr) ?? string.Empty; }
            finally { Win32Interop.GlobalUnlock(handle); }
        }
        finally { Win32Interop.CloseClipboard(); }
    }

    public void SetText(string text)
    {
        text ??= string.Empty;
        if (!Win32Interop.OpenClipboard(IntPtr.Zero)) return;
        try
        {
            Win32Interop.EmptyClipboard();

            var bytes = (UIntPtr)((text.Length + 1) * sizeof(char));   // + terminating NUL
            var hGlobal = Win32Interop.GlobalAlloc(Win32Interop.GMEM_MOVEABLE, bytes);
            if (hGlobal == IntPtr.Zero) return;

            var target = Win32Interop.GlobalLock(hGlobal);
            if (target == IntPtr.Zero) return;
            try
            {
                var chars = (text + '\0').ToCharArray();
                Marshal.Copy(chars, 0, target, chars.Length);
            }
            finally { Win32Interop.GlobalUnlock(hGlobal); }

            // On success the clipboard owns hGlobal - do not free it here.
            Win32Interop.SetClipboardData(Win32Interop.CF_UNICODETEXT, hGlobal);
        }
        finally { Win32Interop.CloseClipboard(); }
    }
}
