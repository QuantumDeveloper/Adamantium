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
using Adamantium.Mathematics;

namespace Adamantium.Game.Sandbox
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
                EntityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, Content, new Vector3(0, 0, 2500))));
        }

        public async Task<Entity> ImportModel(String pathToFile, ContentLoadOptions options = null)
        {
            // Reader produces SceneData (parse now, deserialize a baked binary later); entity assembly stays here.
            var scene = await Content.LoadAsync<SceneData>(pathToFile, options);
            return await ImportModel(scene);
        }

        private async void LoadModels()
        {
            var entity = await ImportModel(SandboxAssets.Models.F15C.F_15C_Eagle_dae);
            EntityWorld.EntityManager.AddEntity(entity);
        }

        /// <summary>Loads a model file and adds it to the scene - the runtime "load model" path the game menu uses.</summary>
        public async Task<Entity> LoadAndAddModel(string pathToFile)
        {
            var entity = await ImportModel(pathToFile);
            EntityWorld.EntityManager.AddEntity(entity);
            return entity;
        }
    }
}