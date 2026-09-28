using System;
using Adamantium.ECS;

namespace Adamantium.Engine;

/// <summary>
/// What the editor has selected. It outlives any tool: switching tools keeps it.
/// </summary>
public class Selection
{
    private Entity current;

    public Entity Current
    {
        get => current;
        set
        {
            if (current == value)
            {
                return;
            }

            if (current != null)
            {
                current.IsSelected = false;
            }

            current = value;

            if (current != null)
            {
                current.IsSelected = true;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler Changed;
}
