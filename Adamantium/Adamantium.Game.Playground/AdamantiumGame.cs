using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.Game.Events;

namespace Adamantium.Game.Playground
{
    internal class AdamantiumGame : Engine.Game
    {
        public AdamantiumGame(GameMode gameMode) : base(gameMode)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(GameOutput output)
        {
            EntityWorld.CreateProcessor<ForwardRenderingProcessor>(EntityWorld, output);
        }

        /// <summary>
        /// Method for initialization of all resources needed for game at startup.
        /// At this point device already initialized
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            InitializeResources();
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            LoadModels();
        }

        private void LoadModels()
        {
            
            Task.Delay(2000).ContinueWith(task =>
            {
                //ImportModel(@"Models\monkey\monkey.dae");
                ImportModel(@"Models\F15C\F-15C_Eagle.dae");
            });
            
        }

        private void InitializeResources()
        {
            try
            {
                EntityWorld.CreateProcessor<InputProcessor>(EntityWorld);
                EntityWorld.CreateProcessor<TransformProcessor>(EntityWorld);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
        
        public Task<Entity> ImportModel(SceneData scene)
        {
            return Task.Run(() => EntityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, Content, CameraService.UserControlledCamera)));
        }

        public Task<Entity> ImportModel(String pathToFile, ContentLoadOptions options = null)
        {
            return Task.Run(() => Content.Load<Entity>(pathToFile, options));
        }
    }
}