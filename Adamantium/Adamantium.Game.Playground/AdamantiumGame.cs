using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Fonts;
using Adamantium.Game.Events;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using Image = Adamantium.Imaging.Image;

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
            // var entity = await ImportModel(@"Models\monkey\monkey.dae");
            // EntityWorld.AddEntity(entity);
            // var ent = entity.Dependencies[0];
            // ent.Transform.SetScaleFactor(100);
            // ent.Transform.SetPosition(new Vector3D(500, 300, -150));
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
                //typeFace = TypeFace.LoadFont(@"Fonts/WoffFonts/Sarabun-Regular.woff2", 3);
                typeFace = TypeFace.LoadFont(@"Fonts/OTFFonts/SourceSans3-Regular.otf", 2);
                var entity = new Entity(null, "Sarabun-Regular.woff2");
                var font = typeFace.GetFont(0);
                
                var textLayout = new TextLayout(font, "Приветствую вас, майне либе. Проблема с матрицами решена!", 24, new Rectangle());
                
                var meshComponent = new MeshData();
                meshComponent.Mesh = textLayout.Mesh;
                
                var meshRenderer = new MeshRenderer();
                entity.AddComponent(meshComponent);
                entity.AddComponent(meshRenderer);
                entity.Transform.SetScaleFactor(textLayout.Scale);
                entity.Transform.SetPosition(new Vector3D(0, 0, 1));
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
                var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/SourceSans3-Regular.otf", 3);
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/GlametrixLight-0zjo.otf", 3);
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/Japan/NotoSansCJKjp-Light.otf", 3);
                var entity = new Entity(null, "Poppins-Medium");
                var font = typeface.GetFont(0);
                var glyph = font.GetGlyphByCharacter('A');
                //var glyph = font.GetGlyphByIndex(2710);
                glyph.Sample(10);
                uint msdfTextureSize = 64;
                uint subpixelGlyphSize = 12;

                //var colors = glyph.GenerateDirectMSDF(msdfTextureSize);
                //uint size = msdfTextureSize;

                var colors = glyph.RasterizeGlyphBySubpixels(subpixelGlyphSize);
                uint size = subpixelGlyphSize;

                var img = Image.New2D(size, size, 1, SurfaceFormat.R8G8B8A8.UNorm);
                var pixels = img.GetPixelBuffer(0, 0);
                pixels.SetPixels(colors);
                img.Save(@"Textures\sdf.png", ImageFileType.Png);                
                
                var glyphSize = 250;
                var quadList = new List<Vector3F>();
                quadList.Add(new Vector3F(10));
                quadList.Add(new Vector3F(glyphSize + 10, 10, 0));
                quadList.Add(new Vector3F(glyphSize + 10, glyphSize + 10, 0));
                quadList.Add(new Vector3F(10, glyphSize + 10, 0));

                var uv = new List<Vector2F>();
                
                uv.Add(new Vector2F(0.0f, 0.0f));
                uv.Add(new Vector2F(1.0f, 0.0f));
                uv.Add(new Vector2F(1.0f, 1.0f));
                uv.Add(new Vector2F(0.0f, 1.0f));

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.TriangleList;
                mesh.SetPositions(quadList);
                mesh.SetUVs(0, uv);
                mesh.SetIndices(new[] { 0, 1, 2, 0, 2, 3 });
                var meshComponent = new MeshData();
                meshComponent.Mesh = mesh;
                var meshRenderer = new MeshRenderer();
                entity.AddComponent(meshComponent);
                entity.AddComponent(meshRenderer);
                
                /*
                var points = glyph.Sample(5);
                
                //parser.GenerateGlyphTriangles(ch);
                //parser.GenerateDefaultGlyphTriangles(ch);
                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.LineList;
                mesh.SetPositions(points);
                var meshComponent = new MeshData();
                meshComponent.Mesh = mesh;
                var meshRenderer = new MeshRenderer();
                entity.AddComponent(meshComponent);
                entity.AddComponent(meshRenderer);
                */
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