using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Adamantium.Core.Events
{
    public abstract class EventBase
    {
        protected EventBase()
        {
            Subscriptions = new List<IEventSubscription>();
        }
        
        internal SynchronizationContext SynchronizationContext { get; set; }
        
        protected ICollection<IEventSubscription> Subscriptions { get; }

        public void Unsubscribe(SubscriptionToken token)
        {
            lock (Subscriptions)
            {
                var subscription = Subscriptions.FirstOrDefault(x => x.Token == token);
                if (subscription != null)
                {
                    Subscriptions.Remove(subscription);
                }
            }
        }

        protected SubscriptionToken SubscribeInternal(IEventSubscription subscription)
        {
            lock (Subscriptions)
            {
                var token = new SubscriptionToken();
                subscription.Token = token;
                Subscriptions.Add(subscription);
                return token;
            }
        }

        protected void PublishInternal(params object[] arguments)
        {
            lock (Subscriptions)
            {
                foreach (var eventSubscription in Subscriptions)
                {
                    eventSubscription.InvokeEvent(arguments);
                }
            }
        }

        public bool ContainsToken(SubscriptionToken token)
        {
            lock (Subscriptions)
            {
                var subscription = Subscriptions.FirstOrDefault(x => x.Token == token);
                return subscription != null;
            }
        }
    }
}