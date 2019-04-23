namespace Adamantium.EntityFramework.ComponentsBasics
{
    public interface IControllableComponent
    {
        T GetOrCreateComponent<T>() where T : class, IComponent, new();

        void AddComponent(IComponent component);

        void RemoveComponent<T>() where T : class, IComponent;

        T GetComponent<T>() where T : class, IComponent;

        T GetComponentInParents<T>() where T : class, IComponent;

        T GetComponentInChildren<T>() where T : class, IComponent;

        T[] GetComponents<T>() where T : class, IComponent;

        T[] GetComponentsInParents<T>() where T : class, IComponent;

        T[] GetComponentsInChildren<T>() where T : class, IComponent;

        bool ContainsComponent<T>() where T : class, IComponent;
    }
}
