namespace Adamantium.ECS.Components
{
    public interface ICloneableComponent
    {
        IComponent Clone();

        void CloneValues(IComponent clone);
    }
}