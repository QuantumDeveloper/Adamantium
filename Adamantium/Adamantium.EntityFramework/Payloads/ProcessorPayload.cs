namespace Adamantium.EntityFramework.Payloads
{
    public abstract class ProcessorPayload
    {
        public EntityService EntityService { get; }

        protected ProcessorPayload(EntityService service)
        {
            EntityService = service;
        }
    }
}
