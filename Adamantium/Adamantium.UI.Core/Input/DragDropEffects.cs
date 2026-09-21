using System;

namespace Adamantium.UI.Core.Input;

/// <summary>What a drop target will do with a payload - set by the target in DragOver, drives the cursor feedback.</summary>
[Flags]
public enum DragDropEffects
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}
