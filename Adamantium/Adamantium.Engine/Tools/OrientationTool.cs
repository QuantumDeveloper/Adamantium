using System;
using System.Collections.Generic;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
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

    public override void Process(Entity targetEntity, CameraManager cameraManager, GameInputManager inputManager)
    {
        Tool.TraverseByLayer(current =>
        {
            var colliders = current.GetComponents<Collider>();

            foreach (var camera in cameraManager.ActiveCameras)
            {
                TransformOrientationTool(current, camera);

                foreach (var collider in colliders)
                {
                    collider.ClearData();
                    collider.UpdateForCamera(camera);
                }

                current.Transform.GetMetadata(camera).IsSelected = false;
            }
        }, true);

        // Without input the gizmo is only placed; picking and dragging need a pointer.
        var userCamera = cameraManager.UserControlledCamera;
        if (userCamera == null || inputManager == null) return;

        // While a part is HELD the pointer is pinned and hidden, so there is nothing to pick with - the grab stands
        // until the button comes up, and the highlight stays on what was grabbed.
        if (grabbed != null)
        {
            grabbed.Transform.GetMetadata(userCamera).IsSelected = true;
            Drag(userCamera, inputManager);
            return;
        }

        // ONCE, and only after every part has its matrix and bounds. This ran INSIDE the loop above - once per part,
        // each call overwriting the last - so a click read whichever part the traversal ended on rather than the
        // nearest hit, and the same spot answered a different axis each time. NEAREST wins, like every other tool.
        toolIntersectionResult = Tool.Intersects(userCamera, inputManager.RelativePosition, false,
            userCamera.UiProjection, CollisionMode.IgnoreNonGeometryParts, CompareOrder.Less, 0, false);

        if (!toolIntersectionResult.Intersects) return;

        toolIntersectionResult.Entity.Transform.GetMetadata(userCamera).IsSelected = true;

        if (!inputManager.IsMouseButtonPressed(MouseButton.Left)) return;

        grabbed = toolIntersectionResult.Entity;
        travelled = 0;
        // Takes the pointer for the duration: hidden, pinned, and reporting raw motion. Without it a drag would end
        // the moment the cursor reached the edge of the screen.
        inputManager.HoldPointer(true);
    }

    // A grab on the gizmo ORBITS the view, the way Blender's does - which is where the cube's edge and corner views
    // went: a drag reaches any angle, not one of twenty-six. A press that never travelled is a click, so the canonical
    // views stay one tap away; that is also why the click fires on RELEASE and not on the press.
    private void Drag(Camera camera, GameInputManager inputManager)
    {
        var delta = inputManager.RawMouseDelta;

        if (inputManager.IsMouseButtonDown(MouseButton.Left) && delta.LengthSquared() > 0)
        {
            travelled += delta.Length();

            if (travelled > ClickSlack)
            {
                // Takes over from a turn still playing - otherwise the animation keeps writing what the drag changes.
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
        // Far enough in for the step arrows to have room: at 60 the gizmo touched the panel's edge and was clipped.
        var relativePosition = new Vector3F(camera.Width - 120, 120, 150f);

        // World axes seen from the camera - that is the whole transform. There used to be an "orientation.X = -X"
        // after this: not a rotation but a MIRROR of the model, which left the picture handed the opposite way to the
        // maths, so a click on the arrow you saw turned to the axis you did not.
        // The STEP arrows are not axes: they are screen furniture, so they keep still while the gizmo turns.
        var orientation = IsStepArrow(current) ? QuaternionF.Identity : QuaternionF.Conjugate(camera.Rotation);

        // The part's own place WITHIN the gizmo, folded in ahead of the scale and the spin so it rides with the whole.
        // Parts share their meshes - this is the only thing that tells one ball, arm or arrow from the next.
        var world = Matrix4x4F.RotationQuaternion(current.Transform.Rotation) *
                    Matrix4x4F.Translation((Vector3F)current.Transform.Position) *
                    Matrix4x4F.Scaling(current.Transform.Scale) *
                    Matrix4x4F.RotationQuaternion(orientation);

        // NO Y flip on the way out. THIS WORLD'S UP IS -Y: the viewport height is positive (no Vulkan flip) and
        // PerspectiveFovY carries a positive M22, so view +Y lands at the BOTTOM of the screen - which is why a
        // correctly authored model stands the right way up. The gizmo draws world axes, so its +Y ball belongs at the
        // bottom too, and it already agrees with the scene it annotates.
        var metadata = current.Transform.GetMetadata(camera);
        metadata.WorldMatrixF = world * Matrix4x4F.Translation(relativePosition);
        metadata.RelativePosition = relativePosition;
    }

    // THE WORLD's up, never the camera's. Referencing the camera's axes folds whatever roll the view has accumulated
    // back into the next turn, so the axes drift a little further every click. Looking straight down leaves the world
    // up with nothing to say, and +Z takes over - the same convention a top view has always had on a map.
    private static Vector3F UpOf(Vector3F forward) =>
        Math.Abs(Vector3F.Dot(Vector3F.Up, forward)) > 0.99f ? Vector3F.ForwardLH : Vector3F.Up;

    private bool IsStepArrow(Entity entity) =>
        entity == stepRight || entity == stepLeft || entity == stepUp || entity == stepDown;

    // A step turns the view about a SCREEN axis - right/left about the screen's vertical, up/down about its
    // horizontal - which is what makes four presses walk a full circle whatever the cube currently shows.
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
            // Composed on the RIGHT of the view rotation: the axis is then the camera's own, so the turn is relative
            // to what is on screen rather than to the world.
            camera.RotateAroundSelectedObject(camera.Rotation * StepOf(selectedTool), RotationMilliseconds);
            return;
        }

        // The hub puts the axes back where the world has them - the way out of a view you cannot read any more.
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