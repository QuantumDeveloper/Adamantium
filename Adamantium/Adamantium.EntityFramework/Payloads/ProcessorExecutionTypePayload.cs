using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework.Payloads
{
    public class ProcessorExecutionTypePayload : ProcessorPayload
    {
        public ProcessorType Type { get; }

        public ExecutionType PreviousExecutionType { get; }

        public ExecutionType CurrentExecutionType { get; }

        public ProcessorExecutionTypePayload(EntityService service, ProcessorType type, ExecutionType previousExecutionType, ExecutionType currentExecutionType) : base(service)
        {
            PreviousExecutionType = previousExecutionType;
            CurrentExecutionType = currentExecutionType;
        }
    }
}
