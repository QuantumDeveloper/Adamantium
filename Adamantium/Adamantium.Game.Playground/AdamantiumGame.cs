﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Templates;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Fonts;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Image = Adamantium.Imaging.Image;

namespace Adamantium.Game.Playground
{
    public class AdamantiumGame : Game
    {
        private TypeFace typeFace;
        
        public AdamantiumGame():base(GameMode.Slave)
        {
            EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(GameOutput output)
        {
            EntityWorld.CreateService<ForwardRenderingService>(EntityWorld, output);
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
            return Task.Run(() => EntityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, Content, CameraManager.UserControlledCamera)));
        }

        public Task<Entity> ImportModel(String pathToFile, ContentLoadOptions options = null)
        {
            return Task.Run(() => Content.Load<Entity>(pathToFile, options));
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
                entity.Transform.SetPosition(new Vector3(0, 0, 1));
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

            var subpixelsQuadList = new List<Vector3>();
            var pixelsQuadList = new List<Vector3>();
            var subpixelsColors = new List<Color>();
            var pixelsColors = new List<Color>();

            for (var y = 0; y < height; y++)
            {
                var p0 = new Vector3();
                var p1 = new Vector3();
                var p2 = new Vector3();
                var p3 = new Vector3();
                
                byte red = 0;
                byte green = 0;
                byte blue = 0;
                
                for (var x = 0; x < width; x++)
                {
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));
                    
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + spaceY * y));
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + subpixelWidth + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));
                    subpixelsQuadList.Add(new Vector3(startPosX + subpixelWidth * x + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight + spaceY * y));

                    var subpixelColor = new Color();

                    if (x % 3 == 0)
                    {
                        subpixelColor = Color.FromRgba(subpixels[x, y], 0, 0, 255);
                        red = subpixels[x, y];

                        p0 = new Vector3(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + spaceY * y);
                        p3 = new Vector3(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + pixelHeight + spaceY * y);
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

                        p1 = new Vector3(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + spaceY * y);
                        p2 = new Vector3(startPosX + subpixelWidth * x + subpixelWidth / 2.0f + spaceX * x, startPosY + subpixelHeight * y + subpixelHeight * 2 / 3.0f + pixelHeight + spaceY * y);
                        
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
            mesh.SetPoints(pixelsQuadList);
            
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
        private uint smallFontSizeMaxValue = 24;

        private Entity PrintText(TypeFace typeface, IFont font, uint fontSize, Color foreground, string text)
        {
            var entity = new Entity(null, "PrintText");
            
            var glyphTextureSize = fontSize <= smallFontSizeMaxValue ? fontSize : mtsdfTextureSize;

            var quadList = new List<Vector3>();
            var uvList = new List<Vector2F>();

            double penPosition = 0.0;
            var ascenderLineDiff = fontSize * (font.UnitsPerEm - font.Ascender) / (double)font.UnitsPerEm;

            var layoutContainer = new GlyphLayoutContainer(typeface);
            var glyphs = font.TranslateIntoGlyphs(text);
            layoutContainer.AddGlyphs(glyphs);
            
            // try to apply GPOS kern
            var kernApplied = font.FeatureService.ApplyFeature(FeatureNames.kern, layoutContainer, 0, (uint)glyphs.Length);
            var subApp = font.FeatureService.ApplyFeature(FeatureNames.aalt, layoutContainer, 0, (uint)glyphs.Length);

            for (var i = 0; i < layoutContainer.Count; i++)
            {
                var glyph = layoutContainer.GetGlyph(i); // @TODO move everything inside Layout Container, and remove GetGlyph method
                glyph.CalculateEmRelatedMultipliers(font.UnitsPerEm); // @TODO: need to somehow calculate this at glyph creation time (for each glyph, maybe at parser)

                var positionMultiplier = glyph.EmRelatedCenterToBaseLineMultiplier;

                double left = penPosition - fontSize * positionMultiplier.X;

                // if GPOS kern is not applied - try TTF kern approach
                if (!kernApplied)
                {
                    if (i > 0)
                    {
                        Glyph prevGlyph = layoutContainer.GetGlyph(i - 1); // @TODO move everything inside Layout Container, and remove GetGlyph method
                        left += fontSize * font.GetKerningValue((ushort)prevGlyph.Index, (ushort)glyph.Index) / (double)font.UnitsPerEm;
                    }
                }

                double top = fontSize * positionMultiplier.Y - ascenderLineDiff;

                double right = left + fontSize;
                double bottom = top + fontSize;

                quadList.Add(new Vector3((float) left, (float) top));
                quadList.Add(new Vector3((float) right, (float) top));
                quadList.Add(new Vector3((float) right, (float) bottom));

                quadList.Add(new Vector3((float) left, (float) top));
                quadList.Add(new Vector3((float) right, (float) bottom));
                quadList.Add(new Vector3((float) left, (float) bottom));

                var glyphUV = glyph.GetTextureAtlasUVCoordinates(glyphTextureSize, 0, typeface.GlyphCount);

                var clampX = glyphUV[1].X - glyphUV[0].X;
                var clampY = glyphUV[1].Y - glyphUV[0].Y;

                var multiplier = 0.001;

                clampX *= multiplier;
                clampY *= multiplier;

                glyphUV[0].X += clampX;
                glyphUV[0].Y += clampY;

                glyphUV[1].X -= clampX;
                glyphUV[1].Y -= clampY;

                uvList.Add(new Vector2F((float) glyphUV[0].X, (float) glyphUV[0].Y));
                uvList.Add(new Vector2F((float) glyphUV[1].X, (float) glyphUV[0].Y));
                uvList.Add(new Vector2F((float) glyphUV[1].X, (float) glyphUV[1].Y));

                uvList.Add(new Vector2F((float) glyphUV[0].X, (float) glyphUV[0].Y));
                uvList.Add(new Vector2F((float) glyphUV[1].X, (float) glyphUV[1].Y));
                uvList.Add(new Vector2F((float) glyphUV[0].X, (float) glyphUV[1].Y));

                penPosition += fontSize * glyph.EmRelatedAdvanceWidthMultiplier;

                // if GPOS kern is applied - modify the advance for current glyph
                if (kernApplied)
                {
                    penPosition += fontSize * layoutContainer.GetAdvance((uint)i).X / (double)font.UnitsPerEm;
                }
            }

            var mesh = new Mesh();
            mesh.MeshTopology = PrimitiveType.TriangleList;
            mesh.SetPoints(quadList);
            mesh.SetUVs(0, uvList);
            var meshComponent = new MeshData();
            meshComponent.Mesh = mesh;
            var meshRenderer = new MeshRenderer();
            meshRenderer.Name = fontSize <= smallFontSizeMaxValue ? "SmallGlyph" : "LargeGlyph";
            var material = new Material();
            material.AmbientColor = foreground.ToVector4();
            entity.AddComponent(meshComponent);
            entity.AddComponent(meshRenderer);
            entity.AddComponent(material);
            
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

                /*var colors = glyph.RasterizeGlyphBySubpixels(subpixelGlyphSize, em);
                uint size = subpixelGlyphSize;
                var visSubpixels = glyph.GetVisSubpixels();
                var visEntity = VisualizeSubpixelRendering(visSubpixels);*/

                // Generate MSDF texture
                /*var atlasGen = new TextureAtlasGenerator();
                
                var mtsdfAtlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4,0, (int)typeface.GlyphCount, GeneratorType.Msdf);
                
                SaveAtlas(@"Textures\mtsdf.png", mtsdfAtlasData);*/

                /*var subAtlasData = atlasGen.GenerateTextureAtlas(typeface, font, fontSize, sampleRate, 0, (int)typeface.GlyphCount, GeneratorType.Subpixel);
                SaveAtlas(@"Textures\subpixel.png", subAtlasData);*/

                var testEntity = new Entity(null, "Test");
            
                var quadList = new List<Vector3>();
                var uvList = new List<Vector2F>();
                
                quadList.Add(new Vector3(0, 0));
                quadList.Add(new Vector3(500, 0));
                quadList.Add(new Vector3(500, 500));

                quadList.Add(new Vector3(0, 0));
                quadList.Add(new Vector3(500, 500));
                quadList.Add(new Vector3(0, 500));
                
                uvList.Add(new Vector2F(0, 0));
                uvList.Add(new Vector2F(1, 0));
                uvList.Add(new Vector2F(1, 1));

                uvList.Add(new Vector2F(0, 0));
                uvList.Add(new Vector2F(1, 1));
                uvList.Add(new Vector2F(0, 1));
                
                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.TriangleList;
                mesh.SetPoints(quadList);
                mesh.SetUVs(0, uvList);
                var meshComponent = new MeshData();
                meshComponent.Mesh = mesh;
                var meshRenderer = new MeshRenderer();
                meshRenderer.Name = "Test";
                testEntity.AddComponent(meshComponent);
                testEntity.AddComponent(meshRenderer);
                testEntity.Transform.Position = new Vector3(0, 0, 6);
                
                var textEntity = PrintText(typeface, font, 240, Colors.Beige, "hi all");
                textEntity.Transform.Position = new Vector3(0, 0, 6);

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
                
                //EntityWorld.AddEntity(testEntity);
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