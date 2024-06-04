namespace Adamantium.EntityFramework
{
    public class OwnerChangedEventArgs:System.EventArgs
    {
        public OwnerChangedEventArgs(Entity oldOwner, Entity newOwner)
        {
            OldOwner = oldOwner;
            NewOwner = newOwner;
        }

        public Entity OldOwner { get; private set; }

        public Entity NewOwner { get; private set; }
    }
}
