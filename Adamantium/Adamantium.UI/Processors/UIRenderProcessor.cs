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
        private Dictionary<IWindow, WindowRenderModule> windowToModule;
        private readonly GraphicsDevice GraphicsDevice;
        private MSAALevel msaaLevel;

        public UIRenderProcessor(EntityWorld world, GraphicsDevice graphicsDevice)
            : base(world)
        {
            //GraphicsDevice = graphicsDevice.MainDevice;
            msaaLevel = MSAALevel.X4;
            windowToModule = new Dictionary<IWindow, WindowRenderModule>();
        }

        protected override void OnEntityAdded(Entity entity)
        {
            TraverseByLayer(
                entity,
                current =>
                {
                    var window = entity.GetComponent<IWindow>();
                    if (window != null)
                    {
                        //var renderer = new WindowRenderModule(window, GraphicsDevice, msaaLevel);
                        //if (!windowToModule.ContainsKey(window))
                        //{
                        //    windowToModule.Add(window, renderer);
                        //}
                    }
                });
        }

        protected override void OnEntityRemoved(Entity entity)
        {
            TraverseByLayer(
                entity,
                current =>
                {
                    var window = entity.GetComponent<IWindow>();
                    if (window != null)
                    {
                        if (windowToModule.ContainsKey(window))
                        {
                            windowToModule.Remove(window);
                        }
                    }
                });
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
                            //var renderer = new WindowRenderModule(window, GraphicsDevice, msaaLevel);
                            if (!windowToModule.ContainsKey(window))
                            {
                                //windowToModule.Add(window, renderer);
                            }
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

            //foreach (var window in windowToModule)
            //{
            //    window.Value.Preapare();
            //    window.Value.Render(gameTime, GraphicsDevice);
            //}
        }
    }
}