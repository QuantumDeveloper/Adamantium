using System;
using System.Collections.Generic;
using Adamantium.Engine.Templates.Tools;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

public class OrientationTool: ToolBase
{
    private readonly Dictionary<Entity, Vector3F> tiles = new();
    private Entity grabbed;
    private float travelled;
    private Entity home;
    private Entity stepRight;
    private Entity stepLeft;
    private Entity stepUp;
    private Entity stepDown;

    public OrientationTool(float size, Vector3F baseScale, QuaternionF initialRotation) : base("OrientationTool")
    {
        var orientationTemplate = new OrientationToolTemplate(size, baseScale, initialRotation);
        Tool = orientationTemplate.BuildEntity(null, Name);

        for (var i = 0; i < OrientationToolTemplate.TileDirections.Length; ++i)
        {
            tiles[Tool.Get(OrientationToolTemplate.TileName(i))] = (Vector3F)OrientationToolTemplate.TileDirections[i];
        }

        home = Tool.Get(OrientationToolTemplate.HomeName);
        stepRight = Tool.Get("StepRightManipulator");
        stepLeft = Tool.Get("StepLeftManipulator");
        stepUp = Tool.Get("StepUpManipulator");
        stepDown = Tool.Get("StepDownManipulator");
    }

    public override void Process(Entity targetEntity, Observatory observatory)
    {
        var outputs = observatory.VisibleOutputs;
        Tool.TraverseByLayer(current =>
        {
            var colliders = current.GetComponents<Collider>();

            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].Camera is not { } camera)
                {
                    continue;
                }

                TransformOrientationTool(current, camera);

                foreach (var collider in colliders)
                {
                    collider.ClearData();
                    collider.UpdateForCamera(camera);
                }

                current.Transform.GetMetadata(camera).IsSelected = false;
            }
        }, true);

        if (observatory.PointerOutput is not { Camera: { } userCamera, Input: { CanLocatePointer: true } inputManager })
        {
            return;
        }

        // A held part keeps the grab until release: the pointer is pinned, so there is nothing to pick with.
        if (grabbed != null)
        {
            grabbed.Transform.GetMetadata(userCamera).IsSelected = true;
            Drag(userCamera, inputManager);
            return;
        }

        // Once, after every part has its matrix: the nearest hit wins.
        toolIntersectionResult = Tool.Intersects(userCamera, inputManager.RelativePosition, false,
            userCamera.UiProjection, CollisionMode.IgnoreNonGeometryParts, CompareOrder.Less, 0, false);

        if (!toolIntersectionResult.Intersects) return;

        toolIntersectionResult.Entity.Transform.GetMetadata(userCamera).IsSelected = true;

        if (!inputManager.IsMouseButtonPressed(MouseButton.Left)) return;

        grabbed = toolIntersectionResult.Entity;
        travelled = 0;
        // Or the drag ends at the screen edge.
        inputManager.HoldPointer(true);
    }

    // A drag orbits the view, as in Blender; a press that never travelled is a click, so it fires on release.
    private void Drag(Camera camera, InputWormhole inputManager)
    {
        var delta = inputManager.RawMouseDelta;

        if (inputManager.IsMouseButtonDown(MouseButton.Left) && delta.LengthSquared() > 0)
        {
            travelled += delta.Length();

            if (travelled > ClickSlack)
            {
                // Or a turn still playing overwrites the drag.
                camera.CancelTravel();
                camera.RotateRelativeXY(delta.Y * DragDegreesPerPixel, -delta.X * DragDegreesPerPixel);
            }
        }

        if (!inputManager.IsMouseButtonReleased(MouseButton.Left)) return;

        inputManager.HoldPointer(false);

        if (travelled <= ClickSlack)
        {
            selectedTool = grabbed;
            HandleMouseClick(camera);
        }

        grabbed = null;
    }

    // Pixels of travel a press is allowed before it stops being a click, and how far a pixel turns the view.
    private const float ClickSlack = 3;
    private const float DragDegreesPerPixel = 0.4f;

    private void TransformOrientationTool(Entity current, Camera camera)
    {
        // Room for the step arrows: at 60 the gizmo was clipped by the panel edge.
        var relativePosition = new Vector3F(camera.Width - 120, 120, 150f);

        // World axes seen from the camera, and nothing more. The step arrows are screen furniture and keep still.
        var orientation = IsStepArrow(current) ? QuaternionF.Identity : QuaternionF.Conjugate(camera.Rotation);

        // The part's place within the gizmo, ahead of the scale and spin: parts share meshes, so this tells them apart.
        var world = Matrix4x4F.RotationQuaternion(current.Transform.Rotation) *
                    Matrix4x4F.Translation((Vector3F)current.Transform.Position) *
                    Matrix4x4F.Scaling(current.Transform.Scale) *
                    Matrix4x4F.RotationQuaternion(orientation);

        // No Y flip: this world's up is -Y, so the +Y ball belongs at the bottom, as in the scene.
        var metadata = current.Transform.GetMetadata(camera);
        metadata.WorldMatrixF = world * Matrix4x4F.Translation(relativePosition);
        metadata.RelativePosition = relativePosition;
    }

    // The world's up, not the camera's, or accumulated roll drifts into every turn. Looking straight down, +Z takes over.
    private static Vector3F UpOf(Vector3F forward) =>
        Math.Abs(Vector3F.Dot(Vector3F.Up, forward)) > 0.99f ? Vector3F.ForwardLH : Vector3F.Up;

    private bool IsStepArrow(Entity entity) =>
        entity == stepRight || entity == stepLeft || entity == stepUp || entity == stepDown;

    // About a screen axis, so four presses walk a full circle whatever the view.
    private QuaternionF StepOf(Entity arrow)
    {
        var quarter = MathHelper.DegreesToRadians(StepDegrees);

        if (arrow == stepRight) return QuaternionF.RotationAxis(Vector3F.Up, quarter);
        if (arrow == stepLeft) return QuaternionF.RotationAxis(Vector3F.Up, -quarter);
        if (arrow == stepUp) return QuaternionF.RotationAxis(Vector3F.Right, quarter);
        if (arrow == stepDown) return QuaternionF.RotationAxis(Vector3F.Right, -quarter);

        return QuaternionF.Identity;
    }

    private const float StepDegrees = 90;

    private void HandleMouseClick(Camera camera)
    {
        if (IsStepArrow(selectedTool))
        {
            // On the right of the view rotation: the axis is the camera's own.
            camera.RotateAroundSelectedObject(camera.Rotation * StepOf(selectedTool), RotationMilliseconds);
            return;
        }

        // The hub restores the world axes.
        if (selectedTool == home)
        {
            camera.RotateAroundSelectedObject(QuaternionF.Identity, RotationMilliseconds);
            return;
        }

        if (!tiles.TryGetValue(selectedTool, out var ball)) return;

        // Stand ON that ball and look back at the hub: six balls, six canonical views.
        var forward = -Vector3F.Normalize(ball);

        camera.RotateAroundSelectedObject(
            QuaternionF.RotationLookAtLH(forward, UpOf(forward)), RotationMilliseconds);
    }

    private const int RotationMilliseconds = 500;
}