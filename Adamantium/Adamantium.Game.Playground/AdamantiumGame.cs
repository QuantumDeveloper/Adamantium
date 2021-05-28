﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Fonts;
using Adamantium.Game.Events;
using Adamantium.Mathematics;

namespace Adamantium.Game.Playground
{
    internal class AdamantiumGame : Engine.Game
    {
        private TypeFace typeFace;
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
                typeFace = TypeFace.LoadFont(@"Fonts/WoffFonts/Sarabun-Regular.woff2", 3);
                var entity = new Entity(null, "Sarabun-Regular.woff2");
                var font = typeFace.GetFont(0);
                var glyph = font.GetGlyphByUnicode('@');
                var points = glyph.Triangulate(3);
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

        private void ImportOTFFont()
        {
            try
            {
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/CFF2/SourceHanSerifVFProtoJP.otf", 3);
                var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/CFF2/AdobeVFPrototype.otf", 3);
                var entity = new Entity(null, "Poppins-Medium");
                var glyph = typeface.GetGlyphByIndex(176);
                var points = glyph.Triangulate(3);
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