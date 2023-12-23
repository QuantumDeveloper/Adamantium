using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public abstract class RenderComponent: ActivatableComponent
    {
        protected Type VertexType { get; set; }

        protected Buffer IndexBuffer { get; set; }

        public abstract void Draw(GraphicsDevice renderContext, AppTime gameTime);
    }
}
