using System;
using System.Collections.Generic;
using Adamantium.EntityFramework;

namespace Adamantium.UI.Processors;

public abstract class UiService : EntityService
{
    protected UiService(EntityWorld world)
        : base(world)
    {
    }

    public void TraverseInDepth(IUIComponent visualComponent, Action<IUIComponent> action)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(visualComponent);
        while (stack.Count > 0)
        {
            var control = stack.Pop();

            action(control);

            foreach (var visual in control.GetVisualDescendants())
            {
                stack.Push(visual as MeasurableUIComponent);
            }
        }
    }

    public void TraverseByLayer(IUIComponent visualComponent, Action<IUIComponent> action)
    {
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(visualComponent);
        while (queue.Count > 0)
        {
            var control = queue.Dequeue();

            action(control);

            foreach (var visual in control.GetVisualDescendants())
            {
                queue.Enqueue(visual);
            }
        }
    }

    public void TraverseByLayer(Entity entity, Action<Entity> action)
    {
        var queue = new Queue<Entity>();
        queue.Enqueue(entity);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            action(current);

            foreach (var visual in current.Dependencies)
            {
                queue.Enqueue(visual);
            }
        }
    }
}