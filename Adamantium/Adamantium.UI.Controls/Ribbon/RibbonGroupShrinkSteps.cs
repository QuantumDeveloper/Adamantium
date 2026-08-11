using System;

namespace Adamantium.UI.Controls;

/// <summary>Which steps of its ladder a <see cref="RibbonGroup"/> is allowed to take when the tab runs out of room.
/// The steps themselves are still DERIVED from what the commands allow (see docs/RIBBON_PLAN.md §3.2) - this only says
/// which kinds of them may be used, which is the explicit override the design left room for.</summary>
[Flags]
public enum RibbonGroupShrinkSteps
{
    /// <summary>Pinned: drawn whole whatever happens. The row gives way elsewhere, and once there is nowhere else it
    /// scrolls (§3.4).</summary>
    None = 0,

    /// <summary>May walk down the size variants its commands allow.</summary>
    Sizes = 1,

    /// <summary>May become a button. On its own it means "whole, or a button, and nothing between".</summary>
    Collapse = 2,

    All = Sizes | Collapse
}
