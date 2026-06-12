using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnit : IDisposable
{
    IUIComponent Component { get; }
    void DeferDispose();
    void Update(Matrix4x4F transform, Matrix4x4F projection);
    void Render();
    void UpdateWithDrawCommand(IDrawCommand command);
    bool Match(IDrawCommand drawCommand);
}