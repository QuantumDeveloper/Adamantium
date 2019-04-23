using System;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;
using Adamantium.Win32;
using SharpDX.Direct3D11;
using Keys = Adamantium.Engine.GameInput.Keys;

namespace Adamantium.EntityFramework.Processors
{
    public class RenderProcessor : EntityProcessor
    {
        protected CommandList CommandList;
        protected D3DGraphicsDevice DeferredDevice;
        protected D2DGraphicDevice D2dDevice;
        protected SpriteBatch SpriteBatch;
        public String Text;

        protected IGraphicsDeviceManager GraphicsDeviceManager;
        protected IGraphicsDeviceService GraphicsDeviceService;
        protected D3DGraphicsDevice GraphicsDevice => GraphicsDeviceService.GraphicsDevice;
        protected IContentManager Content { get; }
        protected GameWindow Window { get; }

        protected LightService LightService { get; }
        protected InputService InputService { get; }
        protected CameraService CameraService { get; }
        protected ToolsService ToolsService { get; }

        protected Effect BasicEffect { get; set; }

        protected Camera ActiveCamera { get; set; }
        protected bool ShowDebugOutput { get; set; }

        public RenderProcessor(EntityWorld world, GameWindow window) : base(world)
        {
            GraphicsDeviceManager = world.Services.Get<IGraphicsDeviceManager>();
            GraphicsDeviceService = world.Services.Get<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeBegin += DeviceChangeBegin;
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
            Content = world.Services.Get<IContentManager>();
            Window = window;
            Window.ParametersChanging += Window_ParametersChanging;
            Window.ParametersChanged += Window_ParametersChanged;
            LightService = world.Services.Get<LightService>();
            InputService = world.Services.Get<InputService>();
            CameraService = EntityWorld.Services.Get<CameraService>();
            ToolsService = EntityWorld.Services.Get<ToolsService>();
            BasicEffect = Effect.Load(@"Content\Effects\BasicEffect.fx.compiled", GraphicsDevice);
            DeferredDevice = GraphicsDevice;
            SpriteBatch = new SpriteBatch(DeferredDevice, 25000);
        }

        private void Window_ParametersChanged(object sender, GameWindowParametersEventArgs e)
        {
            OnWindowParametersChanged(e.Reason);
        }

        private void Window_ParametersChanging(object sender, GameWindowParametersEventArgs e)
        {
            OnWindowParametersChanging(e.Reason);
        }


        protected virtual void OnWindowParametersChanging(ChangeReason reason)
        { }

        protected virtual void OnWindowParametersChanged(ChangeReason reason)
        { }

        private void DeviceChangeBegin(object sender, EventArgs e)
        {
            OnDeviceChangeBegin();
        }

        private void DeviceChangeEnd(object sender, EventArgs e)
        {
            OnDeviceChangeEnd();
        }

        protected virtual void OnDeviceChangeBegin()
        {
            SpriteBatch?.Dispose();
        }

        protected virtual void OnDeviceChangeEnd()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice, 25000);
        }

        public virtual void CreateSystemResources()
        { }

        public override bool BeginDraw()
        {
            GraphicsDevice.SetViewport(Window.Viewport);
            return base.BeginDraw();
        }

        public override void Draw(IGameTime gameTime)
        {
            base.Draw(gameTime);
            GameTime = gameTime;

            if (InputService.IsKeyPressed(Keys.P))
            {
                ShowDebugOutput = !ShowDebugOutput;
            }

            ActiveCamera = CameraService.GetActive(Window);
        }

        protected void DrawTools(Camera activeCamera)
        {
            var tools = EntityWorld.GetGroup("Tools");
            foreach (var tool in tools)
            {
                try
                {
                    tool.TraverseInDepth(ProcessTool);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }
            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
        }


        protected void DrawLights(Camera activeCamera)
        {
            if (CameraService.UserControlledCamera == ActiveCamera)
            {
                LightService.DrawDebugLight(ToolsService.SelectedEntity, BasicEffect, CameraService, ActiveCamera, DeferredDevice, GameTime);
            }
        }

        protected void DrawCommonTools(Camera activeCamera)
        {
            var tools = EntityWorld.GetGroup("Common");
            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipEnabled;
            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;

            foreach (var tool in tools)
            {
                try
                {
                    tool.TraverseInDepth(ProcessTool);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }
            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
        }

        protected void DrawHUD()
        {
            var tools = EntityWorld.GetGroup("HUD");
            DeferredDevice.ClearTargets(Colors.Gray, ClearOptions.DepthBuffer);
            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipDisabled;
            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;

            foreach (var tool in tools)
            {
                try
                {
                    tool.TraverseInDepth(ProcessHUD);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message + exception.StackTrace);
                }
            }
            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
        }


        private void ProcessTool(Entity current)
        {
            var transformation = current.Transform.GetMetadata(ActiveCamera);
            if (!transformation.Enabled || !current.Visible)
            {
                return;
            }
            var material = current.GetComponent<Material>();
            var geometries = current.GetComponents<MeshRendererBase>();
            foreach (var component in geometries)
            {
                var world = transformation.WorldMatrix;
                var wvp = world * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                Matrix4x4F inverseViewProjection = Matrix4x4F.Invert(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
                BasicEffect.Parameters["wvp"].SetValue(wvp);
                BasicEffect.Parameters["sphereCenter"].SetValue(transformation.RelativePosition); 
                BasicEffect.Parameters["InverseViewProjection"].SetValue(inverseViewProjection);
                BasicEffect.Parameters["ViewDir"].SetValue(ActiveCamera.Backward);

                if (material != null)
                {
                    if (current.IsSelected)
                    {
                        BasicEffect.Parameters["meshColor"].SetValue(material.HighlightColor);
                    }
                    else
                    {
                        BasicEffect.Parameters["meshColor"].SetValue(material.MeshColor);
                    }

                    BasicEffect.Parameters["transparency"].SetValue(material.Transparency);
                }

                if (!current.Name.Contains("Orbit"))
                {
                    BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
                }
                else
                {
                    BasicEffect.Techniques["MeshVertex"].Passes["RotationOrbits"].Apply();
                }

                component.Draw(DeferredDevice, GameTime);
            }
        }

        private void ProcessHUD(Entity current)
        {
            var transformation = current.Transform.GetMetadata(ActiveCamera);
            if (!transformation.Enabled || !current.Visible)
            {
                return;
            }

            var material = current.GetComponent<Material>();
            var geometries = current.GetComponents<MeshRendererBase>();
            foreach (var component in geometries)
            {
                var world = transformation.WorldMatrix;
                var wvp = world * ActiveCamera.UiProjection;
                BasicEffect.Parameters["wvp"].SetValue(wvp);
                
                if (material != null)
                {
                    if (transformation.IsSelected)
                    {
                        BasicEffect.Parameters["meshColor"].SetValue(material.HighlightColor);
                    }
                    else
                    {
                        BasicEffect.Parameters["meshColor"].SetValue(material.MeshColor);
                    }

                    BasicEffect.Parameters["transparency"].SetValue(material.Transparency);
                }

                BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();

                component.Draw(DeferredDevice, GameTime);
            }
        }

        protected virtual void Debug() { }

        protected void DrawAdditionalStuff()
        {
            DrawCommonTools(ActiveCamera);

            GraphicsDevice.ClearTargets(Colors.Gray, ClearOptions.DepthBuffer);
            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipEnabled;
            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreater;

            if (ActiveCamera == CameraService.UserControlledCamera)
            {
                DrawTools(ActiveCamera);
            }

            DrawLights(ActiveCamera);

            if (ShowDebugOutput)
            {
                Debug();
            }

            DrawHUD();
        }

        protected void DrawLightIcons()
        {
            LightService.DrawIcons(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
        }

        protected void DrawCameraIcons()
        {
            CameraService.DrawCameraIcons(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
            if (CameraService.Contains(ToolsService.SelectedEntity))
            {
                CameraService.SetSelected(ToolsService.SelectedEntity);
                CameraService.DrawDebugCamera(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
            }
        }

        public override void EndDraw()
        {
            base.EndDraw();

            if (GraphicsDevice.IsD2dSupportEnabled)
            {
                D2dDevice.BeginDraw();
                D2dDevice.DrawText(Text);
                D2dDevice.EndDraw();
            }

            //if (Final.Description.Width == Window.Width &&
            //    Final.Description.Height == Window.Height)
            //{
            //    DeferredDevice.MainDevice.CopyResource(Final, Window.BackBuffer);
            //}
        }
    }
}
