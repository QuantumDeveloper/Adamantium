namespace Adamantium.UI.Core.Behaviors;

public interface IAttachedObject
{
    void AttachTo(IAdamantiumComponent adamantiumComponent);
    
    void DetachFrom(IAdamantiumComponent adamantiumComponent);
}

public abstract class Behavior : AdamantiumComponent, IAttachedObject
{
    public void AttachTo(IAdamantiumComponent adamantiumComponent)
    {
        OnAttached(adamantiumComponent);
    }

    public void DetachFrom(IAdamantiumComponent adamantiumComponent)
    {
        OnDetached(adamantiumComponent);
    }

    protected virtual void OnAttached(IAdamantiumComponent adamantiumComponent)
    {
        
    }

    protected virtual void OnDetached(IAdamantiumComponent adamantiumComponent)
    {
        
    }
}