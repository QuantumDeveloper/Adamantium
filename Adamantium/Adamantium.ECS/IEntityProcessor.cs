using Adamantium.Core;

namespace Adamantium.ECS;

public interface IEntityProcessor
{
    public void Update(AppTime gameTime);

    // Out-of-render-pass work (e.g. a GPU compute dispatch) recorded in the renderer's beforeRenderPass hook, before
    // the render pass begins and before Draw. No-op for processors that don't need it.
    public void PreRender();

    public void Draw(AppTime gameTime);

    public void EndDraw();

    public void LoadContent();

    public void UnloadContent();

    void Attach(IEntityService service);

    void Detach();

    // Draw/update order within a service's processor collection (ascending). Lower runs first
    // (content < adorner < debug overlays ...), so later processors compose on top in the same frame.
    int Order { get; }

    // When false the owning service skips this processor in Update/Draw/EndDraw.
    bool IsEnabled { get; set; }
}