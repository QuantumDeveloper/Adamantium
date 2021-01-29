using System;
using System.Threading.Tasks;

namespace Adamantium.Core.Events
{
    public class BackgroundEventSubscription : EventSubscription
    {
        public BackgroundEventSubscription(Action action) : base(action)
        {
        }

        protected override void InvokeEventInternal(Action action)
        {
            Task.Run(action);
        }
    }
}