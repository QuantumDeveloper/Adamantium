using Adamantium.Graphics.Core;

namespace Adamantium.ECS.Payloads
{
    public class ProcessorPriorityPayload : ProcessorPayload
    {
        public ProcessorType Type { get; }
        public int PreviousPriority { get; }

        public int CurrentPriority { get; }

        public ProcessorPriorityPayload(EntityService service, ProcessorType type, int previousPriority, int currentPriority) : base(service)
        {
            PreviousPriority = previousPriority;
            CurrentPriority = currentPriority;
        }
    }
}
