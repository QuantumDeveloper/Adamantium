using System;

namespace Adamantium.Core.Events
{
    public class EventSubscription : IEventSubscription
    {
        public EventSubscription(Action action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public Action Action { get; }
        
        public SubscriptionToken Token { get; set; }
        
        public void InvokeEvent(object[] arguments)
        {
            InvokeEventInternal(Action);
        }

        protected virtual void InvokeEventInternal(Action action)
        {
            action?.Invoke();
        }
    }
}