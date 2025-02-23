namespace Adamantium.ECS.Payloads
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
