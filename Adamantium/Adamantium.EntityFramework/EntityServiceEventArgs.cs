using System;

namespace Adamantium.EntityFramework
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