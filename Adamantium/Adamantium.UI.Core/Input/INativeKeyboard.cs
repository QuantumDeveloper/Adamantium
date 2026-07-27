namespace Adamantium.UI.Core.Input;

/// <summary>
/// Live keyboard state straight from the OS. Registered on <see cref="Keyboard.Platform"/> at startup.
/// <para>
/// "Live" is the whole point: these answer what the keyboard looks like RIGHT NOW, not what it looked like when the
/// message currently being processed was queued. Our input is dispatched onto the UI loop thread, and during a mouse
/// capture the queue state lags badly - a drag asking "is Ctrl held?" has to see the physical key. Every platform can
/// answer that (Win32 <c>GetAsyncKeyState</c>, macOS <c>NSEvent.modifierFlags</c>, X11 <c>XQueryKeymap</c>).
/// </para>
/// </summary>
public interface INativeKeyboard
{
    bool IsKeyDown(Key key);

    /// <summary>Is the key TOGGLED on (Caps Lock, Num Lock, Scroll Lock)?</summary>
    bool IsKeyToggled(Key key);
}
