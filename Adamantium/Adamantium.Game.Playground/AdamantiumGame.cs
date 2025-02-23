using System;
using System.Threading.Tasks;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Templates;
using Adamantium.ECS;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Game.Playground
{
    public class AdamantiumGame : Game
    {
        public AdamantiumGame(
            bool enableDynamicRendering, 
            bool enableDebug) :
            base(GameMode.Primary, enableDebug)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        public AdamantiumGame(
            IGraphicsDeviceService graphicsDeviceService, 
            bool enableDebug) :
            base(GameMode.Slave, enableDebug, graphicsDeviceService)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(GameOutput output)
        {
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
            //var entity = await ImportModel(@"Models\F15C\F-15C_Eagle.dae");
            //EntityWorld.EntityManager.AddEntity(entity);
        }
    }
}