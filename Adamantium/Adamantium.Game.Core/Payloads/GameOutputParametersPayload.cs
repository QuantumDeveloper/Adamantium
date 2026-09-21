namespace Adamantium.Game.Core.Payloads
{
    public class GameOutputParametersPayload
    {
        public GameOutput Output { get; }
        
        public GameWindowDescription Description { get; }
        
        public ChangeReason Reason { get; }

        public GameOutputParametersPayload(GameOutput output, GameWindowDescription description, ChangeReason reason)
        {
            Output = output;
            Description = description.Clone();
            Reason = reason;
        }
    }
}