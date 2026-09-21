using System;
using System.Collections.Generic;
using System.Threading;

namespace Adamantium.Core.Events
{
    public class EventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, EventBase> registeredEvents = new ();
        
        public T GetEvent<T>() where T : EventBase, new()
        {
            if (registeredEvents.TryGetValue(typeof(T), out var @event)) 
                return (T)@event;

            @event = new T { SynchronizationContext = SynchronizationContext.Current };
            registeredEvents[typeof(T)] = @event;

            return (T)@event;
        }
    }
}