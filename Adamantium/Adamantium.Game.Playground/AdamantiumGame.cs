using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Fonts;
using Adamantium.Fonts.OTF;
using Adamantium.Fonts.TTF;
using Adamantium.Game.Events;

namespace Adamantium.Game.Playground
{
    internal class AdamantiumGame : Engine.Game
    {
        private TTFParser parser;
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

        private async void LoadModels()
        {
            ImportModel(@"Models\monkey\monkey.dae");
            //await ImportModel(@"Models\F15C\F-15C_Eagle.dae");
            //ImportFont();
            ImportOTFFont();
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
                parser = new TTFParser(@"PlayfairDisplay-Regular.ttf", 3);
                var entity = new Entity(null, "PlayfairDisplay-Regular");
                var ch = parser.FontData.GetGlyphForCharacter('@');
                parser.GenerateGlyphTriangles(ch);
                //parser.GenerateDefaultGlyphTriangles(ch);
                var mesh = new Mesh();
                //mesh.MeshTopology = PrimitiveType.PointList;
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

        private void ImportOTFFont()
        {
            try
            {
                var typeface = TypeFace.LoadFont(@"OTFFonts/Poppins-Medium.otf", 3);
                //var otfParser = new OTFParser(@"OTFFonts/Glametrix-oj9A.otf", 3);
                var entity = new Entity(null, "Poppins-Medium");
                var glyph = typeface.GetFont(0).GetGlyphByIndex(25);
                var points = glyph.Triangulate(7);
                //parser.GenerateGlyphTriangles(ch);
                //parser.GenerateDefaultGlyphTriangles(ch);
                var mesh = new Mesh();
                //mesh.MeshTopology = PrimitiveType.LineStrip;
                mesh.SetPositions(points);
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