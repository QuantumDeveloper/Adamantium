using System;

namespace Adamantium.ECS
{
    public class EntityServiceEventArgs : EventArgs
    {
        public EntityService Service { get; }

        public EntityServiceEventArgs(EntityService system)
        {
            Service = system;
        }
    }
}