using System;
using System.Threading;

namespace Adamantium.Core.Events
{
    public class DispatcherEventSubscription : EventSubscription
    {
        private readonly SynchronizationContext context; 
        
        public DispatcherEventSubscription(Action action, SynchronizationContext context) : base(action)
        {
            this.context = context;
        }

        protected override void InvokeEventInternal(Action action)
        {
            context.Post(c => action(), null);
        }
    }
}