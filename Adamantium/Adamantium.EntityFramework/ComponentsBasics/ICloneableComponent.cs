namespace Adamantium.EntityFramework.ComponentsBasics
{
    public interface ICloneableComponent
    {
        IComponent Clone();

        void CloneValues(IComponent clone);
    }
}