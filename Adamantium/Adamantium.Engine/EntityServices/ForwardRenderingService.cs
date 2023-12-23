using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.NoiseGenerator;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;
using Adamantium.Win32;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.EntityServices
{
    public class ForwardRenderingService : RenderingService
    {
        public ForwardRenderingService(EntityWorld world, GameOutput window) : base(world, window)
        {
        }

        public override void LoadContent()
        {
            CreateResources();
            CreateSystemResources();
        }

        protected override void OnWindowParametersChanging(ChangeReason reason)
        {
            DisposeWindowsResources();
        }

        protected override void OnWindowParametersChanged(ChangeReason reason)
        {
            CreateSystemResources();
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
        }

        public sealed override void CreateSystemResources()
        {
            CreateWindowResources();
        }

        protected override void OnDeviceChangeBegin()
        {
            base.OnDeviceChangeBegin();
        }
        
        protected override void OnDeviceChangeEnd()
        {
            base.OnDeviceChangeEnd();
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice.CreateDeferred();
            //DeferredDevice = GraphicsDeviceService.GraphicsDevice;
            //spriteBatch = new SpriteBatch(DeferredDevice, 25000);
            //CreateResources();
        }

        private void CreateResources()
        {

        }

        private void CreateWindowResources()
        {
        }

        private void DisposeWindowsResources()
        {
        }

        public override void Draw(AppTime gameTime)
        {
            if (ActiveCamera == null)
            {
                return;
            }
            
            

//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;

            /*
            Text += "Camera view direction = " + ActiveCamera.Forward + "\n";
            Text += "Offset: " + ActiveCamera.GetOwnerPosition() + "\n";
            Text += "Velocity: " + ActiveCamera.Velocity + "\n";
            Text += "DragVelocity: " + ActiveCamera.DragVelocity + "\n";
            Text += "Mouse sensitivity: " + ActiveCamera.MouseSensitivity + "\n";
            Text += "Keyboard rotation speed: " + ActiveCamera.RotationSpeed + "\n";
            Text += "Camera type: " + ActiveCamera.Type + "\n";
            Text += ToolsService.Text;

            Text += wireframeWidthValue + "\n";
            Text += patternPeriod + "\n";


            if (InputService.IsKeyDown(Keys.Digit1))
            {
                wireframeWidthValue += 0.1f;
            }
            if (InputService.IsKeyDown(Keys.Digit2))
            {
                wireframeWidthValue -= 0.1f;
                if (wireframeWidthValue < 0)
                {
                    wireframeWidthValue = 0;
                }
            }
            if (InputService.IsKeyDown(Keys.Digit3))
            {
                patternPeriod += 0.1f;
            }
            if (InputService.IsKeyDown(Keys.Digit4))
            {
                patternPeriod -= 0.1f;
                if (patternPeriod < 0)
                {
                    patternPeriod = 0;
                }
            }

            //DeferredDevice.SetDepthStencilState(DeferredDevice.DepthStencilStates.DepthDisable);
            //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.Default);

            //skybox.Parameters["WVP"].SetValue(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
            //skybox.Parameters["skyboxTexture"].SetResource(cube);
            //skybox.Parameters["skyBoxSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);

            //skybox.Techniques[0].Passes[0].Apply();
            //var celestialGeometry = skyCube.GetComponent<CelestialBodyGeometry>();
            //celestialGeometry.Draw(DeferredDevice);

            //DeferredDevice.SetDepthStencilState(DeferredDevice.DepthStencilStates.DepthEnable);

            BasicEffect.Parameters["viewMatrix"].SetValue(ActiveCamera.ViewMatrix);
            BasicEffect.Parameters["projectionMatrix"].SetValue(ActiveCamera.ProjectionMatrix);
            BasicEffect.Parameters["sampleType"].SetResource(DeferredDevice.SamplersStates.AnisotropicWrap);

            BasicEffect.Parameters["LightPosition"].SetValue(new Vector3F(0, 1000, 0));
            BasicEffect.Parameters["globalAmbient"].SetValue(new Vector4F(0.5f, 0.5f, 0.5f, 1.0f));


            BasicEffect.Parameters["cameraPos"].SetValue(Vector3F.Zero);


            BasicEffect.Parameters["LightDirection"].SetValue(Vector3F.Down);
            BasicEffect.Parameters["LightColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightDiffuseColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightSpecularColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["LightAmbientColor"].SetValue(new Vector4F(0.5f, 0.5f, 0.5f,
               0.2f));

            BasicEffect.Parameters["MaterialDiffuseColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["MaterialSpecularColor"].SetValue(Colors.White.ToVector4());
            BasicEffect.Parameters["MaterialAmbientColor"].SetValue(new Vector4F(0.2f, 0.2f, 0.2f, 0.2f));
            BasicEffect.Parameters["Shininess"].SetValue(1.2f);
            

            int drawingCounts = 0;
            int verticesCount = 0;
            foreach (var entity in Entities)
            {
                try
                {
                    entity.TraverseInDepth(
                        current =>
                        {
                            if (!current.Visible)
                            {
                                return;
                            }

                            ContainmentType intersects = ContainmentType.Contains;
                            var collision = current.GetComponent<Collider>();
                            if (collision != null)
                            {
                                if (collision.ContainsDataFor(ActiveCamera))
                                {
                                    var transform = current.Transform.GetMetadata(ActiveCamera);
                                    intersects = collision.IsInsideCameraFrustum(ActiveCamera);
                                    var wvp = transform.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                                    BasicEffect.Parameters["worldMatrix"].SetValue(transform.WorldMatrix);
                                    BasicEffect.Parameters["wvp"].SetValue(wvp);
                                    BasicEffect.Parameters["worldInverseTransposeMatrix"].SetValue(Matrix4x4F.Transpose(Matrix4x4F.Invert(transform.WorldMatrix)));
                                    BasicEffect.Parameters["meshColor"].SetValue(Colors.White.ToVector3());
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
                                    //collision.Draw(DeferredDevice, ActiveCamera);
                                    //BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
                                }
                            }

                            if (intersects != ContainmentType.Disjoint)
                            {
                                var controller = current.GetComponent<AnimationController>();
                                if (controller != null && controller.FinalMatrices.Count > 0)
                                {
                                    var arr = controller.FinalMatrices.Values.ToArray();
                                    BasicEffect.Parameters["Bones"].SetValue(arr);
                                }

                                var renderers = current.GetComponents<MeshRendererBase>();

                                var transformation = current.Transform.GetMetadata(ActiveCamera);
                                if (!transformation.Enabled)
                                {
                                    return;
                                }

                                if (renderers.Length > 0)
                                {
                                    foreach (var component in renderers)
                                    {
                                        var material = current.GetComponent<Material>();
                                        BasicEffect.Parameters["worldMatrix"].SetValue(transformation.WorldMatrix);
                                        BasicEffect.Parameters["wvp"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                                        BasicEffect.Parameters["worldInverseTransposeMatrix"]
                                            .SetValue(
                                                Matrix4x4F.Transpose(Matrix4x4F.Invert(transformation.WorldMatrix)));


                                        DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.Default;
                                        //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.CullBackClipDisabled);

                                        if (material?.Texture != null)
                                        {
                                            BasicEffect.Parameters["shaderTexture"].SetResource(material.Texture);
                                            BasicEffect.Parameters["texturePresent"].SetValue(true);
                                        }
                                        else
                                        {
                                            BasicEffect.Parameters["texturePresent"].SetValue(false);
                                            BasicEffect.Parameters["shaderTexture"].SetResource(nullTexture);
                                        }


                                        if (component is SkinnedMeshRenderer)
                                        {
                                            BasicEffect.Techniques["MeshVertex"].Passes["Skinned"].Apply();
                                            component.Draw(DeferredDevice, gameTime);
                                            BasicEffect.Techniques["MeshVertex"].Passes["Skinned"].UnApply(true);
                                        }

                                        if (component is MeshRenderer)
                                        {
                                            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].Apply();
                                            component.Draw(DeferredDevice, gameTime);
                                            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].UnApply(true);
                                        }


                                        drawingCounts++;


                                        DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
                                        //DeferredDevice.SetBlendState(DeferredDevice.BlendStates.NonPremultiplied);

                                        /*
                                        solidWIreframeEffect.Parameters["World"].SetValue(transformation.WorldMatrix);
                                        solidWIreframeEffect.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["WorldView"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix);
                                        solidWIreframeEffect.Parameters["WorldViewProjection"].SetValue(transformation.WorldMatrix * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                                        solidWIreframeEffect.Parameters["Viewport"].SetValue(new Vector4F(Window.Viewport.Width, Window.Viewport.Height, Window.Viewport.X, Window.Viewport.Y));
                                        solidWIreframeEffect.Parameters["PatternPeriod"].SetValue(patternPeriod);
                                        solidWIreframeEffect.Parameters["LineWidth"].SetValue(wireframeWidthValue);

                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].Apply();
                                        component.Draw(DeferredDevice, gameTime);
                                        solidWIreframeEffect.Techniques[0].Passes["SolidWirePattern"].UnApply(true);
                                        
                                    }
                                }
                            }
                        });
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }
            BasicEffect.Techniques["MeshVertex"].Passes["DirectionalLight"].UnApply(true);
            DrawLightIcons();
            DrawCameraIcons();
            DrawAdditionalStuff();
*/

            //DrawFractal();
            /*
            if (InputService.IsKeyPressed(Keys.B) && InputService.IsKeyPressed(Keys.Control))
            {

                var rootEntity = EntityWorld.CreateEntity("Root Perlin", null);
                for (int k = 0; k < 3; ++k)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        for (int j = 0; j < 3; ++j)
                        {

                            Buffer<VertexPositionNormalTexture> streamOut =
                               Buffer.New<VertexPositionNormalTexture>(DeferredDevice.Device,
                                  voxelsQuantity * voxelsQuantity * voxelsQuantity * 15,
                                  BufferFlags.StreamOutput | BufferFlags.VertexBuffer);
                            DeferredDevice.SetVertexBuffer(vertex);
                            DeferredDevice.SetVertexInputLayout(MCLayout);
                            DeferredDevice.SetStreamOutputTarget(streamOut, 0);

                            //Matrix4x4F wvp = Matrix4x4F.Identity * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                            //MarchingCubesEffect.Parameters["WVP"].SetValue(wvp);
                            //MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);

                            MarchingCubesEffect.Parameters["permutationTable"].SetValue(PerlinNoise.permutationTable);
                            MarchingCubesEffect.Parameters["gradientSet"].SetValue(PerlinNoise.gradientSet.ToArray());
                            MarchingCubesEffect.Parameters["mX"].SetValue(PerlinNoise.mX);
                            MarchingCubesEffect.Parameters["mY"].SetValue(PerlinNoise.mY);
                            MarchingCubesEffect.Parameters["mZ"].SetValue(PerlinNoise.mZ);

                            //MC params
                            MarchingCubesEffect.Parameters["EdgeTable"].SetValue(MarchingCubes.EdgeTable);
                            MarchingCubesEffect.Parameters["tritableTex"].SetResource(lookupTexture);
                            MarchingCubesEffect.Parameters["decal"].SetValue(decals);
                            MarchingCubesEffect.Parameters["isolevel"].SetValue(0.0f);
                            MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);


                            Stopwatch timer = Stopwatch.StartNew();
                            MarchingCubesEffect.Techniques[0].Passes[0].Apply();
                            DeferredDevice.Draw(PrimitiveType.PointList, vertex.ElementCount);
                            MarchingCubesEffect.Techniques[0].Passes[0].UnApply(true);
                            timer.Stop();
                            var ms = timer.ElapsedMilliseconds;
                            //var v = MarchingCubesEffect.Parameters["ContainsTriangles"].GetValue<Vector3F>();
                            string name = "Perlin MC";
                            var entity = EntityWorld.CreateEntity(name, null);

                            //var data = streamOut.GetData();
                            //var mesh = new SkinnedVertex[data.Length];
                            //for (int i = 0; i < data.Length; ++i)
                            //{
                            //   mesh[i] = new SkinnedVertex()
                            //   {
                            //      Position = data[i]
                            //   };
                            //}
                            //Buffer<SkinnedVertex> skinned = Buffer.Vertex.New(DeferredDevice.Device, mesh);
                            //var skinnedGeom = new SkinnedGeometry(skinned, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(skinnedGeom);
                            //entity.AddComponent(new Collision(data));

                            //var geometry = new CelestialBodyGeometry(streamOut, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(geometry);

                            blockOrigin.X += 0.5f;


                        }
                        blockOrigin.Z += 0.5f;
                        blockOrigin.X = 0.0f;
                    }
                    blockOrigin.Y += 0.5f;
                }
                blockOrigin.Y = 0;



                /*
                PerlinNoiseManager noiseManager = new PerlinNoiseManager();
                List<Vector3F> vertexList = new List<Vector3F>();

                vertexList = noiseManager.GenerateBlockOfChunks(new Vector3F(-1.0f, -1.0f, -1.0f), 3, 3.0f, 10);

                if (vertexList.Count > 0)
                {
                   SkinnedVertex[] vertexArray = new SkinnedVertex[vertexList.Count];

                   for (int i = 0; i < vertexList.Count; ++i)
                   {
                      vertexArray[i].Position = vertexList[i];
                      vertexArray[i].Normal = Vector3F.Normalize(vertexArray[i].Position);
                   }

                   Buffer<SkinnedVertex> vertexBuf = Buffer.Vertex.New(DeferredDevice.Device, vertexArray);

                   Entity marchingCubesEntity = EntityWorld.CreateEntity(null, "Marching Cube");
                   marchingCubesEntity.AddComponent(new SkinnedGeometry(vertexBuf, null, PrimitiveType.TriangleList));
                   marchingCubesEntity.AddComponent(new Collision(BoundingOrientedBox.FromPoints(vertexList.ToArray())));
                   }
                  */
                //Perlin
                //mcube.Draw(0.5f, 16, 0, 3);
            /*}
            if (InputService.IsKeyPressed(Keys.N) && InputService.IsKeyPressed(Keys.Control))
            {
                var rootEntity = EntityWorld.CreateEntity("Root Simplex");
                //Simplex
                for (int z = 0; z < 3; ++z)
                {
                    for (int k = 0; k < 3; ++k)
                    {
                        for (int j = 0; j < 3; ++j)
                        {
                            Buffer<VertexPositionNormalTexture> streamOut =
                               Buffer.New<VertexPositionNormalTexture>(DeferredDevice.Device,
                                  voxelsQuantity * voxelsQuantity * voxelsQuantity * 15,
                                  BufferFlags.StreamOutput | BufferFlags.VertexBuffer);

                            DeferredDevice.Device.SetVertexBuffer(vertex);
                            DeferredDevice.Device.SetVertexInputLayout(MCLayout);
                            DeferredDevice.Device.SetStreamOutputTarget(streamOut, 0);

                            //Open simplex params
                            MarchingCubesEffect.Parameters["perm"].SetValue(noise.perm);
                            MarchingCubesEffect.Parameters["permGradIndex3D"].SetValue(noise.perm3D);
                            MarchingCubesEffect.Parameters["Gradients3D"].SetValue(OpenSimplexHLSLpreparation.Gradients3D);

                            //MC params
                            MarchingCubesEffect.Parameters["EdgeTable"].SetValue(MarchingCubes.EdgeTable);
                            MarchingCubesEffect.Parameters["tritableTex"].SetResource(lookupTexture);
                            MarchingCubesEffect.Parameters["decal"].SetValue(decals);
                            MarchingCubesEffect.Parameters["isolevel"].SetValue(0.0f);
                            MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);


                            Stopwatch timer = Stopwatch.StartNew();
                            MarchingCubesEffect.Techniques[1].Passes[0].Apply();
                            DeferredDevice.Device.Draw(PrimitiveType.PointList, vertex.ElementCount);
                            MarchingCubesEffect.Techniques[1].Passes[0].UnApply(true);
                            timer.Stop();
                            var ms = timer.ElapsedMilliseconds;
                            //var v = MarchingCubesEffect.Parameters["ContainsTriangles"].GetValue<Vector3F>();
                            string name = "Simplex MC";
                            var entity = EntityWorld.CreateEntity(name, rootEntity);
                            //var data = streamOut.GetData();
                            //var mesh = new SkinnedVertex[data.Length];
                            //for (int i = 0; i < data.Length; ++i)
                            //{
                            //   mesh[i] = new SkinnedVertex()
                            //   {
                            //      Position = data[i]
                            //   };
                            //}
                            //Buffer<SkinnedVertex> skinned = Buffer.Vertex.New(DeferredDevice.Device, mesh);
                            //var skinnedGeom = new SkinnedGeometry(skinned, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(skinnedGeom);
                            //entity.AddComponent(new Collision(data));

                            //var geometry = new CelestialBodyGeometry(streamOut, null, PrimitiveType.TriangleList);
                            //entity.AddComponent(geometry);

                            blockOrigin.X += 0.5f;
                        }
                        blockOrigin.Z += 0.5f;
                        blockOrigin.X = 0f;
                    }
                    blockOrigin.Y += 0.5f;
                }
                blockOrigin.Y = 0;
                //mcube.Draw(0.5f, 16, 1, 3);
            }
            */
            // Precalculate some constants
//            int textureHalfSize = pointer.Width / 2;
//            int spriteSceneWidth = backBuffer.Width / 2;
//            int spriteSceneHeight = backBuffer.Height / 2;
//            int spriteSceneRadiusWidth = backBuffer.Width / 2 - textureHalfSize;
//            int spriteSceneRadiusHeight = backBuffer.Height / 2 - textureHalfSize;
//
//            // Time used to animate the balls
//            var time = (float)gameTime.TotalTime.TotalSeconds;
//
//            // Draw sprites on the screen
//            var random = new Random(0);
//            const int SpriteCount = 50000;
//
//            if (InputService.IsKeyPressed(Keys.F6))
//            {
//                mode = SpriteSortMode.NoSort;
//            }
//            if (InputService.IsKeyPressed(Keys.F7))
//            {
//                mode = SpriteSortMode.BackToFront;
//            }
//            if (InputService.IsKeyPressed(Keys.F8))
//            {
//                mode = SpriteSortMode.FrontToBack;
//            }
//            if (InputService.IsKeyPressed(Keys.F9))
//            {
//                mode = SpriteSortMode.Texture;
//            }
            /*
            //spriteBatch.Begin(SpriteSortMode.NoSort, GraphicsDeviceService.GraphicsDevice.BlendStates.NonPremultiplied);
            spriteBatch.Begin(mode, GraphicsDeviceService.GraphicsDevice.BlendStates.NonPremultiplied, null, GraphicsDevice.DepthStencilStates.DepthDisable);

            for (int i = 0; i < SpriteCount; i++)
            {
               var angleOffset = (float)random.NextDouble() * Math.PI * 2.0f;
               var radiusOffset = (float)random.NextDouble() * 0.8f + 0.2f;
               var spriteSpeed = (float)random.NextDouble() + 0.1f;

               var position = new Vector2F
               {
                  X = spriteSceneWidth + spriteSceneRadiusWidth * radiusOffset * (float)Math.Cos(time * spriteSpeed + angleOffset),
                  Y = spriteSceneHeight + spriteSceneRadiusHeight * radiusOffset * (float)Math.Sin(time * spriteSpeed + angleOffset)
               };

               spriteBatch.Draw(pointer, position, null, Color.Yellow, 0.0f, Vector2F.Zero, Vector2F.One);
            }

           spriteBatch.DrawString(cambriaBMFont, "Hello world", new Vector2F(300, 50), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.None, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 100), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.None, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 200), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipHorizontally, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 300), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipVertically, 0);
           spriteBatch.DrawString(cambriaTrueType, "Hello world\nNew Line", new Vector2F(300, 400), Color.DarkGreen, 0, Vector2F.Zero, Vector2F.One, SpriteEffects.FlipBoth, 0);

               rotation += 1 * (float)GameTime.FrameTime;
            //spriteBatch.Draw(pointer, mouseState.Position - pointerCenterBlack, Color.Red, pointerScaleBlack);
            spriteBatch.Draw(nullTexture6, new RectangleF(0, 0, 300, 300), null, Color.RosyBrown);
            spriteBatch.Draw(nullTexture5, new RectangleF(1050, 100, 300, 300), null, Color.White, rotation, new Vector2F(300, 300), SpriteEffects.None, 0.1f);
            spriteBatch.Draw(nullTexture2, new RectangleF(250, 100, 300, 300), null, Color.White, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.2f);
            spriteBatch.Draw(new RectangleF(550, 100, 300, 300), Color.Green, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.3f);
            spriteBatch.Draw(nullTexture4, new RectangleF(650, 100, 300, 300), null, Color.White, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.4f);
            spriteBatch.Draw(nullTexture4, new RectangleF(1100, 150, 10, 10), null, Color.White, 0f, new Vector2F(150, 150));
            spriteBatch.Draw(nullTexture3, new RectangleF(750, 100, 300, 300), null, Color.Gray, 0f, new Vector2F(150, 150), SpriteEffects.None, 0.5f);

            spriteBatch.End();
            */
            /*
            var state = InputService.GetGamepadState(0);
            
            Text += "Rendered meshes count: " + drawingCounts + "\n";
            Text += "Rendered vertices count: " + verticesCount + "\n";
            Text += "Rendered triangles count: " + verticesCount / 3 + "\n";
            Text += "Real position: " + InputService.AbsolutePosition + "\n";
            Text += "Relative position: " + InputService.RelativePosition + "\n";
            Text += "Virtual position: " + InputService.VirtualPosition + "\n";
            Text += "Left thumb: " + state.LeftThumb + "\n";
            Text += "Right thumb: " + state.RightThumb + "\n";
            Text += "Left trigger: " + state.LeftTrigger + "\n";
            Text += "Right trigger: " + state.RightTrigger + "\n";
            base.Draw(gameTime);
            */
        }
    }
}

