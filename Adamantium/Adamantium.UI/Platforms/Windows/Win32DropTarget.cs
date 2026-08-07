using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.Win32.Ole;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// The OLE drop target living on ONE of our windows for its whole life (registered at creation, never per drag). The OS
/// calls it - on the window's own message thread, from inside the drag source's modal loop - as a payload from ANY
/// application travels over the window; it reads the payload once and hands the gesture to the engine's
/// <see cref="INativeDropSink"/>, which does the targeting/highlight/drop on the UI loop thread.
/// </summary>
internal sealed class Win32DropTarget : IDropTarget
{
    private readonly IWindow _window;
    private readonly INativeDropSink _sink;
    private readonly IDropTargetHelper _helper;

    private IDataPackage _data;
    private DragDropEffects _allowed;
    private DropEffect _effect;

    public Win32DropTarget(IWindow window, INativeDropSink sink, IDropTargetHelper helper)
    {
        _window = window;
        _sink = sink;
        _helper = helper;
    }

    public int DragEnter(ComTypes.IDataObject data, OleKeyState keyState, NativePoint point, ref DropEffect effect)
    {
        try
        {
            _allowed = OleDataBridge.ToEffects(effect);
            // Read the payload NOW: the source's data object is only guaranteed live inside this call, while the drop is
            // delivered to the view-model a frame later on the UI loop thread.
            _data = OleDataBridge.Read(data);
            _effect = OleDataBridge.ToDropEffect(_sink.DragEnter(_window, _data, new PixelPoint(point.X, point.Y), OleDataBridge.ToModifiers(keyState), _allowed));
            _helper?.DragEnter(_window.Handle, data, ref point, _effect);
        }
        catch (Exception)
        {
            _effect = DropEffect.None;   // never let an exception escape into the OS drag loop
        }
        effect = _effect;
        return OleResult.Ok;
    }

    public int DragOver(OleKeyState keyState, NativePoint point, ref DropEffect effect)
    {
        try
        {
            _allowed = OleDataBridge.ToEffects(effect);
            _effect = OleDataBridge.ToDropEffect(_sink.DragOver(_window, new PixelPoint(point.X, point.Y), OleDataBridge.ToModifiers(keyState), _allowed));
            _helper?.DragOver(ref point, _effect);
        }
        catch (Exception)
        {
            _effect = DropEffect.None;
        }
        effect = _effect;
        return OleResult.Ok;
    }

    public int DragLeave()
    {
        try
        {
            _sink.DragLeave(_window);
            _helper?.DragLeave();
        }
        catch (Exception)
        {
            // swallow - the drag is already leaving
        }
        _data = null;
        _effect = DropEffect.None;
        return OleResult.Ok;
    }

    public int Drop(ComTypes.IDataObject data, OleKeyState keyState, NativePoint point, ref DropEffect effect)
    {
        try
        {
            _allowed = OleDataBridge.ToEffects(effect);
            // Re-read: a source may only fill the payload at drop time (deferred rendering).
            _data = OleDataBridge.Read(data) ?? _data;
            _effect = OleDataBridge.ToDropEffect(_sink.Drop(_window, _data, new PixelPoint(point.X, point.Y), OleDataBridge.ToModifiers(keyState), _allowed));
            _helper?.Drop(data, ref point, _effect);
        }
        catch (Exception)
        {
            _effect = DropEffect.None;
        }
        effect = _effect;
        _data = null;
        return OleResult.Ok;
    }
}
