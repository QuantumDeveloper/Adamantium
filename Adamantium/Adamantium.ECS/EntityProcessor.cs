using Adamantium.Core;

namespace Adamantium.ECS;

public abstract class EntityProcessor<T>: IEntityProcessor where T: class, IEntityService
{
    protected T AssociatedService { get; private set; }
    
    public virtual void Update(AppTime gameTime)
    {
        
    }

    public virtual void Draw(AppTime gameTime)
    {
        
    }

    public virtual void EndDraw()
    {
        
    }

    public virtual void LoadContent()
    {
        
    }

    public virtual void UnloadContent()
    {
        
    }

    public void Attach(IEntityService service)
    {
        AssociatedService = (T)service;
        OnAttached();
    }

    public void Detach()
    {
        AssociatedService = null;
        OnDetached();
    }

    protected virtual void OnAttached()
    {
        
    }

    protected virtual void OnDetached()
    {
        
    }
}