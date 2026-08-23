using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UITests.Rendering;

// Shared, GPU-free scaffolding for the RenderCache lifecycle tests. Uses REAL UIComponent-derived controls
// (so RenderId / Visibility / IsGeometryValid / attach-detach all behave exactly like production) plus a
// fake render-unit factory that records created / disposed / deferred units instead of touching the GPU.

/// <summary>Minimal real visual-tree root. Being an <see cref="IRootVisualComponent"/> is what makes its
/// children attach to the tree when added.</summary>
internal sealed class TestRoot : UIComponent, IRootVisualComponent
{
    public TestRoot(double width = 800, double height = 600)
    {
        ClientWidth = width;
        ClientHeight = height;
    }

    /// <summary>The harness draws a frame at a time on demand, exactly like a bake - so content inside it is built where
    /// it is asked for instead of arriving later.</summary>
    public bool RendersOnce => true;

    public void Add(IUIComponent child) => AddVisualChild(child);
    public void Remove(IUIComponent child) => RemoveVisualChild(child);

    /// <summary>Adds a child AT a position - what a virtualizing panel does when content scrolls in at the top, and the case
    /// a dense paint-order rank cannot express without renumbering everything after it.</summary>
    public void Insert(int index, IUIComponent child)
    {
        VisualChildrenCollection.Insert(index, child);
        RenderDirty.MarkStructural(child);
    }

    public double Left { get; set; }
    public double Top { get; set; }
    public string Title { get; set; }
    public double ClientWidth { get; set; }
    public double ClientHeight { get; set; }
    public IUIContext UIContext => null;
    public void AttachContextAndInitialize(IUIContext context) { }
    public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
    public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
}

/// <summary>A real control whose drawing output is fully scripted by <see cref="RenderAction"/>.</summary>
internal sealed class TestControl : UIComponent
{
    /// <summary>Called from OnRender; emit whatever draw commands the test needs. Null = draws nothing.</summary>
    public Action<IDrawingSession> RenderAction;

    /// <summary>The harness draws a frame at a time on demand, exactly like a bake - so content inside it is built where
    /// it is asked for instead of arriving later.</summary>
    public bool RendersOnce => true;

    public void Add(IUIComponent child) => AddVisualChild(child);
    public void Remove(IUIComponent child) => RemoveVisualChild(child);

    /// <summary>Drops every visual child - what a templated control does when it replaces its template.</summary>
    public void ClearChildren() => RemoveVisualChildren();

    /// <summary>Mark dirty so the next Render() actually re-runs OnRender (mirrors a property change).</summary>
    public void Invalidate() => InvalidateRender(false);

    /// <summary>How many times OnRender actually ran - so a test can prove the partial rebuild re-renders ONLY the
    /// dirty control, not its unchanged siblings.</summary>
    public int OnRenderCount { get; private set; }

    protected override void OnRender(IDrawingContext context)
    {
        OnRenderCount++;
        RenderAction?.Invoke(context.ForControl(this));
    }
}

/// <summary>Fake render unit: records lifecycle calls, never allocates GPU resources.</summary>
internal sealed class FakeRenderUnit : IRenderUnit
{
    private IDrawCommand _command;

    public FakeRenderUnit(IDrawCommand command) => _command = command;

    public IUIComponent Component => _command.Component;
    public RenderData RenderData => _command.RenderData;
    public Type PayloadType => _command.Payload.GetType();

    public float EffectiveOpacity { get; private set; } = 1f;
    public void SetEffectiveOpacity(float opacity) => EffectiveOpacity = opacity;

    public int FadeSlot { get; private set; } = -1;

    public void SetFadeSlot(int slot) => FadeSlot = slot;

    public int UpdateCount { get; private set; }
    public int PreRenderCount { get; private set; }
    public int RenderCount { get; private set; }
    public int UpdateWithCommandCount { get; private set; }
    public int DisposeCount { get; private set; }
    public int DeferDisposeCount { get; private set; }

    public void Update(Matrix4x4F transform, Matrix4x4F projection, double renderScale) => UpdateCount++;
    public bool NeedsPreRender => false;

    public void PreRender() => PreRenderCount++;
    public void Render() => RenderCount++;

    public void UpdateWithDrawCommand(IDrawCommand command)
    {
        _command = command;
        UpdateWithCommandCount++;
    }

    // Mirror the production Match: same payload type -> reuse + update, otherwise replace.
    public bool Match(IDrawCommand drawCommand) => _command.Payload.GetType() == drawCommand.Payload.GetType();

    public void Dispose() => DisposeCount++;
    public void DeferDispose() => DeferDisposeCount++;
}

internal sealed class FakeRenderUnitFactory : IRenderUnitFactory
{
    public List<FakeRenderUnit> Created { get; } = new();

    // GPU-free: this factory builds no device resources, so there is no device (and nothing to warm a font atlas on).
    public Adamantium.Graphics.Core.IGraphicsDevice GraphicsDevice => null;

    public void RegisterFactory<T>(Func<IDrawCommand, IRenderUnit> factory) { }

    public IRenderUnit CreateRenderUnitFromCommand(IDrawCommand command)
    {
        var unit = new FakeRenderUnit(command);
        Created.Add(unit);
        return unit;
    }
}
