using System;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Core.Events
{
    public interface IEventSubscription
    {
        SubscriptionToken Token { get; set; }

        void InvokeEvent(object[] arguments);
    }

    

    


    
    
}