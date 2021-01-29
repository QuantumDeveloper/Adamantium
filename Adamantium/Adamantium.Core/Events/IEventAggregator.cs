namespace Adamantium.Core.Events
{
    public interface IEventAggregator
    {
        T GetEvent<T>() where T: EventBase, new();
    }
}