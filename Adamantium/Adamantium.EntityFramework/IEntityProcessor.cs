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