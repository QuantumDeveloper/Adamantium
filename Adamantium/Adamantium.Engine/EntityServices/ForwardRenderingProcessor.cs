using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Rendering;
using Adamantium.Fonts;
using Adamantium.FX;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;
using Serilog;

namespace Adamantium.Engine.EntityServices;

public class ForwardRenderingProcessor : RenderingProcessor
{
    public ForwardRenderingProcessor()
    {
    }

    protected override void CreateDeviceResources()
    {
        _geometryCache?.Dispose();
        BasicEffect = new BasicEffect(GraphicsDevice);
        _geometryCache = new MeshGeometryCache(GraphicsDevice);
        base.CreateDeviceResources();
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
        ActiveCamera = Window.Camera;
        if (ActiveCamera == null) return;

        foreach (var entity in Entities)
        {
            OnDraw(entity, gameTime);
        }

        // The orientation gizmo, over the scene in screen space.
        DrawHUD();
    }
    
    private void OnDraw(Entity entity, AppTime gameTime)
    {
        try
        {
            entity.TraverseInDepth(current => DrawEntity(current, gameTime));
        }
        catch (Exception exception)
        {
            Log.Logger.Error(exception, "Draw failed for entity {Entity}", entity.Name);
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
            // Skinning is WIP: the bones are uploaded here once the pass consumes them.
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

        // The mesh data's choice, not the mesh's: it picks the pass here and the vertex format inside DrawMesh.
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
                Log.Logger.Error(exception, "Draw failed for tool {Tool}", tool.Name);
            }
        }
//            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
    }


    protected void DrawLights(Camera activeCamera)
    {
//            LightService.DrawDebugLight(ToolsService.SelectedEntity, BasicEffect, CameraService, ActiveCamera, DeferredDevice, GameTime);
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
                Log.Logger.Error(exception, "Draw failed for tool {Tool}", tool.Name);
            }
        }
//            BasicEffect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
    }

    protected void DrawHUD()
    {
        var tools = EntityWorld.EntityManager.GetGroup("HUD");

        foreach (var tool in tools)
        {
            try
            {
                _hudParts.Clear();
                tool.TraverseInDepth(CollectHUD);
                DrawHudParts();
            }
            catch (Exception exception)
            {
                Log.Logger.Error(exception, "HUD draw failed for tool {Tool}", tool.Name);
            }
        }
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

    private readonly record struct HudPart(MeshData Data, Matrix4x4F World, Vector4F Color);

    private readonly List<HudPart> _hudParts = [];
    private readonly Matrix4x4F[] _instanceWorld = new Matrix4x4F[InstanceCapacity];
    private readonly Vector4F[] _instanceColors = new Vector4F[InstanceCapacity];

    // Matches the table length declared in BasicEffect.fx.
    private const int InstanceCapacity = 64;

    private void CollectHUD(Entity current)
    {
        var transformation = current.Transform.GetMetadata(ActiveCamera);
        if (!transformation.Enabled || !current.Visible)
        {
            return;
        }

        var material = current.GetComponent<Material>();

        var color = material == null
            ? Colors.White.ToVector4()
            : transformation.IsSelected
                ? material.HighlightColor
                : new Vector4F(material.MeshColor, material.Transparency);

        foreach (var component in current.GetComponents<MeshData>())
        {
            if (component?.Mesh == null || !component.IsEnabled) continue;

            _hudParts.Add(new HudPart(component, transformation.WorldMatrixF, color));
        }
    }

    // Parts sharing a mesh go out as one instanced draw; only placement and color differ per copy.
    private void DrawHudParts()
    {
        if (_hudParts.Count == 0) return;

        // UI projection, not the scene's: a tool places itself in screen pixels.
        BasicEffect.ViewProjection.SetValue(ActiveCamera.UiProjection);

        var drawn = new bool[_hudParts.Count];

        for (var i = 0; i < _hudParts.Count; ++i)
        {
            if (drawn[i]) continue;

            var head = _hudParts[i];
            var count = 0;

            for (var j = i; j < _hudParts.Count && count < InstanceCapacity; ++j)
            {
                if (drawn[j] || !ReferenceEquals(_hudParts[j].Data.Mesh, head.Data.Mesh)) continue;

                _instanceWorld[count] = _hudParts[j].World;
                _instanceColors[count] = _hudParts[j].Color;
                drawn[j] = true;
                ++count;
            }

            BasicEffect.InstanceWorld.SetValue(_instanceWorld);
            BasicEffect.InstanceColor.SetValue(_instanceColors);
            BasicEffect.BasicLitInstancedPass.Apply();

            _geometryCache.DrawMesh(GraphicsDevice, head.Data, (uint)count);
        }
    }

    protected void DrawAdditionalStuff()
    {
        DrawCommonTools(ActiveCamera);

//            GraphicsDevice.ClearTargets(Colors.Gray, ClearOptions.DepthBuffer);
//            DeferredDevice.RasterizerState = DeferredDevice.RasterizerStates.CullNoneClipEnabled;
//            DeferredDevice.BlendState = DeferredDevice.BlendStates.NonPremultiplied;
//            DeferredDevice.DepthStencilState = DeferredDevice.DepthStencilStates.DepthEnableGreater;

        DrawTools(ActiveCamera);

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