using System;
using System.Linq;

namespace Adamantium.Core.Events
{
    public class BasicAggregatorEvent : EventBase
    {
        public SubscriptionToken Subscribe(Action action, ThreadOption threadOption = ThreadOption.PublisherThread)
        {
            IEventSubscription eventSubscription = null;
            switch (threadOption)
            {
                case ThreadOption.PublisherThread:
                    eventSubscription = new EventSubscription(action);
                    break;
                case ThreadOption.BackgroundThread:
                    eventSubscription = new BackgroundEventSubscription(action);
                    break;
                case ThreadOption.UIThread:
                    eventSubscription = new DispatcherEventSubscription(action, SynchronizationContext);
                    break;
            }

            return SubscribeInternal(eventSubscription);
        }

        public void Publish()
        {
            PublishInternal();
        }

        public void Unsubscribe(Action action)
        {
            lock (Subscriptions)
            {
                var subscr = Subscriptions.Cast<EventSubscription>().FirstOrDefault(x => x.Action == action);
                if (subscr != null)
                {
                    Subscriptions.Remove(subscr);
                }
            }
        }

        public bool Contains(Action action)
        {
            lock (Subscriptions)
            {
                var subscr = Subscriptions.Cast<EventSubscription>().FirstOrDefault(x => x.Action == action);
                return subscr != null;
            }
        }
    }
    
    public class BasicAggregatorEvent<TPayload> : EventBase
    {
        public SubscriptionToken Subscribe(
            Action<TPayload> action,
            ThreadOption threadOption = ThreadOption.PublisherThread, 
            Predicate<TPayload> filter = null)
        {
            IEventSubscription eventSubscription = null;
            switch (threadOption)
            {
                case ThreadOption.PublisherThread:
                    eventSubscription = new EventSubscription<TPayload>(action, filter);
                    break;
                case ThreadOption.BackgroundThread:
                    eventSubscription = new BackgroundEventSubscription<TPayload>(action, filter);
                    break;
                case ThreadOption.UIThread:
                    eventSubscription =
                        new DispatcherEventSubscription<TPayload>(action, filter, SynchronizationContext);
                    break;
            }

            return SubscribeInternal(eventSubscription);
        }
        
        public void Publish(TPayload payload)
        {
            PublishInternal(payload);
        }
    }
}