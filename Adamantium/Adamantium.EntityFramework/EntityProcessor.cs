using Adamantium.Core;

namespace Adamantium.EntityFramework;

public interface IEntityProcessor
{
    public void Update(AppTime gameTime);

    public void Draw(AppTime gameTime);

    public void LoadContent();

    public void UnloadContent();

    void Attach(IEntityService service);

    void Detach();
}
public abstract class EntityProcessor<T>: IEntityProcessor where T: class, IEntityService
{
    protected T AssociatedService { get; private set; }
    
    public virtual void Update(AppTime gameTime)
    {
        
    }

    public virtual void Draw(AppTime gameTime)
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