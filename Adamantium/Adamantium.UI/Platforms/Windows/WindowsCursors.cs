using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Win32 <see cref="INativeCursors"/>: maps a <see cref="CursorType"/> onto an <c>IDC_*</c> shared cursor and hands it
/// to <c>SetCursor</c>. Resolved handles are cached - <see cref="Apply"/> is called on every mouse move during a drag.
/// </summary>
internal sealed class WindowsCursors : INativeCursors
{
    // The drag-COPY shape has no IDC_* equivalent on Windows, so it ships as a .cur next to the app.
    private const string DragCopyFile = "dragcopy.cur";

    private readonly Dictionary<CursorType, IntPtr> _standard = new();
    private readonly Dictionary<string, IntPtr> _custom = new();

    public void Apply(Cursor cursor)
    {
        Win32Interop.SetCursor(Resolve(cursor));
    }

    private IntPtr Resolve(Cursor cursor)
    {
        if (cursor == null || cursor.Type == CursorType.None) return IntPtr.Zero;   // NULL = the OS hides the pointer

        if (cursor.Type == CursorType.Custom)
        {
            return cursor.FilePath is { Length: > 0 } path ? LoadFile(path) : DefaultArrow;
        }

        if (_standard.TryGetValue(cursor.Type, out var cached)) return cached;
        var handle = Load(cursor.Type);
        _standard[cursor.Type] = handle;
        return handle;
    }

    private IntPtr Load(CursorType type) => type switch
    {
        CursorType.DragCopy => LoadShipped(DragCopyFile),
        CursorType.DragLink => Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Hand),   // no IDC_ shortcut shape
        _ => Win32Interop.LoadCursor(IntPtr.Zero, Native(type)),
    };

    private static NativeCursors Native(CursorType type) => type switch
    {
        CursorType.AppStarting => NativeCursors.AppStarting,
        CursorType.Crosshair => NativeCursors.Crosshair,
        CursorType.Hand => NativeCursors.Hand,
        CursorType.Help => NativeCursors.Help,
        CursorType.IBeam => NativeCursors.IBeam,
        CursorType.No => NativeCursors.No,
        CursorType.SizeAll => NativeCursors.SizeAll,
        CursorType.SizeNESW => NativeCursors.SizeNESW,
        CursorType.SizeNS => NativeCursors.SizeNS,
        CursorType.SizeNWSE => NativeCursors.SizeNWSE,
        CursorType.SizeEWE => NativeCursors.SizeEWE,
        CursorType.UpArrow => NativeCursors.UpArrow,
        CursorType.Wait => NativeCursors.Wait,
        _ => NativeCursors.Arrow,
    };

    private static IntPtr DefaultArrow => Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow);

    // A cursor file shipped with the app (Resources/ next to the executable).
    private IntPtr LoadShipped(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        return File.Exists(path) ? LoadFile(path) : DefaultArrow;
    }

    private IntPtr LoadFile(string path)
    {
        if (_custom.TryGetValue(path, out var cached)) return cached;
        var handle = Win32Interop.LoadCursorFromFile(path);
        // A missing or broken file must not break input - fall back to the arrow, and remember that so we don't retry
        // the failing load on every mouse move.
        if (handle == IntPtr.Zero) handle = DefaultArrow;
        _custom[path] = handle;
        return handle;
    }
}
