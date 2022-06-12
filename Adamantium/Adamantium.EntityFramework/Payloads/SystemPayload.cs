using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework.Payloads
{
    public class SystemPayload : ProcessorPayload
    {
        public ISystem System { get; }

        public SystemPayload(EntityService service, ISystem system) : base(service)
        {
            System = system;
        }
    }
}
