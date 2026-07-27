using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;
using Adamantium.Win32.Ole;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Translates between the OLE wire format (<see cref="ComTypes.IDataObject"/>, HGLOBAL blocks, DROPEFFECT) and the
/// engine's platform-neutral <see cref="IDataPackage"/> / <see cref="DragDropEffects"/>. This is the ONLY place that
/// knows a "file list" is a <c>CF_HDROP</c> - a view-model just reads <c>DataFormats.Files</c>.
/// </summary>
internal static class OleDataBridge
{
    /// <summary>Read an incoming OLE payload into a managed package, EAGERLY: the source's data object is only
    /// guaranteed valid inside the callback that handed it over, and the drop itself is delivered to the view-model a
    /// frame later on the UI loop thread.
    /// <para>A drag that started in OUR app comes back as our own <see cref="Win32DataObject"/> (the CCW round-trips to
    /// the same managed instance), so the LIVE payload is handed straight through - no serialization, the in-app fast
    /// path survives even when the gesture is running through the OS.</para></summary>
    public static IDataPackage Read(ComTypes.IDataObject data)
    {
        if (data is Win32DataObject ours) return ours.Package;

        var package = new DataPackage();
        if (ReadFiles(data) is { Length: > 0 } files) package.Set(DataFormats.Files, files);
        if (ReadText(data) is { } text) package.Set(DataFormats.Text, text);
        return package;
    }

    private static string[] ReadFiles(ComTypes.IDataObject data)
    {
        if (!TryGetMedium(data, (short)Win32Interop.CF_HDROP, out var medium)) return null;
        try
        {
            // The HDROP is the HGLOBAL HANDLE itself - DragQueryFile locks it internally. Handing it a locked pointer
            // instead works only by accident (fixed memory) and reads garbage from a moveable block.
            var drop = medium.unionmember;
            var count = Win32Interop.DragQueryFile(drop, 0xFFFFFFFF, null, 0);
            var files = new string[count];
            var buffer = new char[260 + 1];
            for (uint i = 0; i < count; i++)
            {
                // Ask for the length first: a path can exceed MAX_PATH (extended-length paths, deep OneDrive trees).
                var length = Win32Interop.DragQueryFile(drop, i, null, 0);
                if (length + 1 > buffer.Length) buffer = new char[length + 1];
                var written = Win32Interop.DragQueryFile(drop, i, buffer, (uint)buffer.Length);
                files[i] = new string(buffer, 0, (int)written);
            }
            return files;
        }
        finally
        {
            Win32Interop.ReleaseStgMedium(ref medium);
        }
    }

    private static string ReadText(ComTypes.IDataObject data)
    {
        if (TryGetMedium(data, (short)Win32Interop.CF_UNICODETEXT, out var unicode))
        {
            try { return ReadGlobalString(unicode.unionmember, true); }
            finally { Win32Interop.ReleaseStgMedium(ref unicode); }
        }
        if (TryGetMedium(data, (short)Win32Interop.CF_TEXT, out var ansi))
        {
            try { return ReadGlobalString(ansi.unionmember, false); }
            finally { Win32Interop.ReleaseStgMedium(ref ansi); }
        }
        return null;
    }

    private static bool TryGetMedium(ComTypes.IDataObject data, short format, out STGMEDIUM medium)
    {
        medium = default;
        var request = new FORMATETC
        {
            cfFormat = format,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };
        if (data.QueryGetData(ref request) != OleResult.Ok) return false;
        try
        {
            data.GetData(ref request, out medium);
        }
        catch (Exception)
        {
            return false;   // a source that advertises a format it can't actually render must not break the drop
        }
        return medium.unionmember != IntPtr.Zero;
    }

    private static string ReadGlobalString(IntPtr global, bool unicode)
    {
        var pointer = Win32Interop.GlobalLock(global);
        if (pointer == IntPtr.Zero) return null;
        try
        {
            return unicode ? Marshal.PtrToStringUni(pointer) : Marshal.PtrToStringAnsi(pointer);
        }
        finally
        {
            Win32Interop.GlobalUnlock(global);
        }
    }

    /// <summary>The payload's text, if it carries any: an explicit <c>DataFormats.Text</c> entry, else a bare string
    /// payload (so <c>DragData="{Binding SomeString}"</c> is draggable into Notepad with no extra ceremony), else a
    /// dragged multi-selection - one line per item, which is what an editor expects from a list of things.</summary>
    public static string TextOf(IDataPackage package)
    {
        if (package?.Get(DataFormats.Text) as string is { } explicitText) return explicitText;
        if (package?.Get<string>() is { } single) return single;
        if (package?.Get<IEnumerable<string>>() is { } many) return string.Join(Environment.NewLine, many);
        return null;
    }

    /// <summary>The payload's file paths, if it carries any (<c>DataFormats.Files</c> as a string[] or any string
    /// sequence).</summary>
    public static string[] FilesOf(IDataPackage package) => package?.Get(DataFormats.Files) switch
    {
        string[] paths => paths,
        IEnumerable<string> paths => [.. paths],
        string single => [single],
        _ => null,
    };

    /// <summary>An HGLOBAL holding a NUL-terminated UTF-16 string, as CF_UNICODETEXT expects. We publish text ONLY as
    /// Unicode (every app that matters takes it); the ANSI CF_TEXT is read on the way IN, never offered on the way out,
    /// so nothing has to guess a codepage.</summary>
    public static IntPtr CreateTextGlobal(string text)
    {
        return CreateGlobal(System.Text.Encoding.Unicode.GetBytes((text ?? string.Empty) + '\0'));
    }

    /// <summary>An HGLOBAL holding a CF_HDROP block: a <see cref="DROPFILES"/> header followed by the NUL-terminated
    /// paths and one closing NUL.</summary>
    public static IntPtr CreateHDropGlobal(IReadOnlyList<string> files)
    {
        var header = Marshal.SizeOf<DROPFILES>();
        var list = new System.Text.StringBuilder();
        foreach (var file in files)
        {
            list.Append(file);
            list.Append('\0');
        }
        list.Append('\0');
        var listBytes = System.Text.Encoding.Unicode.GetBytes(list.ToString());

        var bytes = new byte[header + listBytes.Length];
        var drop = new DROPFILES { pFiles = (uint)header, fWide = true };
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(drop, pinned.AddrOfPinnedObject(), false);
        }
        finally
        {
            pinned.Free();
        }
        Buffer.BlockCopy(listBytes, 0, bytes, header, listBytes.Length);
        return CreateGlobal(bytes);
    }

    private static IntPtr CreateGlobal(byte[] bytes)
    {
        var global = Win32Interop.GlobalAlloc(Win32Interop.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (global == IntPtr.Zero) return IntPtr.Zero;
        var target = Win32Interop.GlobalLock(global);
        if (target == IntPtr.Zero)
        {
            Win32Interop.GlobalFree(global);
            return IntPtr.Zero;
        }
        try
        {
            Marshal.Copy(bytes, 0, target, bytes.Length);
        }
        finally
        {
            Win32Interop.GlobalUnlock(global);
        }
        return global;
    }

    /// <summary>A byte-for-byte copy of an HGLOBAL - what <c>GetData</c> must hand out, since the caller releases what
    /// it receives and we keep ours.</summary>
    public static IntPtr CopyGlobal(IntPtr source)
    {
        var size = (int)Win32Interop.GlobalSize(source);
        if (size <= 0) return IntPtr.Zero;
        var pointer = Win32Interop.GlobalLock(source);
        if (pointer == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, size);
            return CreateGlobal(bytes);
        }
        finally
        {
            Win32Interop.GlobalUnlock(source);
        }
    }

    public static DragDropEffects ToEffects(DropEffect effect)
    {
        var result = DragDropEffects.None;
        if ((effect & DropEffect.Copy) != 0) result |= DragDropEffects.Copy;
        if ((effect & DropEffect.Move) != 0) result |= DragDropEffects.Move;
        if ((effect & DropEffect.Link) != 0) result |= DragDropEffects.Link;
        return result;
    }

    public static DropEffect ToDropEffect(DragDropEffects effects)
    {
        var result = DropEffect.None;
        if ((effects & DragDropEffects.Copy) != 0) result |= DropEffect.Copy;
        if ((effects & DragDropEffects.Move) != 0) result |= DropEffect.Move;
        if ((effects & DragDropEffects.Link) != 0) result |= DropEffect.Link;
        return result;
    }

    /// <summary>The drag's modifier state as the engine's own flags. Only what a drag reads (Ctrl/Shift/Alt + the held
    /// button) - the OS gives no left/right distinction here, so both sides are reported.</summary>
    public static InputModifiers ToModifiers(OleKeyState keyState)
    {
        var modifiers = InputModifiers.None;
        if ((keyState & OleKeyState.Control) != 0) modifiers |= InputModifiers.LeftControl | InputModifiers.RightControl;
        if ((keyState & OleKeyState.Shift) != 0) modifiers |= InputModifiers.LeftShift | InputModifiers.RightShift;
        if ((keyState & OleKeyState.Alt) != 0) modifiers |= InputModifiers.LeftAlt | InputModifiers.RightAlt;
        if ((keyState & OleKeyState.LeftButton) != 0) modifiers |= InputModifiers.LeftMouseButton;
        if ((keyState & OleKeyState.RightButton) != 0) modifiers |= InputModifiers.RightMouseButton;
        if ((keyState & OleKeyState.MiddleButton) != 0) modifiers |= InputModifiers.MiddleMouseButton;
        return modifiers;
    }
}
