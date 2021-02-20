using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Fonts.TTF;
using Adamantium.Game.Events;

namespace Adamantium.Game.Playground
{
    internal class AdamantiumGame : Engine.Game
    {
        private TTFFontParser parser;
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
                //ImportModel(@"Models\F15C\F-15C_Eagle.dae");
                ImportFont();
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

        private void ImportFont()
        {
            try
            {
                parser = new TTFFontParser(@"PlayfairDisplay-Regular.ttf", 7);
                var entity = new Entity(null, "PlayfairDisplay-Regular");
                var ch = parser.FontData.GetGlyphForCharacter('B');
                parser.GenerateGlyphTriangles(ch);
                var mesh = new Mesh();
                mesh.SetPositions(ch.Vertices);
                var meshComponent = new MeshData();
                meshComponent.Mesh = mesh;
                var meshRenderer = new MeshRenderer();
                entity.AddComponent(meshComponent);
                entity.AddComponent(meshRenderer);
                EntityWorld.AddEntity(entity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}