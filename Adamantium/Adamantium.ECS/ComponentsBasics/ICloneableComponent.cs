namespace Adamantium.ECS.ComponentsBasics
{
    public interface ICloneableComponent
    {
        IComponent Clone();

        void CloneValues(IComponent clone);
    }
}