using System;

namespace Adamantium.Engine.Graphics;

public class GraphicsCommand
{
    public GraphicsCommand(Action action)
    {
        Action = action;
    }
    
    public Action Action { get; }

    public void Execute()
    {
        Action?.Invoke();
        Executed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler Executed;
}