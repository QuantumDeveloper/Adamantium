using System;
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
}
