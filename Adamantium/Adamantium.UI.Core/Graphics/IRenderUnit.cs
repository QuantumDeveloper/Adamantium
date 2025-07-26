using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnit : IDisposable
{
    void Update(Matrix4x4F projection);
    void Render();
    void UpdateWithDrawCommand(IDrawCommand command);
    bool Match(IDrawCommand drawCommand);
}