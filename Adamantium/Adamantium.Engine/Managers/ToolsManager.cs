using System;
using System.Collections.Generic;
using Adamantium.Engine.Templates.Tools;
using Adamantium.Engine.Tools;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Managers;

public class ToolsManager
{
    private CollisionResult result = new CollisionResult();
    private float limitDistance = 0.06f;
    private ToolBase currentTool = null;
    private readonly LightManager lightManager;
    private bool _lightProcessingResult;
    private bool _isDraggingEnabled;
    private bool _isMoveToolEnabled;
    private bool _isRotationToolEnabled;
    private bool _isPivotToolEnabled;
    private bool _isScaleToolEnabled;
    private bool _localTransformEnabled;

    public CameraDragTool CameraDragTool { get; private set; }

    public RotationTool RotationTool { get; private set; }

    public MoveTool MoveTool { get; private set; }

    public ScaleTool ScaleTool { get; private set; }

    public PivotTool PivotTool { get; private set; }

    public OrientationTool OrientationTool { get; private set; }

    public Entity SelectedEntity { get; set; }

    public Entity PlaneGridTool { get; set; }

    public String Text { get; private set; }

    /// <summary>
    /// Tools that pick, move, rotate and scale entities; lights are neither picked nor edited.
    /// </summary>
    public ToolsManager(EntityWorld entityWorld) : this(entityWorld, null)
    {
    }

    /// <summary>
    /// Tools that also pick lights and edit them through <paramref name="lightManager"/>.
    /// </summary>
    public ToolsManager(EntityWorld entityWorld, LightManager lightManager)
    {
        this.lightManager = lightManager;
        CameraDragTool = new CameraDragTool(nameof(CameraDragTool));
        MoveTool = new MoveTool(false, 1.0f, new Vector3F(2));
        RotationTool = new RotationTool(false, 2.0f, new Vector3F(2));
        ScaleTool = new ScaleTool(false, 1.0f, new Vector3F(2));
        PivotTool = new PivotTool(false, 1.0f, new Vector3F(2));
        OrientationTool = new OrientationTool(100, new Vector3F(1), QuaternionF.RotationAxis(Vector3F.Right, MathHelper.DegreesToRadians(180)));
        currentTool = MoveTool;

        PlaneGridTool = new PlaneGridToolTemplate(20, 20, new Vector3F(1), 20).BuildEntity(null, "PlaneGrid");

        entityWorld.EntityManager.AddToGroup(MoveTool.Tool, "Tools");
        entityWorld.EntityManager.AddToGroup(PivotTool.Tool, "Tools");
        entityWorld.EntityManager.AddToGroup(RotationTool.Tool, "Tools");
        entityWorld.EntityManager.AddToGroup(ScaleTool.Tool, "Tools");
        entityWorld.EntityManager.AddToGroup(OrientationTool.Tool, "HUD");
        entityWorld.EntityManager.AddToGroup(PlaneGridTool, "Common");
    }


    public Boolean IsDraggingEnabled
    {
        get => _isDraggingEnabled;
        set
        {
            _isDraggingEnabled = value;
            CameraDragTool.Enabled = value;
            if (value)
            {
                currentTool = CameraDragTool;
            }
        }
    }

    public Boolean IsMoveToolEnabled
    {
        get => _isMoveToolEnabled;
        set
        {
            _isMoveToolEnabled = value;
            MoveTool.Enabled = value;
            if (value)
            {
                currentTool = MoveTool;
            }
        }
    }

    public Boolean IsRotationToolEnabled
    {
        get => _isRotationToolEnabled;
        set
        {
            _isRotationToolEnabled = value;
            RotationTool.Enabled = value;
            if (value)
            {
                currentTool = RotationTool;
            }
        }
    }

    public Boolean IsPivotToolEnabled
    {
        get => _isPivotToolEnabled;
        set
        {
            _isPivotToolEnabled = value;
            PivotTool.Enabled = value;
            if (value)
            {
                currentTool = PivotTool;
            }
        }
    }

    public Boolean IsScaleToolEnabled
    {
        get => _isScaleToolEnabled;
        set
        {
            _isScaleToolEnabled = value;
            ScaleTool.Enabled = value;
            if (value)
            {
                currentTool = ScaleTool;
            }
        }
    }

    public Boolean LocalTransformEnabled
    {
        get => _localTransformEnabled;
        set => _localTransformEnabled = value;
    }

    private CollisionResult CheckEntityIntersection(IEnumerable<Entity> entities, Camera camera, Vector2F cursorPosition, CollisionMode collisionMode)
    {
        CollisionResult collisionResult = new CollisionResult();
        foreach (Entity entity in entities)
        {
            if (!entity.IsEnabled)
            {
                continue;
            }
            result = entity.Intersects(camera, cursorPosition, collisionMode, CompareOrder.Less, limitDistance);
            if (result.Intersects)
            {
                collisionResult.ValidateAndSetValues(result.Entity, result.IntersectionPoint, true);
            }
        }
        return collisionResult;
    }

    /// <summary>
    /// Picks and drags in the output under the pointer; places the gizmos for the camera of every visible output.
    /// </summary>
    public void Update(IEnumerable<Entity> entities, Observatory observatory)
    {
        if (observatory.PointerOutput is { Camera: not null, Input.CanLocatePointer: true })
        {
            ProcessTools(entities, observatory);
        }
        else
        {
            OrientationTool.Process(SelectedEntity, observatory);
        }

        var outputs = observatory.VisibleOutputs;
        PlaneGridTool.TraverseInDepth(
            current =>
            {
                for (int i = 0; i < outputs.Count; i++)
                {
                    if (outputs[i].Camera is not { } activeCamera)
                    {
                        continue;
                    }

                    current.Transform.CalculateFinalTransform(activeCamera, Vector3F.Zero, Matrix4x4F.Identity);
                }
            });

        Text = "Current selected entity: " + SelectedEntity + "\n";

        lightManager?.Update();
    }

    private void ProcessTools(IEnumerable<Entity> entities, Observatory observatory)
    {
        CollisionMode collisionMode = CollisionMode.IgnoreNonGeometryParts;
        var output = observatory.PointerOutput;
        var camera = output.Camera;
        var inputManager = output.Input;
        if (SelectedEntity != null && !SelectedEntity.IsEnabled)
        {
            currentTool.SetStandby();
        }

        if (!currentTool.IsLocked && !_lightProcessingResult)
        {
            result = CheckEntityIntersection(entities, camera, inputManager.RelativePosition, collisionMode);
            if (lightManager != null)
            {
                result.ValidateAgainst(lightManager.Intersects(camera, inputManager.RelativePosition, collisionMode));
            }
            result.ValidateAgainst(observatory.CameraGizmo.Intersects(collisionMode));

        }

        OrientationTool.Process(SelectedEntity, observatory);

        if (currentTool.Enabled && !_lightProcessingResult)
        {
            currentTool.LocalTransformEnabled = LocalTransformEnabled;
            currentTool.Process(SelectedEntity, observatory);
        }

        if (lightManager != null && !currentTool.IsLocked)
        {
            _lightProcessingResult = lightManager.ProcessLight(SelectedEntity, observatory);
        }

        if (result.Intersects && inputManager.IsMouseButtonPressed(MouseButton.Left) && !currentTool.IsLocked && !_lightProcessingResult)
        {
            if (SelectedEntity != null && SelectedEntity != result.Entity)
            {
                SelectedEntity.IsSelected = false;
            }
            SelectedEntity = result.Entity;
            SelectedEntity.IsSelected = true;
        }
        else if (!result.Intersects && inputManager.IsMouseButtonPressed(MouseButton.Left) && !currentTool.IsLocked && !_lightProcessingResult)
        {
            if (SelectedEntity != null)
            {
                SelectedEntity.IsSelected = false;
                SelectedEntity = null;
            }
        }
    }
}