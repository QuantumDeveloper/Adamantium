using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public abstract class RenderableComponent: ActivatableComponent
    {
        protected Type VertexType { get; set; }

        protected Buffer IndexBuffer { get; set; }

        public abstract void Draw(GraphicsDevice renderContext, IGameTime gameTime);
    }
}
