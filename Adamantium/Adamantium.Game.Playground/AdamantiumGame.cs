﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
using Adamantium.UI;
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
        
        // --- SUBPIXEL VISUALIZING START ---

        private Entity VisualizeSubpixelRendering(byte[,] subpixels)
        {
            var entity = new Entity(null, "SubpixelVisualizer");
            
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);
            
            var subpixelHeight = 30;
            var subpixelWidth = 10;

            var pixelHeight = 10;
            
            var startPosX = 100;
            var startPosY = 100;

            var spaceX = 3;
            var spaceY = 3;

            var subpixelsQuadList = new List<Vector3F>();
            var pixelsQuadList = new List<Vector3F>();
            var subpixelsColors = new List<Color>();
            var pixelsColors = new List<Color>();

            for (var y = 0; y < height; y++)
            {
                var p0 = new Vector3F();
                var p1 = new Vector3F();
                var p2 = new Vector3F();
                var p3 = new Vector3F();
                
                byte red = 0;
                byte green = 0;
                byte blue = 0;
                
                for (var x = 0; x < width; x++)
                {
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));
                    
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));
                    subpixelsQuadList.Add(new Vector3F(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));

                    var subpixelColor = new Color();

                    if (x % 3 == 0)
                    {
                        subpixelColor = Color.FromRgba(subpixels[x, y], 0, 0, 255);
                        red = subpixels[x, y];

                        p0 = new Vector3F(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + spaceY * y);
                        p3 = new Vector3F(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + pixelHeight + spaceY * y);
                    }

                    if (x % 3 == 1)
                    {
                        subpixelColor = Color.FromRgba(0, subpixels[x, y], 0, 255);
                        green = subpixels[x, y];
                    }

                    if (x % 3 == 2)
                    {
                        subpixelColor = Color.FromRgba(0, 0, subpixels[x, y], 255);
                        blue = subpixels[x, y];

                        p1 = new Vector3F(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + spaceY * y);
                        p2 = new Vector3F(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + pixelHeight + spaceY * y);
                        
                        pixelsQuadList.Add(p0);
                        pixelsQuadList.Add(p1);
                        pixelsQuadList.Add(p2);
                    
                        pixelsQuadList.Add(p0);
                        pixelsQuadList.Add(p2);
                        pixelsQuadList.Add(p3);

                        var pixelColor = new Color();
                        pixelColor = Color.FromRgba(red, green, blue, 255);
                        
                        pixelsColors.Add(pixelColor);
                        pixelsColors.Add(pixelColor);
                        pixelsColors.Add(pixelColor);
                        pixelsColors.Add(pixelColor);
                        pixelsColors.Add(pixelColor);
                        pixelsColors.Add(pixelColor);
                    }

                    subpixelsColors.Add(subpixelColor);
                    subpixelsColors.Add(subpixelColor);
                    subpixelsColors.Add(subpixelColor);
                    subpixelsColors.Add(subpixelColor);
                    subpixelsColors.Add(subpixelColor);
                    subpixelsColors.Add(subpixelColor);
                }
            }

            var mesh = new Mesh();
            mesh.MeshTopology = PrimitiveType.TriangleList;
            
            //subpixelsQuadList.AddRange(pixelsQuadList);
            pixelsQuadList.AddRange(subpixelsQuadList);
            mesh.SetPositions(pixelsQuadList);
            
            //subpixelsColors.AddRange(pixelsColors);
            pixelsColors.AddRange(subpixelsColors);
            mesh.SetColors(pixelsColors);
            
            var meshComponent = new MeshData();
            meshComponent.Mesh = mesh;
            var meshRenderer = new MeshRenderer();
            meshRenderer.Name = "Visualize";
            entity.AddComponent(meshComponent);
            entity.AddComponent(meshRenderer);
            
            return entity;
        }
        
        // --- SUBPIXEL VISUALIZING END ---

        private void SaveAtlas(string path, TextureAtlasData data)
        {
            var img = Image.New2D((uint)data.AtlasSize.Width, (uint)data.AtlasSize.Height, 1, SurfaceFormat.R8G8B8A8.UNorm);
            var pixels = img.GetPixelBuffer(0, 0);
            pixels.SetPixels(data.AtlasColors);
            img.Save(path, ImageFileType.Png);
        }

        uint mtsdfTextureSize = 64;
        private uint subpixelMaxFontSize = 24;

        private Entity PrintText(IFont font, uint fontSize, string text)
        {
            var entity = new Entity(null, "PrintText");
            
            var glyphTextureSize = fontSize <= subpixelMaxFontSize ? fontSize : mtsdfTextureSize;

            var quadList = new List<Vector3F>();
            var uvList = new List<Vector2F>();

            for (var i = 0; i < text.Length; i++)
            {
                var glyph = font.GetGlyphByCharacter(text[i]);

                quadList.Add(new Vector3F(fontSize * i,            0));
                quadList.Add(new Vector3F(fontSize * i + fontSize, 0));
                quadList.Add(new Vector3F(fontSize * i + fontSize, fontSize));
                
                quadList.Add(new Vector3F(fontSize * i,            0));
                quadList.Add(new Vector3F(fontSize * i + fontSize, fontSize));
                quadList.Add(new Vector3F(fontSize * i,            fontSize));

                var glyphUV = glyph.GetTextureAtlasUVCoordinates(glyphTextureSize, 0, font.GlyphCount);

                uvList.Add(new Vector2F((float)glyphUV[0].X,   (float)glyphUV[0].Y));
                uvList.Add(new Vector2F((float)glyphUV[1].X,   (float)glyphUV[0].Y));
                uvList.Add(new Vector2F((float)glyphUV[1].X,   (float)glyphUV[1].Y));

                uvList.Add(new Vector2F((float)glyphUV[0].X,   (float)glyphUV[0].Y));
                uvList.Add(new Vector2F((float)glyphUV[1].X,   (float)glyphUV[1].Y));
                uvList.Add(new Vector2F((float)glyphUV[0].X,   (float)glyphUV[1].Y));
            }

            var mesh = new Mesh();
            mesh.MeshTopology = PrimitiveType.TriangleList;
            mesh.SetPositions(quadList);
            mesh.SetUVs(0, uvList);
            var meshComponent = new MeshData();
            meshComponent.Mesh = mesh;
            var meshRenderer = new MeshRenderer();
            meshRenderer.Name = fontSize <= subpixelMaxFontSize ? "SmallGlyph" : "LargeGlyph";
            entity.AddComponent(meshComponent);
            entity.AddComponent(meshRenderer);
            
            return entity;
        }
        
        private void ImportOTFFont()
        {
            try
            {
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/CFF2/SourceHanSerifVFProtoJP.otf", 3);
                var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/SourceSans3-Regular.otf", 3);
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/GlametrixLight-0zjo.otf", 3);
                //var typeface = TypeFace.LoadFont(@"Fonts/OTFFonts/Japan/NotoSansCJKjp-Light.otf", 3);
                var font = typeface.GetFont(0);
                byte sampleRate = 10;
                uint fontSize = 12;

                /*var colors = glyph.RasterizeGlyphBySubpixels(subpixelGlyphSize, em);
                uint size = subpixelGlyphSize;
                var visSubpixels = glyph.GetVisSubpixels();
                var visEntity = VisualizeSubpixelRendering(visSubpixels);*/

                /*
                var atlasGen = new TextureAtlasGenerator();
                var mtsdfAtlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 0, (int)font.GlyphCount, GeneratorType.Msdf);
                SaveAtlas(@"Textures\mtsdf.png", mtsdfAtlasData);

                var subAtlasData = atlasGen.GenerateTextureAtlas(typeface, font, fontSize, sampleRate, 0, (int)font.GlyphCount, GeneratorType.Subpixel);
                SaveAtlas(@"Textures\subpixel.png", subAtlasData);
                */

                var textEntity = PrintText(font, fontSize, "Привет, майне либовски! Оно таки работает");

                /* // OUTLINES CHECK
                List<Vector3F> vertexList;
                List<Color> colorList;

                glyph.GetSegments(out vertexList, out colorList);                
                
                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.LineList;
                mesh.SetPositions(vertexList);
                mesh.SetColors(colorList);
                var meshComponent = new MeshData();
                meshComponent.Mesh = mesh;
                var meshRenderer = new MeshRenderer();
                meshRenderer.Name = "GlyphOutlines";
                entity.AddComponent(meshComponent);
                entity.AddComponent(meshRenderer);*/

                EntityWorld.AddEntity(textEntity);
                //EntityWorld.AddEntity(visEntity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}