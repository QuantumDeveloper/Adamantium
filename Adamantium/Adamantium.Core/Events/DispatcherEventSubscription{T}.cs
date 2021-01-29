using System;
using System.Threading;

namespace Adamantium.Core.Events
{
    public class DispatcherEventSubscription<TPayload> : EventSubscription<TPayload>
    {
        private readonly SynchronizationContext context;

        public DispatcherEventSubscription(Action<TPayload> action, Predicate<TPayload> filter,
            SynchronizationContext context) : base(action, filter)
        {
            this.context = context;
        }

        protected override void InvokeEventInternal(Action<TPayload> action, TPayload payload)
        {
            context.Post(c => action(payload), null);
        }
    }
}