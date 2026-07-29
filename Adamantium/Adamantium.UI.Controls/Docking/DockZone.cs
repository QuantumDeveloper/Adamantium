using System;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Where a pane sits, or where a drop would put it - and, combined, where a pane is ALLOWED to be.
/// <para>One enum rather than a value type plus a matching set type: two parallel lists of the same members drift the
/// moment a zone is added to one and not the other, and nothing complains. Flags cover both readings - a PLACE is one
/// bit (<see cref="Pane.Zone"/>), a PERMISSION is several (<see cref="Pane.Allowed"/>). The price is that the compiler
/// will not stop someone writing <c>Left | Right</c> as a place; the layout treats an unknown combination as its first
/// set bit rather than inventing a meaning for it.</para>
/// <para>Plain data on purpose - a view-model may name a zone without referencing anything visual, the same way it
/// names a <c>CursorType</c> rather than building a cursor.</para>
/// </summary>
[Flags]
public enum DockZone
{
    None = 0,

    /// <summary>The main area - where documents live.</summary>
    Center = 1 << 0,

    Left = 1 << 1,

    Top = 1 << 2,

    Right = 1 << 3,

    Bottom = 1 << 4,

    /// <summary>Not docked at all: a root of its own, in its own window.</summary>
    Floating = 1 << 5,

    /// <summary>Any edge - not the document area, not floating.</summary>
    Edges = Left | Top | Right | Bottom,

    All = Center | Edges | Floating
}
