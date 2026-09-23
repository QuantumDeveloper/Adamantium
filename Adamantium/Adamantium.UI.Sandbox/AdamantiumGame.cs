using System;
using System.Threading.Tasks;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Templates;
using Adamantium.ECS;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.UI.Sandbox
{
    public class AdamantiumGame : Universe
    {
        public AdamantiumGame(
            bool enableDynamicRendering, 
            bool enableDebug) :
            base(UniverseMode.Primary, enableDebug)
        {
            EventAggregator.GetEvent<UniverseOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        public AdamantiumGame(
            IGraphicsDeviceService graphicsDeviceService, 
            bool enableDebug) :
            base(UniverseMode.Slave, enableDebug, graphicsDeviceService)
        {
            EventAggregator.GetEvent<UniverseOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(UniverseOutput output)
        {
            var renderingService = EntityWorld.CreateService<RenderingService>(EntityWorld, output);
            var processor = new ForwardRenderingProcessor();
            renderingService.AttachProcessor(processor);
        }

        protected override void Initialize()
        {
            base.Initialize();
            // Editing tools are the demo's choice, not the game's: created before the services that resolve them.
            Container.RegisterInstance<ToolsManager>(new ToolsManager(EntityWorld));
            Container.RegisterInstance<LightManager>(new LightManager(EntityWorld));
            InitializeGameResources();
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            // Kept, not dropped: the task is what anyone waiting for the startup model has to hold on to, and it is
            // also what keeps this from starting a second load - LoadContent runs again when the device is recreated.
            _startupLoad ??= LoadModels();
        }

        private Task _startupLoad;

        private InputService _inputService;

        private void InitializeGameResources()
        {
            try
            {
                _inputService = EntityWorld.CreateService<InputService>(EntityWorld);
                EntityWorld.CreateService<TransformService>(EntityWorld);
                EntityWorld.CreateService<ToolsService>(EntityWorld);
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

        /// <summary>Returns a Task rather than being async void. A void one cannot be awaited and cannot hand its
        /// exception anywhere - a broken model file would have taken the process down without a word.</summary>
        private async Task LoadModels()
        {
            try
            {
                await LoadAndAddModel(SandboxAssets.Models.F15C.F_15C_Eagle_dae);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to load the startup model: {exception}");
            }
        }

        /// <summary>Loads a model file and adds it to the scene - the runtime "load model" path the game menu uses. The
        /// model becomes the subject the camera can follow: in a scene with one thing in it, that thing is the one the
        /// third-person modes are about.</summary>
        public async Task<Entity> LoadAndAddModel(string pathToFile)
        {
            var entity = await ImportModel(pathToFile);
            EntityWorld.EntityManager.AddEntity(entity);

            _inputService.UserControlledEntity = entity;
            return entity;
        }
    }
}