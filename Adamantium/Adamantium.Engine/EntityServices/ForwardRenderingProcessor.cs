using System;
using System.Linq;
using Adamantium.Core;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;
using Adamantium.Win32;

namespace Adamantium.Engine.EntityServices;

public class ForwardRenderingProcessor : RenderingProcessor
{
    public ForwardRenderingProcessor()
    {
    }

    public override void Draw(AppTime gameTime)
    {
        foreach (var entity in Entities)
        {
            OnDraw(entity, gameTime);
        }
    }

    private void OnDraw(Entity entity, AppTime gameTime)
    {
        try
        {
            entity.TraverseInDepth(current => DrawEntity(current, gameTime));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message + exception.StackTrace);
        }
    }

    private void DrawEntity(Entity entity, AppTime gameTime)
    {
        ActiveCamera = CameraManager.GetActive(Window);
        if (!entity.Visible || ActiveCamera == null)
        {
            return;
        }
        
        var collider = entity.GetComponent<Collider>();
        if (collider != null)
        {
            if (collider.ContainsDataFor(ActiveCamera))
            {
                var intersects = collider.IsInsideCameraFrustum(ActiveCamera);

                if (intersects == ContainmentType.Disjoint) return;

                if (collider.DisplayCollider)
                {
                    var transform = entity.Transform.GetMetadata(ActiveCamera);
                    var wvp = transform.WorldMatrixF * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                    GraphicsDevice.BasicEffect.Wvp.SetValue(wvp);
                    GraphicsDevice.BasicEffect.MeshColor.SetValue(Colors.White.ToVector3());
                    GraphicsDevice.BasicEffect.BasicColoredPass.Apply();
                    collider.Draw(GraphicsDevice, ActiveCamera);
                    GraphicsDevice.BasicEffect.BasicColoredPass.UnApply();
                }
            }
        }
        else
        {
            return;
        }

        var controller = entity.GetComponent<AnimationController>();
        if (controller != null && controller.FinalMatrices.Count > 0)
        {
            var arr = controller.FinalMatrices.Values.ToArray();
            //BasicEffect.Parameters["Bones"].SetValue(arr);
        }

        var renderers = entity.GetComponents<MeshRendererBase>();

        var transformation = entity.Transform.GetMetadata(ActiveCamera);
        if (!transformation.Enabled)
        {
            return;
        }

        if (renderers.Length <= 0) return;

        foreach (var component in renderers)
        {
            var material = entity.GetComponent<Material>();
            var wvp = transformation.WorldMatrixF * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
            //var orthoProj = Matrix4x4F.OrthoOffCenter(0, Window.Width, 0, Window.Height, 1f, 100000f);
            //var wvp = transformation.WorldMatrixF * ActiveCamera.UiProjection;
            //var wvp = transformation.WorldMatrix * Matrix4x4F.Scaling(1, -1, 1) * Matrix4x4F.Scaling(2.0f / Window.Width, 2.0f/Window.Height, 1.0f / (100000f - 1f));
            GraphicsDevice.BasicEffect.Wvp.SetValue(wvp);
            GraphicsDevice.BasicEffect.MeshColor.SetValue(Colors.Black.ToVector3());
            GraphicsDevice.BasicEffect.Transparency.SetValue(1f);
            
            //GraphicsDevice.BasicEffect.Parameters["worldMatrix"].SetValue(transformation.WorldMatrix);
            //GraphicsDevice.BasicEffect.SetValue(Matrix4x4F.Transpose(Matrix4x4F.Invert(transformation.WorldMatrix)));

            //DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.Default;
            //DeferredDevice.SetRasterizerState(DeferredDevice.RasterizerStates.CullBackClipDisabled);

            if (material?.Texture != null)
            {
                GraphicsDevice.BasicEffect.SampleType.SetResource(GraphicsDevice.SamplerStates.LinearRepeat);
                GraphicsDevice.BasicEffect.ShaderTexture.SetResource(material.Texture);
            }
            /*else
                {
                    GraphicsDevice.BasicEffect.Parameters["shaderTexture"].SetResource(defaultTexture);
                }*/

            if (component is SkinnedMeshRenderer)
            {
                GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["Skinned"].Apply();
                component.Draw(GraphicsDevice, gameTime);
            }

            if (component is MeshRenderer)
            {
                GraphicsDevice.ClearColor = Colors.CornflowerBlue;
                // transformation.WorldMatrixF = Matrix4x4F.Translation(entity.Transform.Position);
                // wvp = transformation.WorldMatrixF * ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix;
                // GraphicsDevice.BasicEffect.Wvp.SetValue(wvp);

                if (material?.Texture != null)
                {
                    GraphicsDevice.BasicEffect.BasicTexturedPass.Apply();
                }
                else
                {
                    GraphicsDevice.BasicEffect.BasicColoredPass.Apply();
                }
                
                component.Draw(GraphicsDevice, gameTime);
            }
        }
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
            Matrix4x4F inverseViewProjection =
                Matrix4x4F.Invert(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
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
            //Debug();
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
}