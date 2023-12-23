using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.Fonts;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Mathematics;

namespace Adamantium.Game.Playground
{
    public class AdamantiumGame : Game
    {
        public AdamantiumGame(
            bool enableDynamicRendering, 
            bool enableDebug) :
            base(GameMode.Primary, enableDynamicRendering, enableDebug)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        public AdamantiumGame(
            IGraphicsDeviceService graphicsDeviceService, 
            bool enableDynamicRendering,
            bool enableDebug) :
            base(GameMode.Slave, enableDynamicRendering, enableDebug, graphicsDeviceService)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(GameOutput output)
        {
            // EntityWorld.CreateService<InputService>(EntityWorld);
            // EntityWorld.CreateService<TransformService>(EntityWorld);
            var renderingService = EntityWorld.CreateService<RenderingService>(EntityWorld, output);
            var processor = new ForwardRenderingProcessor();
            renderingService.AttachProcessor(processor);
        }

        protected override void Initialize()
        {
            base.Initialize();
            InitializeGameResources();
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            LoadModels();
        }

        private void InitializeGameResources()
        {
            try
            {
                EntityWorld.CreateService<InputService>(EntityWorld);
                EntityWorld.CreateService<TransformService>(EntityWorld);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        public Task<Entity> ImportModel(SceneData scene)
        {
            return Task.Run(() =>
                EntityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, Content,
                    CameraManager.UserControlledCamera)));
        }

        public Task<Entity> ImportModel(String pathToFile, ContentLoadOptions options = null)
        {
            return Task.Run(() => Content.Load<Entity>(pathToFile, options));
        }

        private async void LoadModels()
        {
            var entity = await ImportModel(@"Models\F15C\F-15C_Eagle.dae");
            EntityWorld.AddEntity(entity);
            var ent = entity.Dependencies[0];
            //ent.Transform.SetScaleFactor(100);
            ent.Transform.SetPosition(new Vector3(0, 0, -150));
        }

    }
}