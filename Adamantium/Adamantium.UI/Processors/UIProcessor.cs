using System;
using System.Collections.Generic;
using Adamantium.EntityFramework;
using Adamantium.UI.Media;

namespace Adamantium.UI.Processors
{
    public abstract class UIProcessor : EntityProcessor
    {
        protected UIProcessor(EntityWorld world)
            : base(world)
        {
        }

        public void TraverseInDepth(IVisualComponent visualComponentElement, Action<IVisualComponent> action)
        {
            var stack = new Stack<IVisualComponent>();
            stack.Push(visualComponentElement);
            while (stack.Count > 0)
            {
                var control = stack.Pop();

                action(control);

                foreach (var visual in control.GetVisualDescends())
                {
                    stack.Push(visual as FrameworkComponent);
                }
            }
        }

        public void TraverseByLayer(IVisualComponent visualComponentElement, Action<IVisualComponent> action)
        {
            var queue = new Queue<IVisualComponent>();
            queue.Enqueue(visualComponentElement);
            while (queue.Count > 0)
            {
                var control = queue.Dequeue();

                action(control);

                foreach (var visual in control.GetVisualDescends())
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
}