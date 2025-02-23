using System;
using Adamantium.Core;
using Adamantium.ECS.ComponentsBasics;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.ECS.Components
{
    public abstract class RenderComponent: ActivatableComponent
    {
        protected Type VertexType { get; set; }

        protected IBuffer IndexBuffer { get; set; }

        public abstract void Draw(IGraphicsDevice renderContext, AppTime gameTime);
    }
}
