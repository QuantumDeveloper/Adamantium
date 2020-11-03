using System;
using System.Linq;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.Components;
using Adamantium.Game;
using Adamantium.Mathematics;
//using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.EntityFramework.Processors
{
    public class DeferredRenderingProcessor : RenderProcessor
    {
        //Clear Shader
//        Effect Clear;
//        //GBuffer Shader
//        Effect GBuffer;
//
//        //Directional Light Shader
//        Effect directionalLight;
//        //Point Light Shader
//        Effect pointLight;
//        //Spot Light Shader
//        Effect spotLight;
//        //Composition Shader
//        Effect compose;
//
//        private Texture2D AlbedoTexture;
//        private Texture2D NormalTexture;
//        private Texture2D SpecularTexture;
//
//        private RenderTarget2D[] GBufferTargets;
//
//        private Vector2F GBufferTextureSize;
//
//        private RenderTarget2D LightMap;
//        private RenderTarget2D Final;
//        private RenderTarget2D ShadowMap;
//
//        private RenderTargetCube ShadowMapCube;
//
//        private DepthStencilBuffer depthStencilBuffer;
//        private DepthStencilBuffer shadowDepthBuffer;
//
//        private RenderTarget2D _depthBufferRT;
//        private RenderTarget2D _albedoBufferRT;
//        private RenderTarget2D _normalsBufferRT;

        public DeferredRenderingProcessor(EntityWorld world, GameWindow window) : base(world, window)
        {
            //DeferredDevice = GraphicsDevice;
        }

        public override void LoadContent()
        {
            CreateResources();
            CreateSystemResources();
        }

        protected override void OnWindowParametersChanging(ChangeReason reason)
        {
            DisposeSystemResources();
        }

        protected override void OnWindowParametersChanged(ChangeReason reason)
        {
            CreateSystemResources();
        }

        public override void UnloadContent()
        {
            DisposeSystemResources();
            base.UnloadContent();
        }

        protected override void OnDeviceChangeEnd()
        {
            base.OnDeviceChangeEnd();
            //DeferredDevice = GraphicsDevice;
            CreateResources();
        }

        private void CreateResources()
        {
//            Clear = Content.Load<Effect>("Effects/DeferredShading/Clear");
//            GBuffer = Content.Load<Effect>("Effects/DeferredShading/GBuffer");
//            directionalLight = Content.Load<Effect>("Effects/DeferredShading/DirectionalLight");
//            spotLight = Content.Load<Effect>("Effects/DeferredShading/SpotLight");
//            pointLight = Content.Load<Effect>("Effects/DeferredShading/PointLight");
//            compose = Content.Load<Effect>("Effects/DeferredShading/Composition");
//            LoadTextures();
        }

        private void LoadTextures()
        {
//            AlbedoTexture = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultDiffuse.tga");
//            NormalTexture = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultNormal.tga");
//            SpecularTexture = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultSpecular.tga");
        }

        private void DisposeSystemResources()
        {
//            D2dDevice?.Dispose();
//            LightMap?.Dispose();
//            ShadowMapCube?.Dispose();
//            shadowDepthBuffer?.Dispose();
//            depthStencilBuffer?.Dispose();
//            ShadowMap?.Dispose();
//            Final?.Dispose();
//            _albedoBufferRT?.Dispose();
//            _normalsBufferRT?.Dispose();
//            _depthBufferRT?.Dispose();
        }

        public sealed override void CreateSystemResources()
        {
//            GBufferTextureSize = new Vector2F(Window.Width, Window.Height);
//            depthStencilBuffer = DepthStencilBuffer.New(GraphicsDevice, Window.Width, Window.Height, DepthFormat.Depth32Stencil8X24);
//            //shadowDepthBuffer = DepthStencilBuffer.New(GraphicsDevice, 1024, 1024, Window.DepthFormat);
//            _albedoBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, 1, SurfaceFormat.R16G16B16A16.Float);
//            _normalsBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, 1, SurfaceFormat.R16G16B16A16.UNorm);
//            _depthBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, 1, SurfaceFormat.R32G32.Float);
//            LightMap = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, 1, SurfaceFormat.R16G16B16A16.Float);
//            Final = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, 1, Window.PixelFormat);
            //ShadowMap = RenderTarget2D.New(GraphicsDevice, 1024, 1024, 1, SurfaceFormat.R32.Float);

//            Texture2DDescription description = new Texture2DDescription();
//            description.Width = 1024;
//            description.Height = 1024;
//            description.BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;
//            description.Format = SurfaceFormat.R32.Float;
//            description.MipLevels = 1;
//            description.ArraySize = 6;
//            description.SampleDescription = new SampleDescription(1, 0);
//            description.OptionFlags = ResourceOptionFlags.TextureCube;
//            description.Usage = ResourceUsage.Default;
//            description.CpuAccessFlags = CpuAccessFlags.None;

            //ShadowMapCube = RenderTargetCube.New(GraphicsDevice, description);

//            D2dDevice = D2DGraphicDevice.New(DeferredDevice, Final);
        }

        private void MakeLightMap()
        {
            if (Entities.Length == 0)
            {
                return;
            }

//            DeferredDevice.ResetTargets();
//            DeferredDevice.SetTargets(LightMap, null);
//            DeferredDevice.ClearTargets(Colors.Transparent, LightMap);
//
//            Matrix4x4F InverseView = Matrix4x4F.Invert(ActiveCamera.ViewMatrix);
//            Matrix4x4F InverseViewProjection = Matrix4x4F.Invert(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
//
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.LightMap;
//            //DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthReadGreater;
//
//            directionalLight.Parameters["AlbedoSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            directionalLight.Parameters["NormalSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            directionalLight.Parameters["DepthSampler"].SetResource(DeferredDevice.SamplersStates.PointClamp);
//
//            directionalLight.Parameters["AlbedoBuffer"].SetResource(_albedoBufferRT);
//            directionalLight.Parameters["NormalBuffer"].SetResource(_normalsBufferRT);
//            directionalLight.Parameters["DepthBuffer"].SetResource(_depthBufferRT);
//
//
//            directionalLight.Parameters["InverseViewProjection"].SetValue(InverseViewProjection);
//            directionalLight.Parameters["InverseView"].SetValue(InverseView);
//            directionalLight.Parameters["GBufferTextureSize"].SetValue(GBufferTextureSize);
//
//            DeferredDevice.Quad.PrepareDevice();
//
//            foreach (var light in LightService.DirectionalLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(ActiveCamera);
//                directionalLight.Parameters["L"].SetValue(Vector3F.Normalize(transform.WorldMatrix.Forward));
//                directionalLight.Parameters["LightColor"].SetValue(light.Color);
//                directionalLight.Parameters["LightIntensity"].SetValue(light.Intensity);
//                directionalLight.CurrentTechnique.Passes[0].Apply();
//                DeferredDevice.Quad.DrawRaw(false);
//
//                //directionalLight.Parameters["L"].SetValue(Vector3F.Normalize(-transform.WorldMatrix.Forward));
//                //directionalLight.Parameters["LightColor"].SetValue(light.Color);
//                //directionalLight.Parameters["LightIntensity"].SetValue(light.Intensity);
//                //directionalLight.CurrentTechnique.Passes[0].Apply();
//                //DeferredDevice.Quad.DrawRaw(false);
//            }
//            
//            spotLight.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
//            spotLight.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
//            spotLight.Parameters["InverseView"].SetValue(Matrix4x4F.Invert(ActiveCamera.ViewMatrix));
//            spotLight.Parameters["InverseViewProjection"].SetValue(InverseViewProjection);
//            spotLight.Parameters["GBufferTextureSize"].SetValue(new Vector2F(ActiveCamera.Width, ActiveCamera.Height));
//            
//            spotLight.Parameters["AlbedoSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            spotLight.Parameters["NormalSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            spotLight.Parameters["DepthSampler"].SetResource(DeferredDevice.SamplersStates.PointClamp);
//
//            spotLight.Parameters["AlbedoBuffer"].SetResource(_albedoBufferRT);
//            spotLight.Parameters["NormalBuffer"].SetResource(_normalsBufferRT);
//            spotLight.Parameters["DepthBuffer"].SetResource(_depthBufferRT);
//
//            //spotLight.Parameters["GlobalAmbient"].SetValue(new Vector4F(0.05f, 0.05f, 0.05f, 1f));
//            //spotLight.Parameters["LightDiffuseColor"].SetValue(Colors.White.ToVector4());
//            //spotLight.Parameters["LightSpecularColor"].SetValue(Colors.White.ToVector4());
//            //spotLight.Parameters["LightAmbientColor"].SetValue(new Vector4F(0.05f, 0.05f, 0.05f, 1f));
//
//            //spotLight.Parameters["MaterialDiffuseColor"].SetValue(Colors.White.ToVector4());
//            //spotLight.Parameters["MaterialSpecularColor"].SetValue(Colors.White.ToVector4());
//            //spotLight.Parameters["MaterialAmbientColor"].SetValue(Colors.White.ToVector4());
//
//            foreach (var light in LightService.SpotLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(ActiveCamera);
//
//                var world = Matrix4x4F.Scaling(new Vector3F(light.SpotRadius, light.Range, light.SpotRadius)) * Matrix4x4F.RotationQuaternion(light.Owner.Transform.Rotation) * Matrix4x4F.Translation(transform.RelativePosition);
//                //var world = Matrix4x4F.Scaling(new Vector3F(light.Range)) * Matrix4x4F.RotationQuaternion(light.Owner.Transform.Rotation) * Matrix4x4F.Translation(transform.RelativePosition);
//                light.Direction = Vector3F.Normalize(world.Down);
//                var angle = MathHelper.RadiansToDegrees(light.OuterSpotAngle) - light.OuterAngleDelta;
//                var lightAngleCos = (float) Math.Cos(MathHelper.DegreesToRadians(angle));
//                var ang = angle - light.InnerAngleDelta;
//                //var lightAngleCos = (float)Math.Cos(spotAngle * 0.5f);
//                spotLight.Parameters["World"].SetValue(world);
//                spotLight.Parameters["LightPosition"].SetValue(transform.RelativePosition);
//                spotLight.Parameters["LightColor"].SetValue(light.Color);
//                spotLight.Parameters["LightIntensity"].SetValue(light.Intensity);
//                spotLight.Parameters["LightRange"].SetValue(light.Range);
//                spotLight.Parameters["SpotRadius"].SetValue(light.SpotRadius);
//                spotLight.Parameters["LightDirection"].SetValue(light.Direction);
//                spotLight.Parameters["LightOuterConeAngle"].SetValue(MathHelper.DegreesToRadians(angle));
//                spotLight.Parameters["LightInnerConeAngle"].SetValue(MathHelper.DegreesToRadians(ang));
//
//                //Calculate S.L 
//                float SL = Math.Abs(Vector3F.Dot(transform.RelativePosition, light.Direction));
//
//                //Check if SL is within the LightAngle, if so then draw the BackFaces, if not 
//                //then draw the FrontFaces 
//                if (SL < lightAngleCos)
//                    DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullFrontClipDisabled;
//                else
//                    GraphicsDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipDisabled;
//                //GraphicsDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
//                spotLight.CurrentTechnique.Passes[0].Apply();
//                LightService.DrawSpotLightMesh(DeferredDevice, GameTime);
//                //LightService.DrawPointLightMesh(DeferredDevice, GameTime);
//
//                Text += $"AngleCos = {lightAngleCos}"+"\n";
//            }
//
//            pointLight.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
//            pointLight.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
//            pointLight.Parameters["InverseView"].SetValue(Matrix4x4F.Invert(ActiveCamera.ViewMatrix));
//            pointLight.Parameters["InverseViewProjection"].SetValue(InverseViewProjection);
//            pointLight.Parameters["GBufferTextureSize"].SetValue(new Vector2F(ActiveCamera.Width, ActiveCamera.Height));
//
//            //pointLight.Parameters["GlobalAmbient"].SetValue(new Vector4F(0.05f, 0.05f, 0.05f, 1f));
//            //pointLight.Parameters["LightAmbientColor"].SetValue(new Vector4F(0.05f, 0.05f, 0.05f, 1f));
//
//            pointLight.Parameters["AlbedoSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            pointLight.Parameters["NormalSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            pointLight.Parameters["DepthSampler"].SetResource(DeferredDevice.SamplersStates.PointClamp);
//
//            pointLight.Parameters["AlbedoBuffer"].SetResource(_albedoBufferRT);
//            pointLight.Parameters["NormalBuffer"].SetResource(_normalsBufferRT);
//            pointLight.Parameters["DepthBuffer"].SetResource(_depthBufferRT);
//
//            foreach (var light in LightService.PointLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(ActiveCamera);
//                var world = Matrix4x4F.Scaling(light.Range) * Matrix4x4F.Translation(transform.RelativePosition);
//                pointLight.Parameters["World"].SetValue(world);
//                pointLight.Parameters["LightPosition"].SetValue(transform.RelativePosition);
//                pointLight.Parameters["LightRadius"].SetValue(light.Range);
//                pointLight.Parameters["LightColor"].SetValue(light.Color);
//                pointLight.Parameters["LightIntensity"].SetValue(light.Intensity);
//
//                float cameraToCenter = Vector3F.Distance(Vector3F.Zero, transform.RelativePosition);
//
//                if (cameraToCenter <= light.Range)
//                {
//                    DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullFrontClipDisabled;
//                }
//                else
//                {
//                    DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipDisabled;
//                }
//                //GraphicsDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
//                pointLight.CurrentTechnique.Passes[0].Apply();
//
//                LightService.DrawPointLightMesh(DeferredDevice, GameTime);
//            }
//
//            DeferredDevice.SetTargets(null);
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
//            DeferredDevice.RasterizerState  = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;

        }

//        private void MakeFinal(RenderTarget2D output)
//        {
//            DeferredDevice.SetTargets(output);
//            DeferredDevice.ClearTargets(Colors.Transparent, output);
//            compose.Parameters["Albedo"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            compose.Parameters["LightMap"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//
//            compose.Parameters["AlbedoTexture"].SetResource(_albedoBufferRT);
//            compose.Parameters["LightMapTexture"].SetResource(LightMap);
//
//            compose.Parameters["GBufferTextureSize"].SetValue(GBufferTextureSize);
//
//            compose.CurrentTechnique.Passes[0].Apply();
//
//            DeferredDevice.Quad.DrawRaw();
//
//            compose.CurrentTechnique.Passes[0].UnApply();
//        }

        private void MakeGBuffer()
        {
//            DeferredDevice.SetRenderTargets(depthStencilBuffer, _albedoBufferRT, _normalsBufferRT, _depthBufferRT);
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipDisabled;
//            GBuffer.Parameters["View"].SetValue(ActiveCamera.ViewMatrix);
//            GBuffer.Parameters["Projection"].SetValue(ActiveCamera.ProjectionMatrix);
//            GBuffer.Parameters["AlbedoSampler"].SetResource(DeferredDevice.SamplersStates.LinearWrap);
//            //GBuffer.Parameters["NormalSampler"].SetResource(DeferredDevice.SamplersStates.LinearClamp);
//            GBuffer.Parameters["SpecularSampler"].SetResource(DeferredDevice.SamplersStates.LinearWrap);
//            //GBuffer.Parameters["MeshColor"].SetValue(Colors.Red.ToVector4());
//            GBuffer.CurrentTechnique = GBuffer.Techniques[0];
//
//            foreach (var entity in Entities)
//            {
//                entity.TraverseInDepth(DrawEntity);
//            }
        }

        private void DrawEntity(Entity current)
        {
//            var meshRenderer = current.GetComponent<MeshRendererBase>();
//            if (meshRenderer == null)
//            {
//                return;
//            }
//            var material = current.GetComponent<Material>();
//            var transform = current.Transform.GetMetadata(ActiveCamera);
//            GBuffer.Parameters["World"].SetValue(transform.WorldMatrix);
//            GBuffer.Parameters["WorldViewIT"].SetValue(
//                Matrix4x4F.Transpose(Matrix4x4F.Invert(transform.WorldMatrix * ActiveCamera.ViewMatrix)));
//
//            if (material?.Texture != null && !material.Texture.IsDisposed)
//            {
//                GBuffer.Parameters["AlbedoBuffer"].SetResource(material.Texture);
//                //GBuffer.Parameters["NormalMap"].SetResource(material.Texture);
//                GBuffer.Parameters["SpecularMap"].SetResource(material.Texture);
//            }
//            else
//            {
//                GBuffer.Parameters["AlbedoBuffer"].SetResource(AlbedoTexture);
//                //GBuffer.Parameters["NormalMap"].SetResource(NormalTexture);
//                GBuffer.Parameters["SpecularMap"].SetResource(SpecularTexture);
//            }
//
//            if (meshRenderer is SkinnedMeshRenderer)
//            {
//                var controller = current.GetComponent<AnimationController>();
//                if (controller != null && controller.FinalMatrices.Count > 0)
//                {
//                    var arr = controller.FinalMatrices.Values.ToArray();
//                    GBuffer.Parameters["Bones"].SetValue(arr);
//                }
//                GBuffer.Techniques[1].Passes[0].Apply();
//            }
//            else
//            {
//                GBuffer.CurrentTechnique.Passes[0].Apply();
//            }
//
//            meshRenderer.Draw(GraphicsDevice, GameTime);
        }

        private void ClearGBuffer()
        {
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.None;
//            DeferredDevice.BlendState = GraphicsDevice.BlendStates.Opaque;
//            DeferredDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
//            //DeferredDevice.SetRenderTargets(depthStencilBuffer, _albedoBufferRT, _normalsBufferRT, _depthBufferRT);
//            DeferredDevice.SetRenderTargets(null, _albedoBufferRT, _normalsBufferRT, _depthBufferRT);
//            DeferredDevice.ClearDepthStencil(depthStencilBuffer, DepthStencilClearFlags.Depth, 0, 0);
//            Clear.Parameters["AlbedoColor"].SetValue(Colors.Transparent.ToVector4());
//            Clear.CurrentTechnique.Passes[0].Apply();
//            DeferredDevice.Quad.DrawRaw();
        }

        public override void Draw(IGameTime gameTime)
        {
//            base.Draw(gameTime);
//            if (ActiveCamera == null)
//            {
//                return;
//            }
//
//            Text = "FPS: " + GameTime.FpsCount + "\n";
//            Text += "Camera view direction = " + ActiveCamera.Forward + "\n";
//            Text += "Offset: " + ActiveCamera.Owner.Transform.Position + "\n";
//            Text += "Camera type: " + ActiveCamera.Type + "\n";
//            Text += "Camera Window Size:" + ActiveCamera.Width + ": " + ActiveCamera.Height + " \n";
//            Text += "Real Window Size:" + Window.Width + ": " + Window.Height + " \n";
//            Text += "Backbuffer format = " + Window.PixelFormat + " \n";
//
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipEnabled;
//            
//
//            ClearGBuffer();
//            MakeGBuffer();
//            MakeLightMap();
//            MakeFinal(Final);
//
//            DeferredDevice.SetRenderTargets(depthStencilBuffer, Final);
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//
//            DrawLightIcons();
//            DrawCameraIcons();
//            DrawAdditionalStuff();
        }

        protected override void Debug()
        {
//            SpriteBatch.Begin(SpriteSortMode.NoSort, 
//                GraphicsDeviceService.GraphicsDevice.BlendStates.Opaque, 
//                GraphicsDeviceService.GraphicsDevice.SamplersStates.PointClamp,
//                GraphicsDevice.DepthStencilStates.DepthEnableGreater, 
//                GraphicsDevice.RasterizerStates.CullNoneClipDisabled);
//
//            int width = Window.Width / 4;
//            int height = Window.Height / 4;
//            //Draw GBuffer 0
//            SpriteBatch.Draw(_albedoBufferRT, new RectangleF(0, 0, width, height), Colors.White);
//            //Draw GBuffer 1
//            SpriteBatch.Draw(_normalsBufferRT, new RectangleF(width, 0, width, height), Colors.White);
//            //Draw GBuffer 2
//            SpriteBatch.Draw(_depthBufferRT, new RectangleF(width*2, 0, width, height), Colors.White);
//
//            SpriteBatch.Draw(LightMap, new RectangleF(width*3, 0, width, height), Colors.White);
//
//            //End SpriteBatch
//            SpriteBatch.End();
        }

        public override void EndDraw()
        {
//            if (GraphicsDevice.IsD2dSupportEnabled)
//            {
//                D2dDevice.BeginDraw();
//                D2dDevice.DrawText(Text);
//                D2dDevice.EndDraw();
//            }
//
//            if (Final.Description.Width == Window.Width &&
//                Final.Description.Height == Window.Height)
//            {
//                GraphicsDevice.SetTargets(Window.BackBuffer);
//                GraphicsDevice.ClearTargets(Colors.Transparent, ClearOptions.RenderTarget);
//                SpriteBatch.Begin(SpriteSortMode.NoSort,
//                    GraphicsDeviceService.GraphicsDevice.BlendStates.Opaque,
//                    GraphicsDeviceService.GraphicsDevice.SamplersStates.PointClamp,
//                    GraphicsDevice.DepthStencilStates.DepthEnableGreater,
//                    GraphicsDevice.RasterizerStates.CullNoneClipDisabled);
//                SpriteBatch.Draw(Final, new RectangleF(0, 0, Window.Width, Window.Height), Colors.White);
//                //SpriteBatch.Draw(_normalsBufferRT, new RectangleF(0, 0, _normalsBufferRT.Width, _normalsBufferRT.Height), Colors.White);
//                SpriteBatch.End();
//            }
//            base.EndDraw();
        }
    }
}

