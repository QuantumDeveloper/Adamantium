using System;
using System.Collections.Generic;
using System.Threading;

namespace Adamantium.Core.Events
{
    internal class EventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, EventBase> registeredEvents = new Dictionary<Type, EventBase>();
        
        private readonly SynchronizationContext syncContext = SynchronizationContext.Current;
        
        public T GetEvent<T>() where T : EventBase, new()
        {
            if (registeredEvents.TryGetValue(typeof(T), out var @event)) 
                return (T)@event;
            
            @event = new T {SynchronizationContext = syncContext};
            registeredEvents[typeof(T)] = @event;

            return (T)@event;
        }
    }
}