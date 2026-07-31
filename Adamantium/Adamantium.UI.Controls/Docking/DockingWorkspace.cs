using System;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The saved arrangement, owned by a VIEW MODEL: <c>&lt;docking:DockingArea Workspace="{Binding Workspace}"/&gt;</c>.
/// The view model creates one, keeps it, and calls <see cref="Save"/> / <see cref="Load"/> from its own commands.
/// <para>Why an object in between at all: a view model cannot reach the control (it does not know the view), and the
/// control must not decide WHERE a layout is kept - a file, a settings store, a server - which is the application's
/// business. This is the handle the two meet on, and it holds no state of its own.</para>
/// </summary>
public class DockingWorkspace
{
    private DockingArea _area;

    /// <summary>Raised after a layout has been applied - the view model may want to write down that it worked, or
    /// enable the commands that only make sense once there is an arrangement.</summary>
    public event EventHandler Restored;

    /// <summary>Raised when the view is built and an area has attached itself. This is when a view model may restore a
    /// saved arrangement: before it there is nothing to restore INTO, and a view model has no other way to know - it
    /// deliberately cannot see the view.</summary>
    public event EventHandler Ready;

    /// <summary>Whether an area is attached. False before the view is built, which is exactly when a view model would
    /// otherwise try to restore a layout into nothing.</summary>
    public bool IsReady => _area != null;

    /// <summary>The whole arrangement as text, or null when no area is attached yet.</summary>
    public string Save() => _area?.SaveLayout();

    /// <summary>Applies a saved arrangement. False when there is no area yet, or the text is not one this version can
    /// read - the caller keeps what is on screen, which on a first run is the authored arrangement.</summary>
    public bool Load(string state)
    {
        if (_area == null || !_area.LoadLayout(state)) return false;

        Restored?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Called by the attached property when the view is built. A workspace serves ONE area: two would make "save" mean
    // two different arrangements under one name.
    internal void Attach(DockingArea area)
    {
        _area = area;
        Ready?.Invoke(this, EventArgs.Empty);
    }

    internal void Detach(DockingArea area)
    {
        if (ReferenceEquals(_area, area)) _area = null;
    }
}
