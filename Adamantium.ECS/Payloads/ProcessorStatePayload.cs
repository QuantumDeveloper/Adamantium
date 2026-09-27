namespace Adamantium.ECS.Payloads
{
    public class ProcessorStatePayload : ProcessorPayload
    {
        public bool State { get; }

        public ProcessorStatePayload(EntityService service, bool state) : base(service)
        {
            State = state;
        }
    }
}
