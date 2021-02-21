using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Processors
{
    public class UIRenderProcessor : UIProcessor
    {
        private IGameTime _gameTime;
        private IWindow window;
        private WindowRenderModule windowRenderModule;
        private readonly GraphicsDevice GraphicsDevice;
        private MSAALevel msaaLevel;

        public UIRenderProcessor(EntityWorld world, GraphicsDevice graphicsDevice)
            : base(world)
        {
            GraphicsDevice = graphicsDevice;
            msaaLevel = MSAALevel.X4;
            EntityWorld.EntityAdded += EntityWorldOnEntityAdded;
            EntityWorld.EntityRemoved += EntityWorldOnEntityRemoved;
        }

        private void EntityWorldOnEntityAdded(object sender, EntityEventArgs e)
        {
            var wnd = e.Entity.GetComponent<IWindow>();
            if (wnd != null)
            {
                windowRenderModule = new WindowRenderModule(wnd, GraphicsDevice, msaaLevel);
            }
        }
        
        private void EntityWorldOnEntityRemoved(object sender, EntityEventArgs e)
        {
            var wnd = e.Entity.GetComponent<IWindow>();
            if (wnd == null) return;
            windowRenderModule?.Dispose();
            windowRenderModule = null;
        }

        public override void LoadContent()
        {
            foreach (var entity in Entities)
            {
                TraverseByLayer(
                    entity,
                    current =>
                    {
                        var window = entity.GetComponent<IWindow>();
                        if (window != null)
                        {
                            windowRenderModule = new WindowRenderModule(window, GraphicsDevice, msaaLevel);
                        }
                    });
            }
        }

        public override void UnloadContent()
        {
        }

        public override bool BeginDraw()
        {
            return IsVisible;
        }

        public override void Draw(IGameTime gameTime)
        {
            _gameTime = gameTime;
            base.Draw(gameTime);

            if (windowRenderModule.Prepare())
            {
                windowRenderModule.Render(gameTime);
            }
            
        }
    }
}