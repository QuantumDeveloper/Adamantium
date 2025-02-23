using Adamantium.Graphics.Core;

namespace Adamantium.ECS.Payloads
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
