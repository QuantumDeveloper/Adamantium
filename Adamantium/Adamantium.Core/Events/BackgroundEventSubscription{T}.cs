using System;
using System.Threading.Tasks;

namespace Adamantium.Core.Events
{
    public class BackgroundEventSubscription<TPayload> : EventSubscription<TPayload>
    {
        public BackgroundEventSubscription(Action<TPayload> action, Predicate<TPayload> filter) : base(action, filter)
        {
        }

        protected override void InvokeEventInternal(Action<TPayload> action, TPayload payload)
        {
            Task.Run(() => action(payload));
        }
    }
}