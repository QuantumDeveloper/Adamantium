﻿using System;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Mathematics;
using Adamantium.Win32;
using Keys = Adamantium.Game.Core.Input.Keys;

namespace Adamantium.Engine.EntityServices
{
    public class RenderingService : EntityService
    {
        
        // protected SpriteBatch SpriteBatch;
        public String Text;
        
        protected IGraphicsDeviceService GraphicsDeviceService;
        protected GraphicsDevice GraphicsDevice => Window.GraphicsDevice;
        protected IContentManager Content { get; }
        protected GameOutput Window { get; }

        protected LightManager LightManager { get; }
        protected GameInputManager InputManager { get; }
        protected CameraManager CameraManager { get; }
        protected ToolsManager ToolsManager { get; }

//        protected Effect BasicEffect { get; set; }

        protected Camera ActiveCamera { get; set; }
        protected bool ShowDebugOutput { get; set; }

        public RenderingService(EntityWorld world, GameOutput window) : base(world)
        {
            GraphicsDeviceService = world.DependencyResolver.Resolve<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeBegin += DeviceChangeBegin;
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
            Content = world.DependencyResolver.Resolve<IContentManager>();
            Window = window;
            Window.ParametersChanging += Window_ParametersChanging;
            Window.ParametersChanged += Window_ParametersChanged;
            Window.StateChanged += StateChanged;
            LightManager = world.DependencyResolver.Resolve<LightManager>();
            InputManager = world.DependencyResolver.Resolve<GameInputManager>();
            CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
            ToolsManager = EntityWorld.DependencyResolver.Resolve<ToolsManager>();

//            BasicEffect = Effect.Load(@"Content\Effects\BasicEffect.fx.compiled", GraphicsDevice);
//            SpriteBatch = new SpriteBatch(DeferredDevice, 25000);
        }

        private void StateChanged(WindowStatePayload obj)
        {
            
        }

        private void Window_ParametersChanged(GameOutputParametersPayload payload)
        {
            OnWindowParametersChanged(payload.Reason);
        }

        private void Window_ParametersChanging(GameOutputParametersPayload payload)
        {
            OnWindowParametersChanging(payload.Reason);
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
//            SpriteBatch?.Dispose();
        }

        protected virtual void OnDeviceChangeEnd()
        {
//            SpriteBatch = new SpriteBatch(GraphicsDevice, 25000);
        }

        public virtual void CreateSystemResources()
        { }

        public override bool BeginDraw()
        {
            if (!Window.IsUpToDate()) return false;

            if (base.BeginDraw())
            {
                if (GraphicsDevice.BeginDraw(1.0f, 0))
                {
                    GraphicsDevice.SetViewports(Window.Viewport);
                    GraphicsDevice.SetScissors(Window.Scissor);
                    return true;
                }

                return false;
            }

            return false;
        }

        private AutoResetEvent pauseEvent = new AutoResetEvent(false);
        public override void Draw(AppTime gameTime)
        {
            if (!Window.IsVisible)
            {
                pauseEvent.WaitOne();
            }
            
            base.Draw(gameTime);
            AppTime = gameTime;

            if (InputManager.IsKeyPressed(Keys.P))
            {
                ShowDebugOutput = !ShowDebugOutput;
            }

            ActiveCamera = CameraManager.GetActive(Window);
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
//            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
        }


        protected void DrawLights(Camera activeCamera)
        {
            if (CameraManager.UserControlledCamera == ActiveCamera)
            {
//                LightService.DrawDebugLight(ToolsService.SelectedEntity, BasicEffect, CameraService, ActiveCamera, DeferredDevice, GameTime);
            }
        }

        protected void DrawCommonTools(Camera activeCamera)
        {
            var tools = EntityWorld.GetGroup("Common");
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipEnabled;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;

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
//            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
        }

        protected void DrawHUD()
        {
            var tools = EntityWorld.GetGroup("HUD");
//            DeferredDevice.ClearTargets(Colors.Gray, ClearOptions.DepthBuffer);
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullBackClipDisabled;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.Opaque;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreaterEqual;

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
//            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
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
                var world = transformation.WorldMatrixF;
                var wvp = world * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                Matrix4x4F inverseViewProjection = Matrix4x4F.Invert(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
//                BasicEffect.Parameters["wvp"].SetValue(wvp);
//                BasicEffect.Parameters["sphereCenter"].SetValue(transformation.RelativePosition); 
//                BasicEffect.Parameters["InverseViewProjection"].SetValue(inverseViewProjection);
//                BasicEffect.Parameters["ViewDir"].SetValue(ActiveCamera.Backward);

                if (material != null)
                {
                    if (current.IsSelected)
                    {
//                        BasicEffect.Parameters["meshColor"].SetValue(material.HighlightColor);
                    }
                    else
                    {
//                        BasicEffect.Parameters["meshColor"].SetValue(material.MeshColor);
                    }

//                    BasicEffect.Parameters["transparency"].SetValue(material.Transparency);
                }

                if (!current.Name.Contains("Orbit"))
                {
//                    BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
                }
                else
                {
//                    BasicEffect.Techniques["MeshVertex"].Passes["RotationOrbits"].Apply();
                }

//                component.Draw(DeferredDevice, GameTime);
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
                var world = transformation.WorldMatrixF;
                var wvp = world * ActiveCamera.UiProjection;
//                BasicEffect.Parameters["wvp"].SetValue(wvp);
                
//                if (material != null)
//                {
//                    if (transformation.IsSelected)
//                    {
//                        BasicEffect.Parameters["meshColor"].SetValue(material.HighlightColor);
//                    }
//                    else
//                    {
//                        BasicEffect.Parameters["meshColor"].SetValue(material.MeshColor);
//                    }
//
//                    BasicEffect.Parameters["transparency"].SetValue(material.Transparency);
//                }
//
//                BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
//
//                component.Draw(DeferredDevice, GameTime);
            }
        }

        protected virtual void Debug() { }

        protected void DrawAdditionalStuff()
        {
            DrawCommonTools(ActiveCamera);

//            GraphicsDevice.ClearTargets(Colors.Gray, ClearOptions.DepthBuffer);
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipEnabled;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreater;

            if (ActiveCamera == CameraManager.UserControlledCamera)
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
//            LightService.DrawIcons(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
        }

        protected void DrawCameraIcons()
        {
//            CameraService.DrawCameraIcons(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
//            if (CameraService.Contains(ToolsService.SelectedEntity))
//            {
//                CameraService.SetSelected(ToolsService.SelectedEntity);
//                CameraService.DrawDebugCamera(BasicEffect, ActiveCamera, DeferredDevice, GameTime);
//            }
        }

        public override void EndDraw()
        {
            GraphicsDevice.EndDraw();
        }
    }
}
