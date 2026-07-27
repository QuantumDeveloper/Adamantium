namespace Adamantium.UI.Core.Input;

/// <summary>The standard cursor catalog. Each is a shared, immutable description - the platform resolves it to a native
/// shape once and caches that, so handing the same instance around costs nothing.</summary>
public static class Cursors
{
    public static Cursor Arrow { get; } = new(CursorType.Arrow);

    public static Cursor AppStarting { get; } = new(CursorType.AppStarting);

    public static Cursor Crosshair { get; } = new(CursorType.Crosshair);

    public static Cursor Hand { get; } = new(CursorType.Hand);

    public static Cursor Help { get; } = new(CursorType.Help);

    public static Cursor IBeam { get; } = new(CursorType.IBeam);

    public static Cursor No { get; } = new(CursorType.No);

    public static Cursor SizeAll { get; } = new(CursorType.SizeAll);

    public static Cursor SizeNESW { get; } = new(CursorType.SizeNESW);

    public static Cursor SizeNS { get; } = new(CursorType.SizeNS);

    public static Cursor SizeNWSE { get; } = new(CursorType.SizeNWSE);

    public static Cursor SizeEWE { get; } = new(CursorType.SizeEWE);

    public static Cursor UpArrow { get; } = new(CursorType.UpArrow);

    public static Cursor Wait { get; } = new(CursorType.Wait);

    /// <summary>No pointer at all (hidden while over the element).</summary>
    public static Cursor None { get; } = new(CursorType.None);

    /// <summary>Drag feedback for a COPY. Windows has no standard shape for it, so the Win32 platform ships one as a
    /// <c>.cur</c>; macOS has <c>dragCopyCursor</c> natively - which platform does what is the platform's business.</summary>
    public static Cursor DragCopy { get; } = new(CursorType.DragCopy);

    /// <summary>Drag feedback for a LINK.</summary>
    public static Cursor DragLink { get; } = new(CursorType.DragLink);

    /// <summary>The shared description for a standard shape - the way to turn a <see cref="CursorType"/> that arrived as
    /// DATA (a setting, a view-model's choice) into a cursor without allocating one per read. Null for
    /// <see cref="CursorType.Custom"/>: that one names a FILE, not a shape, so there is nothing shared to hand back -
    /// build it with <c>new Cursor(path)</c>.</summary>
    public static Cursor Of(CursorType type) => type switch
    {
        CursorType.None => None,
        CursorType.Arrow => Arrow,
        CursorType.AppStarting => AppStarting,
        CursorType.Crosshair => Crosshair,
        CursorType.Hand => Hand,
        CursorType.Help => Help,
        CursorType.IBeam => IBeam,
        CursorType.No => No,
        CursorType.SizeAll => SizeAll,
        CursorType.SizeNESW => SizeNESW,
        CursorType.SizeNS => SizeNS,
        CursorType.SizeNWSE => SizeNWSE,
        CursorType.SizeEWE => SizeEWE,
        CursorType.UpArrow => UpArrow,
        CursorType.Wait => Wait,
        CursorType.DragCopy => DragCopy,
        CursorType.DragLink => DragLink,
        _ => null,
    };
}
