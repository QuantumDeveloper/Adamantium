namespace Adamantium.Game.Core.Payloads
{
    public class GameOutputParametersPayload
    {
        public GameOutput Output { get; }
        public ChangeReason Reason { get; }

        public GameOutputParametersPayload(GameOutput output, ChangeReason reason)
        {
            Output = output;
            Reason = reason;
        }
    }
}