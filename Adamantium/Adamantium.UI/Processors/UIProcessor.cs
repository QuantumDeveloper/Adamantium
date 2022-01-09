using System;
using System.Collections.Generic;
using Adamantium.EntityFramework;
using Adamantium.UI.Media;

namespace Adamantium.UI.Processors;

public abstract class UIProcessor : EntityProcessor
{
    protected UIProcessor(EntityWorld world)
        : base(world)
    {
    }

    public void TraverseInDepth(IUIComponent visualComponentElement, Action<IUIComponent> action)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(visualComponentElement);
        while (stack.Count > 0)
        {
            var control = stack.Pop();

            action(control);

            foreach (var visual in control.GetVisualDescendants())
            {
                stack.Push(visual as FrameworkComponent);
            }
        }
    }

    public void TraverseByLayer(IUIComponent visualComponentElement, Action<IUIComponent> action)
    {
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(visualComponentElement);
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