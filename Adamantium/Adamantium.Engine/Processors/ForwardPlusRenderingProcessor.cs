using System;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.Components;
using Adamantium.Imaging;
using Adamantium.Imaging.Dds;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

//using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.EntityFramework.Processors
{
    public class ForwardPlusRenderingProcessor : RenderProcessor
    {
        public ForwardPlusRenderingProcessor(EntityWorld world, GameWindow window) : base(world, window)
        {
        }

//        private Texture2D backBuffer;
//        private ViewportF viewPort;
//
//        private DepthStencilBuffer _depthStencilBuffer;
//        private DepthStencilBuffer _outputDepthStencilBuffer;
//
//        private RenderTarget2D _depthBufferRT;
//        private RenderTarget2D _normalBufferRT;
//        private RenderTarget2D _lightBufferRT;
//        private RenderTarget2D _outputBufferRT;
//
//        private Effect _clearGBuffer;
//        private Effect _lighting;
//        private Effect _reconstructZBuffer;
//        private Effect _mainEffect;
//
//        private Vector3F[] _cornersWorldSpace = new Vector3F[8];
//        private Vector3F[] _cornersViewSpace = new Vector3F[8];
//        private Vector3F[] _currentFrustumCorners = new Vector3F[4];
//
//        private Texture2D _normalMap;
//        private Texture2D _diffuseMap;
//        private Texture2D _specularMap;
//        private Texture2D _emissiveMap;

        private void CreateResources()
        {
//            GraphicsDevice.SetTargets(null);
//            SpriteBatch = new SpriteBatch(GraphicsDevice, 2500);
        }

        public override void LoadContent()
        {
//            SpriteBatch = new SpriteBatch(GraphicsDevice, 2500);
//            CreateSystemResources();
        }

        protected override void OnWindowParametersChanging(ChangeReason reason)
        {
            UnloadContent();
        }

        protected override void OnWindowParametersChanged(ChangeReason reason)
        {
            LoadContent();
        }

        private void CreateWindowResources()
        {
//            Texture2DDescription rendertoTextureDescription = new Texture2DDescription();
//            rendertoTextureDescription.Width = Window.Width;
//            rendertoTextureDescription.Height = Window.Height;
//            rendertoTextureDescription.MipLevels = 1;
//            rendertoTextureDescription.ArraySize = 1;
//            rendertoTextureDescription.Format = Window.PixelFormat;
//            rendertoTextureDescription.SampleDescription.Count = (Int32)Window.MSAALevel;
//            rendertoTextureDescription.SampleDescription.Quality = 0;
//            rendertoTextureDescription.Usage = ResourceUsage.Default;
//            rendertoTextureDescription.BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;
//            rendertoTextureDescription.OptionFlags = ResourceOptionFlags.None;
//            rendertoTextureDescription.CpuAccessFlags = CpuAccessFlags.None;
//
//            backBuffer = Texture2D.New(GraphicsDevice, rendertoTextureDescription);
//
//            // Renderview on the backbuffer
//            //_outputBufferRT = RenderTarget2D.New(GraphicsDevice, backBuffer);
//
//            // Create the depth buffer and depth buffer view
//            _outputDepthStencilBuffer = DepthStencilBuffer.New(GraphicsDevice, new Texture2DDescription()
//            {
//                Format = Format.D32_SFLOAT_S8_UINT,
//                ArraySize = 1,
//                MipLevels = 1,
//                Width = Window.Width,
//                Height = Window.Height,
//                SampleDescription = new SampleDescription((Int32)Window.MSAALevel, 0),
//                Usage = ResourceUsage.Default,
//                BindFlags = BindFlags.DepthStencil,
//                CpuAccessFlags = CpuAccessFlags.None,
//                OptionFlags = ResourceOptionFlags.None
//            });
//
//            _depthStencilBuffer = DepthStencilBuffer.New(GraphicsDevice, Window.Width, Window.Height, Window.DepthFormat);
//            _outputDepthStencilBuffer = DepthStencilBuffer.New(GraphicsDevice, Window.Width, Window.Height, Window.DepthFormat);
//
//            viewPort = new ViewportF(0.0f, 0.0f, Window.Width, Window.Height, 0.0f, 1.0f);
//
//            _depthBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, MSAALevel.None, SurfaceFormat.R32G32.Float);
//            _normalBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, MSAALevel.None, SurfaceFormat.R16G16B16A16.UNorm);
//            _lightBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, MSAALevel.None, SurfaceFormat.R16G16B16A16.Float);
//            _outputBufferRT = RenderTarget2D.New(GraphicsDevice, Window.Width, Window.Height, Window.MSAALevel, Window.PixelFormat);
//
//            if (GraphicsDevice.IsD2dSupportEnabled)
//            {
//                D2dDevice = D2DGraphicDevice.New(GraphicsDevice, _outputBufferRT);
//            }
//
//            LoadEffects();
//            LoadDefaultTextures();
        }

        private void LoadEffects()
        {
//            _mainEffect = Effect.Load("Content/Effects/ForwardPlusShading/ForwardPlusMain.fx.compiled", GraphicsDevice);
//            _reconstructZBuffer = Effect.Load("Content/Effects/ForwardPlusShading/ReconstructDepth.fx.compiled", GraphicsDevice);
//            _clearGBuffer = Effect.Load("Content/Effects/ForwardPlusShading/ClearGBuffer.fx.compiled", GraphicsDevice);
//            _lighting = Effect.Load("Content/Effects/ForwardPlusShading/Lighting.fx.compiled", GraphicsDevice);
        }

        private void LoadDefaultTextures()
        {
//            _normalMap = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultNormal.tga");
//            _diffuseMap = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultDiffuse.tga");
//            _specularMap = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultSpecular.tga");
//            _emissiveMap = Content.Load<Texture2D>("Textures/ForwardPlusDefaultTextures/DefaultEmissive.tga");
        }


        public override void UnloadContent()
        {
//            SpriteBatch?.Dispose();
//            D2dDevice?.Dispose();
//            _clearGBuffer?.Dispose();
//            _mainEffect?.Dispose();
//            _reconstructZBuffer?.Dispose();
//            _lighting?.Dispose();
//
//            base.UnloadContent();
        }

        public sealed override void CreateSystemResources()
        {
            CreateWindowResources();
        }

        protected override void OnDeviceChangeEnd()
        {
            base.OnDeviceChangeEnd();
            CreateResources();
        }

        protected override void OnDeviceChangeBegin()
        {
            //SpriteBatch?.Dispose();
        }


        public override void EndDraw()
        {
//            if (_outputBufferRT.Description.Width == Window.Width &&
//                _outputBufferRT.Description.Height == Window.Height)
//            {
//                GraphicsDevice.ResetTargets();
//                GraphicsDevice.SetTargets(Window.BackBuffer);
//                GraphicsDevice.ClearTargets(Colors.Transparent, ClearOptions.RenderTarget);
//                SpriteBatch.Begin(SpriteSortMode.NoSort,
//                    GraphicsDeviceService.GraphicsDevice.BlendStates.Opaque,
//                    GraphicsDeviceService.GraphicsDevice.SamplersStates.PointClamp,
//                    GraphicsDevice.DepthStencilStates.DepthEnableGreater,
//                    GraphicsDevice.RasterizerStates.CullNoneClipDisabled);
//                SpriteBatch.Draw(_outputBufferRT, new RectangleF(0, 0, Window.Width, Window.Height), Colors.White);
//                SpriteBatch.End();
//            }
//            base.EndDraw();
        }

        public override void Draw(IGameTime gameTime)
        {
            base.Draw(gameTime);
            if (ActiveCamera == null)
            {
                return;
            }

            ComputeFrustumCorners(ActiveCamera);

//            GraphicsDevice.SetRenderTargets(_depthStencilBuffer, _normalBufferRT, _depthBufferRT);
//            DeferredDevice.ClearDepthStencil(_depthStencilBuffer, DepthStencilClearFlags.Depth, 0, 0);
//            //GraphicsDevice.ClearTargets(Colors.Transparent, _depthStencilBuffer, _normalBufferRT, _depthBufferRT);
//            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Opaque;
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
//
//            _clearGBuffer.CurrentTechnique.Passes[0].Apply();
//
//            GraphicsDevice.Quad.DrawRaw();
//
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipEnabled;
//
//            GraphicsDevice.SetRenderTargets(_depthStencilBuffer, _normalBufferRT, _depthBufferRT);
//            RenderToGBuffer(ActiveCamera);
//
//            GraphicsDevice.SetRenderTargets(null, _lightBufferRT);
//            //dont be fooled by Color.Black, as its alpha is 255 (or 1.0f)
//            GraphicsDevice.ClearTargets(Colors.Transparent, _lightBufferRT);
//
//            //dont use depth/stencil test...we dont have a depth buffer, anyway
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.None;
//            //GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullFrontClipDisabled;
//            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
//
//            //draw using additive blending. 
//            //At first I was using BlendState.additive, but it seems to use alpha channel for modulation, 
//            //and as we use alpha channel as the specular intensity, we have to create our own blend state here
//            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.LightMap;
//
//            RenderLights(ActiveCamera);
//
//            GraphicsDevice.SetRenderTargets(_outputDepthStencilBuffer, _outputBufferRT);
//            GraphicsDevice.ClearTargets(Colors.Black, _outputDepthStencilBuffer, _outputBufferRT);
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Opaque;
//            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
//
//            ReconstructShading(ActiveCamera);
//
//            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Opaque;
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
//
//            DeferredDevice.SetRenderTargets(_outputDepthStencilBuffer, _outputBufferRT);
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipDisabled;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;
//
//            DrawLightIcons();
//            DrawCameraIcons();
//            DrawAdditionalStuff();
//
//            Text = "FPS: " + gameTime.FpsCount + "\n";
        }

        private bool _showDebug = true;

        protected override void Debug()
        {
            //output our final composition into the backbuffer. We could output it into a
            //post-process fx, or to an object diffuse texture, or or or...well, its up to you =)
            //Just remember that we are using a floating point buffer, so we should use Point Clamp here
            //spriteBatch.Begin(SpriteSortMode.NoSort,
            //    GraphicsDevice.BlendStates.Opaque,
            //    GraphicsDevice.SamplersStates.PointWrap,
            //    GraphicsDevice.DepthStencilStates.DepthEnableLessEqual,
            //    GraphicsDevice.RasterizerStates.CullNoneClipDisabled);

//            SpriteBatch.Begin(SpriteSortMode.NoSort,
//                GraphicsDevice.BlendStates.Opaque,
//                GraphicsDevice.SamplersStates.PointClamp,
//                GraphicsDevice.DepthStencilStates.DepthEnableGreater,
//                GraphicsDevice.RasterizerStates.CullNoneClipDisabled);
//
//            int smallWidth = Window.Width / 3;
//            int smallHeigth = Window.Height / 3;
//
//            //draw the intermediate steps into screen
//            SpriteBatch.Draw(_depthBufferRT, new Rectangle(0, 0, smallWidth, smallHeigth), Colors.White);
//            SpriteBatch.Draw(_normalBufferRT, new Rectangle(smallWidth, 0, smallWidth, smallHeigth), Colors.White);
//            SpriteBatch.Draw(_lightBufferRT, new Rectangle(smallWidth * 2, 0, smallWidth, smallHeigth), Colors.White);
//            //spriteBatch.Draw(_testTexture, new RectangleF(0, smallHeigth, 100, 50), Colors.White);
//            SpriteBatch.End();
        }

        private void RenderLights(Camera camera)
        {
//            _lighting.Parameters["GBufferPixelSize"].SetValue(new Vector2F(0.5f / camera.Width, 0.5f / camera.Height));
//            _lighting.Parameters["DepthBuffer"].SetResource(_depthBufferRT);
//            _lighting.Parameters["DepthSampler"].SetResource(GraphicsDevice.SamplersStates.PointClamp);
//            _lighting.Parameters["NormalBuffer"].SetResource(_normalBufferRT);
//            _lighting.Parameters["NormalSampler"].SetResource(GraphicsDevice.SamplersStates.LinearClamp);
//
//            ReconstructZBuffer(camera);
//
//            _lighting.Parameters["TanAspect"].SetValue(new Vector2F(camera.TanFov * camera.AspectRatio, -camera.TanFov));
//
//            //GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
//            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadLess;
//            ApplyFrustumCorners(_lighting, -Vector2F.One, Vector2F.One);
//            _lighting.CurrentTechnique = _lighting.Techniques[0];
//
//            foreach (var light in LightService.DirectionalLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(camera);
//
//                //convert light position into viewspace
//                Vector3F viewSpaceLDir = Vector3F.TransformNormal(Vector3F.Normalize(transform.WorldMatrix.Forward), camera.ViewMatrix);
//                _lighting.Parameters["LightDir"].SetValue(viewSpaceLDir);
//                Vector3F lightColor = light.Color * light.Intensity;
//                _lighting.Parameters["LightColor"].SetValue(lightColor);
//                _lighting.Parameters["FarClip"].SetValue(camera.ZFar);
//
//                _lighting.CurrentTechnique.Passes[0].Apply();
//                GraphicsDevice.Quad.DrawRaw();
//            }
//
//            _lighting.CurrentTechnique = _lighting.Techniques[1];
//            foreach (var light in LightService.PointLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(camera);
//                //light.Range = Vector3F.Max(light.Owner.Transform.Scale);
//                //convert light position into viewspace
//                //Vector3F viewSpaceLPos = transform.RelativePosition;
//                Vector3F viewSpaceLPos = Vector3F.TransformCoordinate(transform.RelativePosition, camera.ViewMatrix);
//
//                //if (viewSpaceLPos.Z - 2 * light.Radius < -camera.ZFar)
//                //{
//                //    GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullBackClipDisabled;
//                //    GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadGreaterEqual;
//                //}
//                //else
//                //{
//                //    GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullFrontClipDisabled;
//                //    GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadLessEqual;
//                //}
//                
//                
//
//                //float cameraToCenter = transform.RelativePosition.Length();
//
//                //if (cameraToCenter <= light.Radius)
//                //{
//                //    GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.CullFrontClipEnabled);
//                //}
//                //else
//                //{
//                //    GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.CullBackClipEnabled);
//                //}
//
//                //_lighting.Parameters["WorldViewProjection"].SetValue(transform.WorldMatrix * camera.ViewProjectionMatrix);
//                _lighting.Parameters["WorldViewProjection"].SetValue(transform.WorldMatrix * camera.ViewMatrix * camera.ProjectionMatrix);
//                _lighting.Parameters["LightPosition"].SetValue(viewSpaceLPos);
//                Vector3F lightColor = light.Color * light.Intensity;
//                _lighting.Parameters["LightColor"].SetValue(lightColor);
//                _lighting.Parameters["FarClip"].SetValue(camera.ZFar);
//                float invRadiusSqr = 1.0f / (light.Range * light.Range);
//                _lighting.Parameters["InvLightRadiusSqr"].SetValue(invRadiusSqr);
//                //_lighting.Parameters["InvLightRadiusSqr"].SetValue(light.Radius);
//
//                _lighting.CurrentTechnique.Passes[0].Apply();
//                LightService.DrawPointLightMesh(GraphicsDevice, GameTime);
//            }
//
//            _lighting.CurrentTechnique = _lighting.Techniques[2];
//            foreach (var light in LightService.SpotLights)
//            {
//                if (!light.Owner.IsEnabled)
//                {
//                    continue;
//                }
//
//                var transform = light.Owner.Transform.GetMetadata(camera);
//                //light.Range = Math.Max(transform.Scale.X, transform.Scale.Z); 
//                var angle = (float)Math.Atan(transform.Scale.Y / transform.Scale.X);
//                light.OuterSpotAngle = angle * 2;
//                light.InnerAngleDelta = light.OuterSpotAngle - (180 - light.OuterSpotAngle);
//                //convert light position into viewspace
//                Vector3F viewSpaceLPos = Vector3F.TransformCoordinate(transform.RelativePosition, camera.ViewMatrix);
//                Vector3F viewSpaceLDir = Vector3F.TransformNormal(Vector3F.Normalize(transform.WorldMatrix.Down),
//                    camera.ViewMatrix);
//
//                //if (viewSpaceLPos.Z - 2 * light.Radius < -camera.ZFar)
//                //{
//                //    //GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullBackClipDisabled;
//                //    GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadGreaterEqual;
//                //}
//                //else
//                //{
//                //    //GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullFrontClipDisabled;
//                //    GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadLessEqual;
//                //}
//
//                //GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadGreaterEqual;
//
//                float tan = (float)Math.Tan(MathHelper.DegreesToRadians(light.OuterSpotAngle));
//                Matrix4x4F lightMatrix = Matrix4x4F.Scaling(light.Range * tan, light.Range * tan, light.Range);
//                //var world = lightMatrix * Matrix4x4F.Translation(transform.RelativePosition);
//
//                _lighting.Parameters["WorldViewProjection"].SetValue(transform.WorldMatrix * camera.ViewProjectionMatrix);
//                //_lighting.Parameters["WorldViewProjection"].SetValue(world * camera.ViewMatrix * camera.ProjectionMatrix);
//                _lighting.Parameters["LightPosition"].SetValue(viewSpaceLPos);
//                _lighting.Parameters["LightDir"].SetValue(viewSpaceLDir);
//                Vector3F lightColor = light.Color * light.Intensity;
//                _lighting.Parameters["LightColor"].SetValue(lightColor);
//                _lighting.Parameters["FarClip"].SetValue(camera.ZFar);
//                float invRadiusSqr = 1.0f / (light.Range * light.Range);
//                //_lighting.Parameters["InvLightRadiusSqr"].SetValue(invRadiusSqr);
//                float cosSpotAngle = (float)Math.Cos(MathHelper.DegreesToRadians(light.OuterSpotAngle *0.5f));
//                float cosSpotExpAngle = (float)Math.Cos(MathHelper.DegreesToRadians(light.InnerAngleDelta *0.5f));
//                _lighting.Parameters["SpotAngle"].SetValue(cosSpotAngle);
//                _lighting.Parameters["SpotExponent"].SetValue(cosSpotExpAngle);
//                _lighting.Parameters["SpotDirection"].SetValue(Vector3F.Normalize(transform.WorldMatrix.Up));
//                _lighting.Parameters["InvLightRadiusSqr"].SetValue(light.Range);
//
//                _lighting.CurrentTechnique.Passes[0].Apply();
//                LightService.DrawSpotLightMesh(GraphicsDevice, GameTime);
//            }
        }

        private void RenderToGBuffer(Camera camera)
        {
//            foreach (var entity in Entities)
//            {
//                _mainEffect.CurrentTechnique = _mainEffect.Techniques[0];
//                entity.TraverseInDepth(
//                    current =>
//                    {
//                        var meshRenderer = current.GetComponent<MeshRenderer>();
//                        if (meshRenderer == null)
//                        {
//                            return;
//                        }
//
//                        var transform = current.Transform.GetMetadata(camera);
//                        _mainEffect.Parameters["World"].SetValue(transform.WorldMatrix);
//                        _mainEffect.Parameters["View"].SetValue(camera.ViewMatrix);
//                        _mainEffect.Parameters["Projection"].SetValue(camera.ProjectionMatrix);
//                        _mainEffect.Parameters["WorldView"].SetValue(transform.WorldMatrix * camera.ViewMatrix);
//                        _mainEffect.Parameters["WorldViewProjection"].SetValue(transform.WorldMatrix * camera.ViewProjectionMatrix);
//                        _mainEffect.Parameters["FarClip"].SetValue(camera.ZFar);
//
//                        _mainEffect.Parameters["NormalMap"].SetResource(_normalMap);
//                        _mainEffect.Parameters["NormalMapSampler"].SetResource(GraphicsDevice.SamplersStates.LinearWrap);
//                        _mainEffect.CurrentTechnique.Passes[0].Apply();
//                        meshRenderer.Draw(GraphicsDevice, GameTime);
//                    });
//            }
        }

        private void ReconstructShading(Camera camera)
        {
//            foreach (var entity in Entities)
//            {
//                _mainEffect.CurrentTechnique = _mainEffect.Techniques[1];
//                entity.TraverseInDepth(
//                    current =>
//                    {
//                        var meshRenderer = current.GetComponent<MeshRenderer>();
//                        if (meshRenderer == null)
//                        {
//                            return;
//                        }
//
//                        var transform = current.Transform.GetMetadata(camera);
//                        var material = current.GetComponent<Material>();
//
//                        _mainEffect.Parameters["LightBufferPixelSize"].SetValue(new Vector2F(0.5f / _lightBufferRT.Width, 0.5f / _lightBufferRT.Height));
//                        _mainEffect.Parameters["World"].SetValue(transform.WorldMatrix);
//                        _mainEffect.Parameters["WorldView"].SetValue(transform.WorldMatrix * camera.ViewMatrix);
//                        _mainEffect.Parameters["WorldViewProjection"].SetValue(transform.WorldMatrix * camera.ViewProjectionMatrix);
//
//                        if (material?.Texture != null && !material.Texture.IsDisposed)
//                        {
//                            _mainEffect.Parameters["DiffuseMap"].SetResource(material.Texture);
//                        }
//                        else
//                        {
//                            _mainEffect.Parameters["DiffuseMap"].SetResource(_diffuseMap);
//                        }
//                        
//                        _mainEffect.Parameters["DiffuseMapSampler"].SetResource(GraphicsDevice.SamplersStates.LinearWrap);
//
//                        _mainEffect.Parameters["SpecularMap"].SetResource(_specularMap);
//                        _mainEffect.Parameters["SpecularMapSampler"].SetResource(GraphicsDevice.SamplersStates.LinearWrap);
//
//                        _mainEffect.Parameters["EmissiveMap"].SetResource(_emissiveMap);
//                        _mainEffect.Parameters["EmissiveMapSampler"].SetResource(GraphicsDevice.SamplersStates.LinearWrap);
//
//                        _mainEffect.Parameters["LightMap"].SetResource(_lightBufferRT);
//                        _mainEffect.Parameters["LightMapSampler"].SetResource(GraphicsDevice.SamplersStates.PointClamp);
//
//                        _mainEffect.CurrentTechnique.Passes[0].Apply();
//                        meshRenderer.Draw(GraphicsDevice, GameTime);
//                    });
//            }
        }

        private void ReconstructZBuffer(Camera camera)
        {
            //bind effect
//            _reconstructZBuffer.Parameters["GBufferPixelSize"].SetValue(new Vector2F(0.5f / camera.Width, 0.5f / camera.Height));
//            _reconstructZBuffer.Parameters["DepthBuffer"].SetResource(_depthBufferRT);
//            _reconstructZBuffer.Parameters["DepthSampler"].SetResource(GraphicsDevice.SamplersStates.PointClamp);
//            _reconstructZBuffer.Parameters["FarClip"].SetValue(camera.ZFar);
//            //our projection matrix is almost all 0s, we just need these 2 values to restoure our Z-buffer from our linear depth buffer
//            _reconstructZBuffer.Parameters["ProjectionValues"].SetValue(new Vector2F(camera.ProjectionMatrix.M33, camera.ProjectionMatrix.M43));
//            _reconstructZBuffer.CurrentTechnique.Passes[0].Apply();
//
//            //we need to always write to z-buffer
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableAlways;
//
//            //store previous state
//            var oldBlendState = GraphicsDevice.BlendState;
//            //we dont need to write to color channels
//            //GraphicsDevice.BlendState = GraphicsDevice.BlendStates.DoNotWriteToColorChannels;
//            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Opaque;
//            GraphicsDevice.Quad.DrawRaw();
//            //with our z-buffer reconstructed we only need to read it
//            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthReadGreaterEqual;
//            GraphicsDevice.BlendState = oldBlendState;
        }

        private void ComputeFrustumCorners(Camera camera)
        {
//            camera.Frustum.GetCorners(_cornersWorldSpace);
//            Matrix4x4F view = camera.ViewMatrix; //this is the inverse of our camera transform
//            Vector3F.TransformCoordinate(_cornersWorldSpace, ref view, _cornersViewSpace); //put the frustum into view space
//            for (int i = 0; i < 4; i++) //take only the 4 farthest points
//            {
//                _currentFrustumCorners[i] = _cornersViewSpace[i + 4];
//            }
//            Vector3F temp = _currentFrustumCorners[3];
//            _currentFrustumCorners[3] = _currentFrustumCorners[2];
//            _currentFrustumCorners[2] = temp;
        }

        /// <summary>
        /// This method computes the frustum corners applied to a quad that can be smaller than
        /// our screen. This is useful because instead of drawing a full-screen quad for each
        /// point light, we can draw smaller quads that fit the light's bounding sphere in screen-space,
        /// avoiding unecessary pixel shader operations
        /// </summary>
        /// <param name="effect">The effect we want to apply those corners</param>
        /// <param name="topLeftVertex"> The top left vertex, in screen space [-1..1]</param>
        /// <param name="bottomRightVertex">The bottom right vertex, in screen space [-1..1]</param>
//        private void ApplyFrustumCorners(Effect effect, Vector2F topLeftVertex, Vector2F bottomRightVertex)
//        {
//            float dx = _currentFrustumCorners[1].X - _currentFrustumCorners[0].X;
//            float dy = _currentFrustumCorners[0].Y - _currentFrustumCorners[2].Y;
//
//            Vector3F[] localFrustumCorners = new Vector3F[4];
//            localFrustumCorners[0] = _currentFrustumCorners[2];
//            localFrustumCorners[0].X += dx * (topLeftVertex.X * 0.5f + 0.5f);
//            localFrustumCorners[0].Y += dy * (bottomRightVertex.Y * 0.5f + 0.5f);
//
//            localFrustumCorners[1] = _currentFrustumCorners[2];
//            localFrustumCorners[1].X += dx * (bottomRightVertex.X * 0.5f + 0.5f);
//            localFrustumCorners[1].Y += dy * (bottomRightVertex.Y * 0.5f + 0.5f);
//
//            localFrustumCorners[2] = _currentFrustumCorners[2];
//            localFrustumCorners[2].X += dx * (topLeftVertex.X * 0.5f + 0.5f);
//            localFrustumCorners[2].Y += dy * (topLeftVertex.Y * 0.5f + 0.5f);
//
//            localFrustumCorners[3] = _currentFrustumCorners[2];
//            localFrustumCorners[3].X += dx * (bottomRightVertex.X * 0.5f + 0.5f);
//            localFrustumCorners[3].Y += dy * (topLeftVertex.Y * 0.5f + 0.5f);
//
//            effect.Parameters["FrustumCorners"].SetValue(localFrustumCorners);
//        }
    }
}
