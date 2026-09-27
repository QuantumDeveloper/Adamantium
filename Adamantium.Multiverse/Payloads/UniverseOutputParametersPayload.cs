namespace Adamantium.Multiverse.Payloads
{
    public class UniverseOutputParametersPayload
    {
        public UniverseOutput Output { get; }
        
        public UniverseOutputDescription Description { get; }
        
        public ChangeReason Reason { get; }

        public UniverseOutputParametersPayload(UniverseOutput output, UniverseOutputDescription description, ChangeReason reason)
        {
            Output = output;
            Description = description.Clone();
            Reason = reason;
        }
    }
}