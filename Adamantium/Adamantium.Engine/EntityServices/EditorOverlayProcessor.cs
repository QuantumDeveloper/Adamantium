using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Rendering;
using Adamantium.Engine.Tools;
using Adamantium.FX;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Engine.EntityServices;

/// <summary>
/// Draws the editor's processors into one output's frame, after its scene: in the scene, in front of what it drew, and
/// on the screen.
/// </summary>
public class EditorOverlayProcessor : RenderingProcessor
{
    private const int InstanceCapacity = 64;
    private const int OutlineDirections = 8;

    private readonly ToolsService tools;
    private readonly List<Part> parts = [];
    private readonly Matrix4x4F[] instanceWorld = new Matrix4x4F[InstanceCapacity];
    private readonly Vector4F[] instanceColors = new Vector4F[InstanceCapacity];
    private bool[] drawn = [];
    private uint outlineMark;
    private BasicEffect effect;
    private MeshGeometryCache geometry;

    public EditorOverlayProcessor(ToolsService tools)
    {
        this.tools = tools;
    }

    public override int Order => 100;

    /// <summary>The camera the frame is drawn with.</summary>
    public Camera Camera => ActiveCamera;

    protected override void CreateDeviceResources()
    {
        geometry?.Dispose();
        effect = new BasicEffect(GraphicsDevice);
        geometry = new MeshGeometryCache(GraphicsDevice);
        base.CreateDeviceResources();
    }

    protected override void OnDetached()
    {
        geometry?.Dispose();
        base.OnDetached();
    }

    public override void Draw(AppTime gameTime)
    {
        ActiveCamera = Window.Camera;
        if (ActiveCamera == null)
        {
            return;
        }

        outlineMark = 0;
        var processors = tools.Processors;
        for (int i = 0; i < processors.Count; i++)
        {
            if (processors[i].IsEnabled && processors[i] is EditorProcessor editor)
            {
                editor.DrawOverlay(this);
            }
        }
    }

    public override void EndDraw()
    {
    }

    /// <summary>
    /// Draws an entity and what is under it in the scene, in front of everything the scene drew. A ring drawn with
    /// <paramref name="frontHalf"/> keeps only the half that faces the eye, as the rings round a ball do.
    /// </summary>
    public void DrawInScene(Entity root, bool frontHalf = false)
    {
        Collect(root);
        DrawParts(ActiveCamera.ViewProjectionMatrix, true, frontHalf ? Look.FrontHalf : Look.Shaded);
    }

    /// <summary>Draws an entity laid out in the output's pixels.</summary>
    public void DrawOnScreen(Entity root)
    {
        Collect(root);
        DrawParts(ActiveCamera.UiProjection, false, Look.Shaded);
    }

    /// <summary>
    /// Outlines an entity and what is under it: a band <paramref name="pixels"/> wide round their joint silhouette, in
    /// front of everything the scene drew.
    /// </summary>
    public void DrawOutline(Entity root, Vector4F color, float pixels)
    {
        Collect(root);
        if (parts.Count == 0)
        {
            return;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            parts[i] = parts[i] with { Color = color };
        }

        var colorMask = GraphicsDevice.ColorComponentFlags;
        GraphicsDevice.StencilTestEnabled = true;
        GraphicsDevice.StencilReference = ++outlineMark;
        GraphicsDevice.StencilCompareOp = CompareOp.Always;
        GraphicsDevice.StencilPassOp = StencilOp.Replace;
        GraphicsDevice.StencilWriteMask = 0xFF;
        GraphicsDevice.ColorComponentFlags = 0;
        DrawParts(ActiveCamera.ViewProjectionMatrix, true, Look.Mask);

        GraphicsDevice.StencilCompareOp = CompareOp.NotEqual;
        GraphicsDevice.StencilPassOp = StencilOp.Keep;
        GraphicsDevice.StencilWriteMask = 0;
        GraphicsDevice.ColorComponentFlags = colorMask;
        effect.OutlineStep.SetValue(new Vector2F(2 * pixels / ActiveCamera.Width, 2 * pixels / ActiveCamera.Height));
        DrawParts(ActiveCamera.ViewProjectionMatrix, true, Look.Outline);

        GraphicsDevice.StencilTestEnabled = false;
    }

    private void Collect(Entity root)
    {
        parts.Clear();
        root.TraverseInDepth(current =>
        {
            var transformation = current.Transform.GetMetadata(ActiveCamera);
            if (!transformation.Enabled || !current.Visible)
            {
                return;
            }

            var material = current.GetComponent<Material>();
            var color = material == null
                ? Colors.White.ToVector4()
                : transformation.IsSelected || current.IsSelected
                    ? material.HighlightColor
                    : new Vector4F(material.MeshColor, material.Transparency);

            foreach (var data in current.GetComponents<MeshData>())
            {
                if (data?.Mesh == null || !data.IsEnabled)
                {
                    continue;
                }

                var topology = data.Mesh.MeshTopology;
                var flat = topology == PrimitiveType.LineList || topology == PrimitiveType.LineStrip || !data.Mesh.IsNormalsPresent;
                parts.Add(new Part(data, transformation.WorldMatrixF, color, flat));
            }
        }, true);
    }

    private void DrawParts(Matrix4x4F viewProjection, bool inFront, Look look)
    {
        if (parts.Count == 0)
        {
            return;
        }

        var directions = look == Look.Outline ? OutlineDirections : 1;
        var capacity = InstanceCapacity / directions;
        effect.ViewProjection.SetValue(viewProjection);

        if (drawn.Length < parts.Count)
        {
            drawn = new bool[parts.Count];
        }

        Array.Clear(drawn, 0, parts.Count);

        for (var i = 0; i < parts.Count; ++i)
        {
            if (drawn[i])
            {
                continue;
            }

            var head = parts[i];
            var count = 0;

            for (var j = i; j < parts.Count && count < capacity; ++j)
            {
                if (drawn[j] || !ReferenceEquals(parts[j].Data.Mesh, head.Data.Mesh))
                {
                    continue;
                }

                instanceWorld[count] = parts[j].World;
                instanceColors[count] = parts[j].Color;
                drawn[j] = true;
                ++count;
            }

            effect.InstanceWorld.SetValue(instanceWorld);
            effect.InstanceColor.SetValue(instanceColors);
            PassFor(look, head.Flat).Apply();

            var instances = (uint)(count * directions);
            if (inFront)
            {
                var data = head.Data;
                var state = new RenderState(data.IsWireFrame, data.CullMode, false, false, data.TopologyOverride);
                geometry.GetOrCreate(data.Mesh, data.ResolveVertexType()).Draw(GraphicsDevice, state, instances);
            }
            else
            {
                geometry.DrawMesh(GraphicsDevice, head.Data, instances);
            }
        }
    }

    private IEffectPass PassFor(Look look, bool flat)
    {
        return look switch
        {
            Look.Outline => effect.BasicOutlineInstancedPass,
            Look.Mask => effect.BasicFlatInstancedPass,
            Look.FrontHalf when flat => effect.BasicOrbitInstancedPass,
            _ => flat ? effect.BasicFlatInstancedPass : effect.BasicLitInstancedPass
        };
    }

    private enum Look
    {
        Shaded,
        FrontHalf,
        Mask,
        Outline
    }

    private readonly record struct Part(MeshData Data, Matrix4x4F World, Vector4F Color, bool Flat);
}
