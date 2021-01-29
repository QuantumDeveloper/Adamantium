using System;

namespace Adamantium.Core.Events
{
    public class EventSubscription<TPayload> : IEventSubscription
    {
        private Predicate<TPayload> Filter { get; }
        public Action<TPayload> Action { get; }
        
        public SubscriptionToken Token { get; set; }

        public EventSubscription(Action<TPayload> action, Predicate<TPayload> filter)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Filter = filter;
        }
        
        public void InvokeEvent(object[] arguments)
        {
            var payload = (TPayload)arguments[0];
            if (Filter != null)
            {
                if (Filter(payload))
                {
                    InvokeEventInternal(Action, payload);
                }
            }
            else
            {
                InvokeEventInternal(Action, payload);
            }
        }

        protected virtual void InvokeEventInternal(Action<TPayload> action, TPayload payload)
        {
            Action?.Invoke(payload);
        }
    }
}