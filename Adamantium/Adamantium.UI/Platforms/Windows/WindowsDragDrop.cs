using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;
using Adamantium.Win32.Ole;
using Serilog;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// The Windows half of <see cref="INativeDragDrop"/>: OLE. DROP-IN is an <see cref="IDropTarget"/> registered on every
/// window for its whole life; DRAG-OUT is one <c>DoDragDrop</c> per gesture, opted into by the source.
/// <para>
/// THREADING: every OLE call here must happen on the thread that owns the windows (the message pump). Registration
/// already runs there (window creation); a drag-out is REQUESTED from the UI loop thread, so it is posted as a private
/// message to a hidden window of ours and the modal <c>DoDragDrop</c> loop then runs on the pump thread - the same trick
/// the caption drag uses. The loop thread never blocks, so the app keeps updating and rendering throughout the drag.
/// </para>
/// </summary>
public sealed class WindowsDragDrop : INativeDragDrop
{
    // Private message (WM_APP range) that carries a drag-out request from the loop thread to the pump thread.
    private const uint BeginDragMessage = (uint)WindowMessages.App + 3;

    private readonly Dictionary<IWindow, (IntPtr Handle, Win32DropTarget Target)> _targets = new();

    private Win32NativeWindowWrapper _messageWindow;
    private object _helper;
    private bool _helperResolved;

    private IDataPackage _pendingData;
    private DragDropEffects _pendingAllowed;
    private DragGhostImage _pendingGhost;
    private Action<DragDropEffects> _pendingCompleted;
    private volatile bool _dragActive;

    public bool IsAvailable => WindowsOle.IsAvailable;

    public void RegisterDropTarget(IWindow window, INativeDropSink sink)
    {
        if (!IsAvailable || window == null || window.Handle == IntPtr.Zero) return;
        if (_targets.ContainsKey(window)) return;

        EnsureMessageWindow();
        var target = new Win32DropTarget(window, sink, DropHelper());
        var result = Win32Interop.RegisterDragDrop(window.Handle, target);
        if (result != OleResult.Ok)
        {
            Log.Logger.Warning("RegisterDragDrop failed with 0x{Result:X8} - this window won't accept OS drops.", result);
            return;
        }
        // Hold the managed target: OLE keeps only the COM wrapper, and the dictionary is also how we revoke later.
        _targets[window] = (window.Handle, target);
    }

    public void UnregisterDropTarget(IWindow window)
    {
        if (window == null || !_targets.Remove(window, out var registration)) return;
        Win32Interop.RevokeDragDrop(registration.Handle);
    }

    public bool BeginDrag(IWindow source, IDataPackage data, DragDropEffects allowed, DragGhostImage ghost,
        Action<DragDropEffects> completed)
    {
        // The hidden window is created during registration, on the pump thread - never from here (the loop thread).
        if (!IsAvailable || _dragActive || _messageWindow == null) return false;

        _pendingData = data;
        _pendingAllowed = allowed;
        _pendingGhost = ghost;
        _pendingCompleted = completed;
        _dragActive = true;
        Messages.PostMessage(_messageWindow.Handle, BeginDragMessage, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    private void EnsureMessageWindow()
    {
        if (_messageWindow != null) return;
        _messageWindow = new Win32NativeWindowWrapper($"AdamantiumDragDropWindow {Guid.NewGuid()}", 0, 0, 0, 0, 0, 0, 0, IntPtr.Zero);
        _messageWindow.AddHook(OnMessage);
    }

    private IntPtr OnMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != BeginDragMessage) return IntPtr.Zero;
        handled = true;
        RunPendingDrag();
        return IntPtr.Zero;
    }

    // Pump thread. Runs the modal OLE loop for the whole gesture, then reports the outcome back on the UI loop thread.
    private void RunPendingDrag()
    {
        var data = _pendingData;
        var allowed = _pendingAllowed;
        var ghost = _pendingGhost;
        var completed = _pendingCompleted;
        _pendingData = null;
        _pendingGhost = default;
        _pendingCompleted = null;
        if (data == null)
        {
            _dragActive = false;
            return;
        }

        var effects = DragDropEffects.None;
        var dataObject = new Win32DataObject(data);
        try
        {
            AttachDragImage(dataObject, ghost);
            var result = Win32Interop.DoDragDrop(dataObject, new Win32DropSource(),
                OleDataBridge.ToDropEffect(allowed), out var effect);
            if (result == OleResult.DragDrop) effects = OleDataBridge.ToEffects(effect);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "OLE drag-out failed");
        }
        finally
        {
            dataObject.Dispose();
            _dragActive = false;
        }

        var outcome = effects;
        Threading.Dispatcher.CurrentDispatcher?.Post(() => completed?.Invoke(outcome));
    }

    // Hand our baked ghost to the shell so IT carries the picture for the whole gesture - including over other
    // applications, where our own floating window has no business being. Best-effort: a failure just means the plain
    // drag cursor. The shell TAKES OWNERSHIP of the bitmap on success, so it is only deleted when the call fails.
    private void AttachDragImage(Win32DataObject dataObject, DragGhostImage ghost)
    {
        if (ghost.IsEmpty) return;
        if (DragSourceHelper() is not { } helper) return;

        var bitmap = CreateDragBitmap(ghost);
        if (bitmap == IntPtr.Zero) return;

        var image = new SHDRAGIMAGE
        {
            sizeDragImage = new NativeSize(ghost.Width, ghost.Height),
            ptOffset = new NativePoint(ghost.OffsetX, ghost.OffsetY),
            hbmpDragImage = bitmap,
            crColorKey = 0xFFFFFFFF   // CLR_NONE: the bitmap carries per-pixel alpha, no color key
        };
        if (helper.InitializeFromBitmap(ref image, dataObject) != OleResult.Ok)
        {
            Win32Interop.DeleteObject(bitmap);
        }
    }

    // A 32-bit top-down DIB the shell can composite with per-pixel alpha, filled from the premultiplied BGRA readback.
    private static IntPtr CreateDragBitmap(DragGhostImage ghost)
    {
        var screenDc = Win32Interop.GetDC(IntPtr.Zero);
        try
        {
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = ghost.Width,
                biHeight = -ghost.Height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32Interop.BI_RGB
            };
            var bitmap = Win32Interop.CreateDIBSection(screenDc, ref header, Win32Interop.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) return IntPtr.Zero;
            Marshal.Copy(ghost.PremultipliedBgra, 0, bits, Math.Min(ghost.PremultipliedBgra.Length, ghost.Width * ghost.Height * 4));
            return bitmap;
        }
        finally
        {
            Win32Interop.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private IDropTargetHelper DropHelper() => Helper<IDropTargetHelper>();

    private IDragSourceHelper DragSourceHelper() => Helper<IDragSourceHelper>();

    // CLSID_DragDropHelper implements BOTH halves (source + target); resolve the object once and query the side asked for.
    private T Helper<T>() where T : class
    {
        if (!_helperResolved)
        {
            _helperResolved = true;
            var clsid = Win32Interop.ClsidDragDropHelper;
            var iid = typeof(IDropTargetHelper).GUID;
            if (Win32Interop.CoCreateInstance(ref clsid, IntPtr.Zero, Win32Interop.ClsCtxInprocServer, ref iid, out var instance) == OleResult.Ok)
            {
                _helper = instance;
            }
        }
        return _helper as T;
    }
}
