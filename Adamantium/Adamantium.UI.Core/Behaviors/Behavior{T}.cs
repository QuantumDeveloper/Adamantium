namespace Adamantium.UI.Core.Behaviors;

public abstract class Behavior<T> : Behavior where T : IAdamantiumComponent
{
    protected T AssociatedComponent { get; private set; }

    protected override void OnAttached(IAdamantiumComponent adamantiumComponent)
    {
        AssociatedComponent = (T)adamantiumComponent;
        base.OnAttached(adamantiumComponent);
        OnAttached((T)adamantiumComponent);
    }

    protected override void OnDetached(IAdamantiumComponent adamantiumComponent)
    {
        base.OnDetached(adamantiumComponent);
        OnDetached((T)adamantiumComponent);
    }

    protected virtual void OnAttached(T component)
    {
        
    }

    protected virtual void OnDetached(T component)
    {
        
    }
}