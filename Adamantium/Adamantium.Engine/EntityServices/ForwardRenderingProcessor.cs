using System;
using System.Linq;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Rendering;
using Adamantium.Fonts;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;
using Adamantium.Win32;

namespace Adamantium.Engine.EntityServices;

public class ForwardRenderingProcessor : RenderingProcessor
{
    public ForwardRenderingProcessor()
    {
    }

    protected override void LoadContent()
    {
        BasicEffect = new BasicEffect(GraphicsDevice);
        _geometryCache = new MeshGeometryCache(GraphicsDevice);
        base.LoadContent();
    }

    protected override void OnDetached()
    {
        _geometryCache?.Dispose();
        base.OnDetached();
    }

    private BasicEffect BasicEffect { get; set; }
    private MeshGeometryCache _geometryCache;
    
    public override void Draw(AppTime gameTime)
    {
        // Resolve the active camera ONCE per frame, not once per entity in the traversal below - it is invariant for the
        // whole draw and GetActive takes a lock + dictionary lookup.
        ActiveCamera = CameraManager.GetActive(Window);
        if (ActiveCamera == null) return;

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
        if (!entity.Visible)
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
                    var wvp = transform.WorldMatrixF * ActiveCamera.ViewProjectionMatrix;
                    BasicEffect.Wvp.SetValue(wvp);
                    BasicEffect.MeshColor.SetValue(Colors.White.ToVector3());
                    BasicEffect.BasicColoredPass.Apply();
                    if (collider.Geometry == null) collider.GetVisualRepresentation();
                    if (collider.Geometry != null)
                    {
                        _geometryCache.GetOrCreate(collider.Geometry, typeof(MeshVertex)).Draw(GraphicsDevice, RenderState.Default);
                    }
                    BasicEffect.BasicColoredPass.UnApply();
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
            // Skinning is WIP; upload the bones here once the pass is wired (do NOT materialise the array every frame
            // until it is actually consumed).
            //BasicEffect.Parameters["Bones"].SetValue(controller.FinalMatrices.Values.ToArray());
        }

        var meshData = entity.GetComponent<MeshData>();
        if (meshData?.Mesh == null || !meshData.IsEnabled) return;

        var transformation = entity.Transform.GetMetadata(ActiveCamera);
        if (!transformation.Enabled)
        {
            return;
        }

        var material = entity.GetComponent<Material>();

        var meshWvp = transformation.WorldMatrixF * ActiveCamera.ViewProjectionMatrix;
        BasicEffect.Wvp.SetValue(meshWvp);
        BasicEffect.MeshColor.SetValue(Colors.Black.ToVector3());
        BasicEffect.Transparency.SetValue(1f);

        if (material?.Texture != null)
        {
            BasicEffect.SampleType.SetResource(GraphicsDevice.SamplerStates.LinearRepeat);
            BasicEffect.ShaderTexture.SetResource(material.Texture);
        }

        // The render PATH is the mesh data's explicit choice, not a property of the mesh - it selects the shader pass
        // here and (inside DrawMesh) the vertex format the GPU buffers are built for.
        if (meshData.RenderMode == MeshRenderMode.Skinned)
        {
            BasicEffect.Techniques["Basic"].Passes["Skinned"].Apply();
        }
        else
        {
            GraphicsDevice.ClearColor = Colors.CornflowerBlue;
            if (material?.Texture != null)
            {
                BasicEffect.BasicTexturedPass.Apply();
            }
            else
            {
                BasicEffect.BasicColoredPass.Apply();
            }
        }

        _geometryCache.DrawMesh(GraphicsDevice, meshData);
    }

    protected void DrawTools(Camera activeCamera)
    {
        var tools = EntityWorld.EntityManager.GetGroup("Tools");
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
        var tools = EntityWorld.EntityManager.GetGroup("Common");
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
        var tools = EntityWorld.EntityManager.GetGroup("HUD");
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
        var geometries = current.GetComponents<MeshData>();
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
        var geometries = current.GetComponents<MeshData>();
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