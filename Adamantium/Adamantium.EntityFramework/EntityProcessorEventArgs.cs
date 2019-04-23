using System;

namespace Adamantium.EntityFramework
{
    public class EntityProcessorEventArgs : EventArgs
    {
        public EntityProcessor Processor { get; }

        public EntityProcessorEventArgs(EntityProcessor system)
        {
            Processor = system;
        }
    }
}