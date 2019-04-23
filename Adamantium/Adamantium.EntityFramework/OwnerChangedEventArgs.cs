namespace Adamantium.EntityFramework
{
    public class OwnerChangedEventArgs:System.EventArgs
    {
        public OwnerChangedEventArgs(Entity oldOwner, Entity newOwner)
        {
            OlOwner = oldOwner;
            NewOwner = newOwner;
        }

        public Entity OlOwner { get; private set; }

        public Entity NewOwner { get; private set; }
    }
}
